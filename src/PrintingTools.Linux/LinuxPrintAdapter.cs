using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using PrintingTools.Core;
using PrintingTools.Core.Preview;
using PrintingTools.Core.Rendering;
using PrintingTools.Linux.Dialogs;
using PrintingTools.Linux.Rendering;

namespace PrintingTools.Linux;

/// <summary>
/// Provides a first-pass Linux printing adapter backed by the system CUPS clients.
/// </summary>
public sealed class LinuxPrintAdapter : IPrintAdapter, IPrintPreviewProvider
{
    private const string DiagnosticsCategory = "LinuxPrintAdapter";
    private static readonly Vector TargetPrintDpi = new(300, 300);
    private static readonly Vector TargetPreviewDpi = new(144, 144);
    private static readonly Regex MediaSizePattern = new(@"(?<width>\d+(?:\.\d+)?)x(?<height>\d+(?:\.\d+)?)(?<unit>mm|cm|in)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly CupsCommandClient _cups;
    private readonly IVectorPageRenderer _vectorRenderer;
    private readonly GtkPrintDialogBridge _gtkDialogBridge = new();

    public LinuxPrintAdapter()
        : this(CupsCommandClient.CreateDefault(), new SkiaVectorPageRenderer())
    {
    }

    internal LinuxPrintAdapter(CupsCommandClient cups, IVectorPageRenderer? vectorRenderer = null)
    {
        _cups = cups ?? throw new ArgumentNullException(nameof(cups));
        _vectorRenderer = vectorRenderer ?? new SkiaVectorPageRenderer();
    }

    public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        EnsureSupported();

        var printers = new List<PrinterInfo>();
        var defaultPrinter = await TryGetDefaultPrinterAsync(cancellationToken).ConfigureAwait(false);

        var result = await _cups.RunAsync("lpstat", new[] { "-p" }, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "lpstat -p failed; returning empty printer list.", result.Exception, new { result.ExitCode, result.StandardError });
            return printers;
        }

        using var reader = new StringReader(result.StandardOutput);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!line.StartsWith("printer ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tokens = line.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length < 2)
            {
                continue;
            }

            var printerName = tokens[1];
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["RawStatus"] = line
            };

            if (tokens.Length >= 4)
            {
                attributes["State"] = tokens[3];
            }

            var info = new PrinterInfo(
                new PrinterId(printerName),
                printerName,
                isDefault: string.Equals(defaultPrinter, printerName, StringComparison.OrdinalIgnoreCase),
                isOnline: !line.Contains("disabled", StringComparison.OrdinalIgnoreCase),
                isLocal: true,
                attributes);

            printers.Add(info);
        }

        return printers;
    }

    public async Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default)
    {
        EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        var args = new[] { "-p", printerId.Value, "-l" };
        var result = await _cups.RunAsync("lpoptions", args, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, $"lpoptions failed for '{printerId}'.", result.Exception, new { result.ExitCode, result.StandardError });
            return PrintCapabilities.CreateDefault();
        }

        var mediaMap = new Dictionary<string, MediaDescriptor>(StringComparer.OrdinalIgnoreCase);
        var colorModes = new HashSet<ColorMode>();
        var copies = new SortedSet<int>();
        var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var orientations = new List<PageOrientation> { PageOrientation.Portrait, PageOrientation.Landscape };

        var duplexing = DuplexingSupport.None;
        string? defaultMediaRaw = null;
        string? defaultColorRaw = null;
        string? defaultDuplexRaw = null;

        using var reader = new StringReader(result.StandardOutput);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (line.Contains(':', StringComparison.Ordinal))
            {
                ParseOptionLine(
                    line,
                    mediaMap,
                    colorModes,
                    copies,
                    extensions,
                    ref duplexing,
                    ref defaultMediaRaw,
                    ref defaultColorRaw,
                    ref defaultDuplexRaw);
            }
            else
            {
                ParseAttributeLine(line, extensions);
            }
        }

        if (mediaMap.Count == 0)
        {
            foreach (var fallback in PrintCapabilities.CreateDefault().PageMediaSizes)
            {
                mediaMap[fallback.Size.Name] = new MediaDescriptor(fallback.Size, fallback.IsDefault, fallback.Metadata);
            }
        }

        if (colorModes.Count == 0)
        {
            colorModes.Add(ColorMode.Color);
            colorModes.Add(ColorMode.Monochrome);
        }

        if (copies.Count == 0)
        {
            for (var i = 1; i <= 10; i++)
            {
                copies.Add(i);
            }
        }

        if (duplexing == DuplexingSupport.None)
        {
            duplexing = DuplexingSupport.LongEdge | DuplexingSupport.ShortEdge;
        }

        var mediaInfos = mediaMap
            .Values
            .Select(descriptor => new PageMediaSizeInfo(descriptor.Size, descriptor.IsDefault, descriptor.Metadata))
            .ToList();

        var capability = new PrintCapabilities(
            new ReadOnlyCollection<PageMediaSizeInfo>(mediaInfos),
            new ReadOnlyCollection<PageOrientation>(orientations),
            duplexing,
            new ReadOnlyCollection<ColorMode>(colorModes.ToList()),
            new ReadOnlyCollection<int>(copies.ToList()),
            new ReadOnlyDictionary<string, string>(extensions));

        if (baseTicket is not null)
        {
            if (!string.IsNullOrWhiteSpace(defaultMediaRaw))
            {
                baseTicket.Extensions["cups.media"] = defaultMediaRaw;
            }

            if (!string.IsNullOrWhiteSpace(defaultColorRaw))
            {
                baseTicket.Extensions["cups.print-color-mode"] = defaultColorRaw;
            }

            if (!string.IsNullOrWhiteSpace(defaultDuplexRaw))
            {
                baseTicket.Extensions["cups.sides"] = defaultDuplexRaw;
            }
        }

        return capability;
    }

    public async Task<PrintSession> CreateSessionAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupported();
        cancellationToken.ThrowIfCancellationRequested();

        var options = request.Options?.Clone() ?? new PrintOptions();
        var ticket = (request.Ticket ?? PrintTicketModel.CreateDefault()).Clone();

        var session = new PrintSession(request.Document, options, request.Description, ticket: ticket);

        try
        {
            var printers = await GetPrintersAsync(cancellationToken).ConfigureAwait(false);
            PrinterInfo? selected = null;

            if (request.PreferredPrinterId is { } preferredId)
            {
                selected = printers.FirstOrDefault(p => p.Id == preferredId);
            }

            if (selected is null && !string.IsNullOrWhiteSpace(options.PrinterName))
            {
                selected = printers.FirstOrDefault(p =>
                    string.Equals(p.Name, options.PrinterName, StringComparison.OrdinalIgnoreCase));
            }

            selected ??= printers.FirstOrDefault(p => p.IsDefault) ?? printers.FirstOrDefault();

            if (selected is not null)
            {
                session.AssignPrinter(selected);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Failed to select default printer during session creation.", ex);
        }

        return session;
    }

    public Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        var includeVector = session.Options.UseVectorRenderer;
        var preview = PrintPreviewModel.Create(
            session,
            TargetPreviewDpi,
            includeBitmaps: true,
            includeVectorDocument: includeVector,
            vectorRenderer: includeVector ? _vectorRenderer : null,
            cancellationToken);

        return Task.FromResult(preview);
    }

    public async Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        var options = session.Options;
        if (options.ShowPrintDialog)
        {
            var dialogOutcome = await PresentPrintDialogAsync(session, cancellationToken).ConfigureAwait(false);
            options.ShowPrintDialog = false;
            if (dialogOutcome == PrintDialogOutcome.Cancelled)
            {
                session.NotifyJobEvent(PrintJobEventKind.Cancelled, "Print dialog cancelled by the user.");
                return;
            }
        }

        var pages = PrintRenderPipeline.CollectPages(session, TargetPrintDpi, cancellationToken);
        if (pages.Count == 0)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Print session contained no pages.", context: new { session.Description });
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.PdfOutputPath))
        {
            session.NotifyJobEvent(PrintJobEventKind.Started, "Exporting PDF via managed pipeline.");
            _vectorRenderer.ExportPdf(options.PdfOutputPath!, pages);
            session.NotifyJobEvent(PrintJobEventKind.Completed, "Managed PDF export completed.");

            if (!options.UseManagedPdfExporter)
            {
                return;
            }
        }

        var printer = session.Printer;
        if (printer is null)
        {
            var printers = await GetPrintersAsync(cancellationToken).ConfigureAwait(false);
            printer = printers.FirstOrDefault(p => p.IsDefault) ?? printers.FirstOrDefault();
            if (printer is not null)
            {
                session.AssignPrinter(printer);
            }
        }

        if (printer is null)
        {
            throw new InvalidOperationException("No printers are available via CUPS.");
        }

        var workingPath = Path.Combine(Path.GetTempPath(), $"printingtools-linux-{Guid.NewGuid():N}.pdf");
        session.NotifyJobEvent(PrintJobEventKind.Started, $"Submitting print job to '{printer.Name}'.");

        try
        {
            _vectorRenderer.ExportPdf(workingPath, pages);

            var lpArguments = BuildLpArguments(session, printer, workingPath);
            var result = await _cups.RunAsync("lp", lpArguments, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                var message = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"lp failed with exit code {result.ExitCode}."
                    : result.StandardError.Trim();

                throw new InvalidOperationException(message);
            }

            session.NotifyJobEvent(PrintJobEventKind.Completed, $"Submitted job to '{printer.Name}'.");
        }
        catch (Exception ex)
        {
            session.NotifyJobEvent(PrintJobEventKind.Failed, "Submitting print job failed.", ex);
            PrintDiagnostics.Report(DiagnosticsCategory, "Submitting CUPS job failed.", ex, new { printer = printer.Name });
            throw;
        }
        finally
        {
            TryDeleteTemporary(workingPath);
        }
    }

    private async Task<PrintDialogOutcome> PresentPrintDialogAsync(PrintSession session, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        var gtkResult = _gtkDialogBridge.ShowDialog(session);
        if (gtkResult.Status == GtkPrintDialogStatus.Accepted)
        {
            return PrintDialogOutcome.Confirmed;
        }

        if (gtkResult.Status == GtkPrintDialogStatus.Cancelled)
        {
            return PrintDialogOutcome.Cancelled;
        }

        if (gtkResult.Status == GtkPrintDialogStatus.Error)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "GTK dialog failed; falling back to managed dialog.", gtkResult.Exception);
        }

        IReadOnlyList<PrinterInfo> printers;
        try
        {
            printers = await GetPrintersAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Failed to enumerate printers for managed dialog.", ex);
            printers = Array.Empty<PrinterInfo>();
        }

        var fallbackOutcome = await ManagedPrintDialogFallback
            .TryShowAsync(session, printers, cancellationToken)
            .ConfigureAwait(false);

        switch (fallbackOutcome)
        {
            case ManagedPrintDialogOutcome.Accepted:
                return PrintDialogOutcome.Confirmed;
            case ManagedPrintDialogOutcome.Cancelled:
                return PrintDialogOutcome.Cancelled;
            default:
                PrintDiagnostics.Report(DiagnosticsCategory, "Managed print dialog unavailable; continuing with existing settings.");
                return PrintDialogOutcome.Confirmed;
        }
    }

    private static void EnsureSupported()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Linux printing adapter requires the current platform to be Linux.");
        }

        if (!CupsCommandClient.IsInstalled())
        {
            throw new PlatformNotSupportedException("CUPS command-line tools are not installed or not accessible in PATH.");
        }
    }

    private async Task<string?> TryGetDefaultPrinterAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _cups.RunAsync("lpstat", new[] { "-d" }, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return null;
            }

            var raw = result.StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            const string Prefix = "system default destination:";
            if (raw.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return raw.Substring(Prefix.Length).Trim();
            }

            var colonIndex = raw.LastIndexOf(':');
            return colonIndex >= 0 ? raw[(colonIndex + 1)..].Trim() : raw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Unable to determine default printer.", ex);
            return null;
        }
    }

    private static void ParseAttributeLine(string line, IDictionary<string, string> extensions)
    {
        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();
        if (key.Length == 0 || value.Length == 0)
        {
            return;
        }

        extensions[key] = value;
    }

    private static void ParseOptionLine(
        string line,
        IDictionary<string, MediaDescriptor> mediaMap,
        ISet<ColorMode> colorModes,
        SortedSet<int> copies,
        IDictionary<string, string> extensions,
        ref DuplexingSupport duplexing,
        ref string? defaultMediaRaw,
        ref string? defaultColorRaw,
        ref string? defaultDuplexRaw)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
        {
            return;
        }

        var header = line[..colonIndex].Trim();
        var optionsSegment = line[(colonIndex + 1)..].Trim();
        if (optionsSegment.Length == 0)
        {
            return;
        }

        var slashIndex = header.IndexOf('/');
        var optionName = slashIndex >= 0 ? header[..slashIndex] : header;

        var choices = ParseChoices(optionsSegment);

        switch (optionName)
        {
            case "PageSize":
            case "media":
                foreach (var choice in choices)
                {
                    var descriptor = TryCreateMediaDescriptor(choice);
                    if (descriptor is null)
                    {
                        continue;
                    }

                    if (mediaMap.TryGetValue(descriptor.Value.Size.Name, out var existing))
                    {
                        var isDefault = existing.IsDefault || descriptor.Value.IsDefault;
                        var metadata = descriptor.Value.Metadata.Count > 0 ? descriptor.Value.Metadata : existing.Metadata;
                        mediaMap[descriptor.Value.Size.Name] = new MediaDescriptor(existing.Size, isDefault, metadata);
                    }
                    else
                    {
                        mediaMap[descriptor.Value.Size.Name] = descriptor.Value;
                    }

                    if (descriptor.Value.IsDefault && string.IsNullOrWhiteSpace(defaultMediaRaw))
                    {
                        defaultMediaRaw = choice.RawValue;
                    }
                }

                break;

            case "Duplex":
            case "sides":
                foreach (var choice in choices)
                {
                    duplexing |= MapDuplex(choice.Value);
                    if (choice.IsDefault && string.IsNullOrWhiteSpace(defaultDuplexRaw))
                    {
                        defaultDuplexRaw = MapDuplexOption(choice.Value);
                    }
                }

                break;

            case "ColorModel":
            case "print-color-mode":
                foreach (var choice in choices)
                {
                    if (TryMapColor(choice.Value, out var colorMode))
                    {
                        colorModes.Add(colorMode);
                        if (choice.IsDefault && string.IsNullOrWhiteSpace(defaultColorRaw))
                        {
                            defaultColorRaw = choice.RawValue;
                        }
                    }
                }

                break;

            case "Copies":
            case "copies":
                foreach (var choice in choices)
                {
                    if (int.TryParse(choice.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    {
                        copies.Add(Math.Clamp(value, 1, 999));
                    }
                }

                break;
        }

        extensions[$"cups.option.{optionName}"] = optionsSegment;
    }

    private static IEnumerable<LpOptionChoice> ParseChoices(string segment)
    {
        var tokens = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var isDefault = token[0] == '*';
            var value = isDefault ? token[1..] : token;
            yield return new LpOptionChoice(token, value, isDefault);
        }
    }

    private static MediaDescriptor? TryCreateMediaDescriptor(LpOptionChoice choice)
    {
        var mediaSize = TryMapMediaChoice(choice.Value) ?? TryParseMediaSize(choice.Value);
        if (mediaSize is null)
        {
            return null;
        }

        var metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cups.media"] = choice.RawValue
        });

        return new MediaDescriptor(mediaSize, choice.IsDefault, metadata);
    }

    private static PageMediaSize? TryMapMediaChoice(string value)
    {
        var normalized = value.Replace('-', '_').ToLowerInvariant();

        if (normalized.Contains("letter", StringComparison.Ordinal))
        {
            return CommonPageMediaSizes.Letter;
        }

        if (normalized.Contains("legal", StringComparison.Ordinal))
        {
            return CommonPageMediaSizes.Legal;
        }

        if (normalized.Contains("a4", StringComparison.Ordinal))
        {
            return CommonPageMediaSizes.A4;
        }

        if (normalized.Contains("tabloid", StringComparison.Ordinal) || normalized.Contains("ledger", StringComparison.Ordinal))
        {
            return CommonPageMediaSizes.Tabloid;
        }

        return null;
    }

    private static PageMediaSize? TryParseMediaSize(string value)
    {
        var match = MediaSizePattern.Match(value);
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups["width"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
        {
            return null;
        }

        if (!double.TryParse(match.Groups["height"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        var widthPoints = ConvertToPoints(width, unit);
        var heightPoints = ConvertToPoints(height, unit);

        if (widthPoints <= 0 || heightPoints <= 0)
        {
            return null;
        }

        return new PageMediaSize(value, widthPoints, heightPoints);
    }

    private static double ConvertToPoints(double value, string unit) => unit switch
    {
        "in" => value * 72d,
        "mm" => value / 25.4d * 72d,
        "cm" => value / 2.54d * 72d,
        _ => 0d
    };

    private static DuplexingSupport MapDuplex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DuplexingSupport.None;
        }

        return value.ToLowerInvariant() switch
        {
            "duplexnotumble" or "two-sided-long-edge" or "long-edge" => DuplexingSupport.LongEdge,
            "duplextumble" or "two-sided-short-edge" or "short-edge" => DuplexingSupport.ShortEdge,
            _ => DuplexingSupport.None
        };
    }

    private static string MapDuplexOption(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "duplexnotumble" => "two-sided-long-edge",
            "duplextumble" => "two-sided-short-edge",
            "two-sided-short-edge" => "two-sided-short-edge",
            "two-sided-long-edge" => "two-sided-long-edge",
            _ => "one-sided"
        };
    }

    private static bool TryMapColor(string value, out ColorMode colorMode)
    {
        var normalized = value.ToLowerInvariant();
        colorMode = ColorMode.Auto;

        if (normalized.Contains("gray", StringComparison.Ordinal) || normalized.Contains("mono", StringComparison.Ordinal))
        {
            colorMode = ColorMode.Monochrome;
            return true;
        }

        if (normalized.Contains("color", StringComparison.Ordinal) || normalized.Contains("rgb", StringComparison.Ordinal))
        {
            colorMode = ColorMode.Color;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> BuildLpArguments(PrintSession session, PrinterInfo printer, string pdfPath)
    {
        var args = new List<string>
        {
            "-d",
            printer.Name,
            "-t",
            session.Description ?? session.Options.JobName ?? "Avalonia Print Job"
        };

        var copies = Math.Clamp(session.Ticket.Copies, 1, 999);
        args.Add("-n");
        args.Add(copies.ToString(CultureInfo.InvariantCulture));

        foreach (var option in BuildPrintOptions(session))
        {
            args.Add("-o");
            args.Add(option);
        }

        args.Add(pdfPath);
        return args;
    }

    private static IEnumerable<string> BuildPrintOptions(PrintSession session)
    {
        if (session.Options.PageRange is { } range)
        {
            yield return $"page-ranges={range.StartPage}-{range.EndPage}";
        }

        var duplex = session.Ticket.Duplex switch
        {
            DuplexingMode.TwoSidedLongEdge => "two-sided-long-edge",
            DuplexingMode.TwoSidedShortEdge => "two-sided-short-edge",
            _ => "one-sided"
        };
        yield return $"sides={duplex}";

        if (session.Ticket.ColorMode == ColorMode.Monochrome)
        {
            yield return "print-color-mode=monochrome";
        }
        else if (session.Ticket.ColorMode == ColorMode.Color)
        {
            yield return "print-color-mode=color";
        }

        if (TryGetCupsMedia(session, out var mediaValue))
        {
            yield return $"media={mediaValue}";
        }
    }

    private static bool TryGetCupsMedia(PrintSession session, out string media)
    {
        media = string.Empty;

        if (session.Ticket.Extensions.TryGetValue("cups.media", out var ticketMedia) && !string.IsNullOrWhiteSpace(ticketMedia))
        {
            media = ticketMedia;
            return true;
        }

        if (session.Capabilities is not null)
        {
            var match = session.Capabilities.PageMediaSizes.FirstOrDefault(info => info.Size.Equals(session.Ticket.PageMediaSize));
            if (match?.Metadata is not null && match.Metadata.TryGetValue("cups.media", out var metadataMedia) && !string.IsNullOrWhiteSpace(metadataMedia))
            {
                media = metadataMedia;
                return true;
            }
        }

        return false;
    }

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

    private readonly record struct MediaDescriptor(PageMediaSize Size, bool IsDefault, IReadOnlyDictionary<string, string> Metadata);

    private readonly record struct LpOptionChoice(string RawValue, string Value, bool IsDefault);
}

internal enum PrintDialogOutcome
{
    Confirmed,
    Cancelled
}
