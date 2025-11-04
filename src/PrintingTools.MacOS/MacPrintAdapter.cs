using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Platform;
using PrintingTools.Core;
using PrintingTools.Core.Preview;
using PrintingTools.Core.Rendering;
using PrintingTools.MacOS.Native;
using PrintingTools.MacOS.Rendering;

namespace PrintingTools.MacOS;

internal enum NativePrintPanelMode
{
    Modal,
    Sheet
}

/// <summary>
/// Placeholder macOS implementation that will bridge Avalonia visuals to AppKit printing APIs.
/// </summary>
public sealed class MacPrintAdapter : IPrintAdapter, IPrintPreviewProvider
{
    private static readonly Vector TargetPrintDpi = new(300, 300);
    private static readonly Vector TargetPreviewDpi = new(144, 144);
    private static readonly Size DefaultPageSize = new Size(816, 1056);
    private static readonly Thickness DefaultMargins = new Thickness(48);
    private const double DipsPerInch = 96d;
    private const double PointsPerInch = 72d;
    private const int BytesPerPixel = 4;
    private const int PixelFormatBgra8888 = 0;
    private const int PixelFormatRgba8888 = 1;
    private static readonly bool EnableRenderDiagnostics =
        string.Equals(Environment.GetEnvironmentVariable("PRINTINGTOOLS_TRACE_RENDER"), "1", StringComparison.OrdinalIgnoreCase);
    private static readonly IVectorPageRenderer VectorRenderer = new SkiaVectorPageRenderer();
    private const string DiagnosticsCategory = "MacPrintAdapter";
    private const int MacColorAuto = 0;
    private const int MacColorMonochrome = 1;
    private const int MacColorColor = 2;
    private const int MacDuplexNone = 0;
    private const int MacDuplexLongEdge = 1;
    private const int MacDuplexShortEdge = 2;
    private const int MacColorSpaceAuto = 0;
    private const int MacColorSpaceSrgb = 1;
    private const int MacColorSpaceDisplayP3 = 2;
    private const int JobEventWillRun = 0;
    private const int JobEventCompleted = 1;
    private const int JobEventFailed = 2;
    private const int JobEventCancelled = 3;
    private static readonly Regex MediaSizePattern = new(@"(?<width>\d+(?:\.\d+)?)x(?<height>\d+(?:\.\d+)?)(?<unit>mm|cm|in)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static void EmitDiagnostic(string message, Exception? exception = null, object? context = null) =>
        PrintDiagnostics.Report(DiagnosticsCategory, message, exception, context);

    private static void EmitTrace(string message, object? context = null)
    {
        if (EnableRenderDiagnostics)
        {
            PrintDiagnostics.Report($"{DiagnosticsCategory}.Trace", message, null, context);
        }
    }

    public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();

        IReadOnlyList<string> printerNames;
        try
        {
            printerNames = MacPrinterCatalog.GetInstalledPrinters();
        }
        catch (Exception ex)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Enumerating printers failed.", ex);
            return Array.Empty<PrinterInfo>();
        }

        if (printerNames.Count == 0)
        {
            return Array.Empty<PrinterInfo>();
        }

        string? defaultPrinter = null;
        try
        {
            defaultPrinter = await TryGetDefaultPrinterAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Unable to determine default printer.", ex);
        }

        var printers = new List<PrinterInfo>(printerNames.Count);
        var hasDefault = false;

        foreach (var name in printerNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var isDefault = defaultPrinter is not null &&
                string.Equals(name, defaultPrinter, StringComparison.OrdinalIgnoreCase);

            hasDefault |= isDefault;

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Source"] = "NSPrinter",
                ["Catalog"] = "MacPrinterCatalog"
            };

            printers.Add(new PrinterInfo(
                new PrinterId(name),
                name,
                isDefault: isDefault,
                isOnline: true,
                isLocal: true,
                attributes));
        }

        if (!hasDefault && printers.Count > 0)
        {
            var first = printers[0];
            printers[0] = new PrinterInfo(first.Id, first.Name, isDefault: true, first.IsOnline, first.IsLocal, first.Attributes);
        }

        return printers;
    }

    public async Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();

        var args = new[] { "-p", printerId.Value, "-l" };
        var result = await RunCupsCommandAsync("lpoptions", args, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                $"lpoptions failed for '{printerId.Value}'.",
                result.Exception,
                new { result.ExitCode, result.StandardError });
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

        using (var reader = new StringReader(result.StandardOutput))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

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
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();

        var options = request.Options?.Clone() ?? new PrintOptions();
        var ticket = (request.Ticket ?? PrintTicketModel.CreateDefault()).Clone();

        var session = new PrintSession(request.Document, options, request.Description, ticket: ticket);

        var printers = await GetPrintersAsync(cancellationToken).ConfigureAwait(false);
        PrinterInfo? selected = null;
        if (request.PreferredPrinterId is { } preferredId)
        {
            selected = printers.FirstOrDefault(p => p.Id == preferredId);
        }

        selected ??= printers.FirstOrDefault(p => p.IsDefault) ?? printers.FirstOrDefault();
        if (selected is not null)
        {
            session.AssignPrinter(selected);
        }

        return session;
    }

    public async Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        var pages = PrintRenderPipeline.CollectPages(session, TargetPrintDpi, cancellationToken);

        EmitDiagnostic(
            $"CollectPages -> {pages.Count} pages for session '{session.Description ?? string.Empty}'.",
            context: new { session.Description, PageCount = pages.Count });
        for (var i = 0; i < pages.Count; i++)
        {
            var tag = (pages[i].Visual as Control)?.Tag ?? "<null>";
            EmitTrace(
                $"Page[{i}] details",
                new
                {
                    Index = i,
                    Tag = tag,
                    pages[i].Metrics?.ContentOffset
                });
        }

        if (session.Options.ShowPrintDialog && TryDisplayNativePrintPanel(session, pages))
        {
            return;
        }

        if (session.Options.UseManagedPdfExporter)
        {
            HandleManagedPdfExport(session, pages);
            return;
        }

        if (TryExportManagedPdfIfRequested(session, pages))
        {
            return;
        }

        if (TryCommitVectorPrint(session, pages))
        {
            return;
        }

        await SubmitToNativePrintOperationAsync(session, pages, cancellationToken).ConfigureAwait(false);
    }

    public Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        var pages = PrintRenderPipeline.CollectPages(session, TargetPreviewDpi, cancellationToken);

        if (EnableRenderDiagnostics)
        {
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metrics = page.Metrics ?? PrintPageMetrics.Create(page.Visual, page.Settings, TargetPreviewDpi);
                LogRenderDiagnostics(page, metrics);
            }
        }

        byte[]? vectorDocument = null;
        if (session.Options.UseVectorRenderer)
        {
            vectorDocument = PrintRenderPipeline.TryCreateVectorDocument(pages, VectorRenderer);
        }

        return Task.FromResult(new PrintPreviewModel(pages, vectorDocument: vectorDocument));
    }

    private static bool TryExportManagedPdfIfRequested(PrintSession session, IReadOnlyList<PrintPage> pages)
    {
        var options = session.Options;
        if (string.IsNullOrWhiteSpace(options.PdfOutputPath))
        {
            return false;
        }

        if (options.UseManagedPdfExporter)
        {
            return false;
        }

        session.NotifyJobEvent(PrintJobEventKind.Started, "Exporting macOS PDF via managed pipeline.");
        VectorRenderer.ExportPdf(options.PdfOutputPath!, pages);
        session.NotifyJobEvent(PrintJobEventKind.Completed, "Managed PDF export completed.");
        return true;
    }

    private static void HandleManagedPdfExport(PrintSession session, IReadOnlyList<PrintPage> pages)
    {
        var options = session.Options;
        if (pages.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.PdfOutputPath))
        {
            session.NotifyJobEvent(PrintJobEventKind.Started, "Exporting PDF via managed exporter before presenting panel.");
            VectorRenderer.ExportPdf(options.PdfOutputPath!, pages);
            session.NotifyJobEvent(PrintJobEventKind.Completed, "Managed PDF export completed.");
            if (!options.ShowPrintDialog)
            {
                return;
            }
        }

        var pdfBytes = VectorRenderer.CreatePdfBytes(pages);
        if (pdfBytes.Length == 0)
        {
            return;
        }

        var showPanel = options.ShowPrintDialog ? 1 : 0;
        _ = PrintingToolsInterop.RunPdfPrintOperation(pdfBytes, pdfBytes.Length, showPanel);
    }

    private static bool TryDisplayNativePrintPanel(PrintSession session, IReadOnlyList<PrintPage> pages) =>
        ShowNativePrintPanel(session, pages, NativePrintPanelMode.Modal, IntPtr.Zero, out _, mutateSession: true);

    internal static bool ShowNativePrintPanel(
        PrintSession session,
        IReadOnlyList<PrintPage> pages,
        NativePrintPanelMode mode,
        IntPtr hostWindow,
        out IntPtr previewViewHandle,
        bool mutateSession = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(pages);

        if (mode == NativePrintPanelMode.Sheet && hostWindow == IntPtr.Zero)
        {
            throw new ArgumentException("Sheet presentation requires a valid window handle.", nameof(hostWindow));
        }

        previewViewHandle = IntPtr.Zero;

        var printContext = new PrintContext(session, pages, TargetPrintDpi, ConvertColorSpacePreference(session.Options.ColorSpacePreference));
        var contextHandle = GCHandle.Alloc(printContext);
        var callbacks = new PrintingToolsInterop.ManagedCallbacks
        {
            Context = GCHandle.ToIntPtr(contextHandle),
            RenderPage = RenderPageDelegate,
            GetPageCount = GetPageCountDelegate,
            LogDiagnostic = LogDiagnosticDelegate,
            JobEvent = JobEventDelegate
        };

        var nativeCallbacks = callbacks.ToNative();
        PrintingToolsInterop.PrintOperationHandle? operationHandle = null;

        try
        {
            var operationPtr = PrintingToolsInterop.CreatePrintOperation(nativeCallbacks);
            if (operationPtr == IntPtr.Zero)
            {
                return false;
            }

            operationHandle = new PrintingToolsInterop.PrintOperationHandle(operationPtr);
            ConfigureNativeOperation(operationHandle.DangerousGetHandle(), session, pages);

            previewViewHandle = PrintingToolsInterop.GetPreviewView(operationHandle.DangerousGetHandle());

            using var title = new NativeString(session.Description ?? session.Options.JobName ?? "Print");

            var range = session.Options.PageRange ?? new PrintPageRange(1, pages.Count);

            var panelOptions = new PrintingToolsInterop.PrintPanelOptions
            {
                Operation = operationHandle.DangerousGetHandle(),
                AllowsSelection = session.Options.EnableSelection ? 1 : 0,
                RequestedRangeStart = range.StartPage,
                RequestedRangeEnd = range.EndPage,
                RequestedCopies = Math.Clamp(session.Ticket.Copies, 1, 999),
                Title = title.Pointer,
                TitleLength = title.Length
            };

            if (session.Options.ShowPageLayoutDialog)
            {
                var layoutResult = PrintingToolsInterop.ShowPageLayout(operationHandle.DangerousGetHandle(), ref panelOptions);
                if (layoutResult == PrintingToolsInterop.PrintPanelResultCancel)
                {
                    session.Options.ShowPageLayoutDialog = false;
                    EmitDiagnostic("User cancelled page layout dialog.");
                    session.NotifyJobEvent(PrintJobEventKind.Cancelled, "macOS page layout dialog was cancelled by the user.");
                    return true;
                }

                var layoutInfo = new PrintingToolsInterop.PrintInfo();
                if (PrintingToolsInterop.GetPrintInfo(operationHandle.DangerousGetHandle(), ref layoutInfo) == 1)
                {
                    ApplyPrintInfoToSession(session, layoutInfo, pages.Count);
                    range = session.Options.PageRange ?? new PrintPageRange(1, pages.Count);
                    panelOptions.RequestedRangeStart = range.StartPage;
                    panelOptions.RequestedRangeEnd = range.EndPage;
                    panelOptions.RequestedCopies = Math.Clamp(session.Ticket.Copies, 1, 999);
                }

                session.Options.ShowPageLayoutDialog = false;
            }

            var panelResult = mode == NativePrintPanelMode.Modal
                ? PrintingToolsInterop.ShowPrintPanel(operationHandle.DangerousGetHandle(), ref panelOptions)
                : PrintingToolsInterop.ShowPrintPanelSheet(operationHandle.DangerousGetHandle(), hostWindow, ref panelOptions);

            if (panelResult == PrintingToolsInterop.PrintPanelResultCancel)
            {
                var reason = mode == NativePrintPanelMode.Sheet
                    ? "macOS print panel sheet was cancelled by the user."
                    : "macOS print panel was cancelled by the user.";
                session.NotifyJobEvent(PrintJobEventKind.Cancelled, reason);
                return true;
            }

            var info = new PrintingToolsInterop.PrintInfo();
            if (PrintingToolsInterop.GetPrintInfo(operationHandle.DangerousGetHandle(), ref info) == 1)
            {
                ApplyPrintInfoToSession(session, info, pages.Count);
            }

            if (mutateSession)
            {
                session.Options.ShowPrintDialog = false;
            }

            return false;
        }
        catch (Exception ex)
        {
            EmitDiagnostic("ShowPrintPanel failed; falling back to managed preview.", ex);
            previewViewHandle = IntPtr.Zero;
            return TryRunVectorPreview(session.Options, pages);
        }
        finally
        {
            PrintingToolsInterop.ManagedCallbacks.FreeNative(nativeCallbacks);
            operationHandle?.Dispose();
            if (contextHandle.IsAllocated)
            {
                contextHandle.Free();
            }
        }
    }

    private static bool TryRunVectorPreview(PrintOptions options, IReadOnlyList<PrintPage> pages)
    {
        if (!options.UseVectorRenderer || !options.ShowPrintDialog)
        {
            return false;
        }

        if (pages.Count == 0)
        {
            return false;
        }

        var bytes = PrintRenderPipeline.TryCreateVectorDocument(pages, VectorRenderer);
        if (bytes is null)
        {
            return false;
        }

        var vectorDocument = new PrintingToolsInterop.VectorDocument
        {
            Length = bytes.Length,
            ShowPrintPanel = options.ShowPrintDialog ? 1 : 0
        };

        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            vectorDocument.PdfBytes = handle.AddrOfPinnedObject();
            var result = PrintingToolsInterop.RunVectorPreview(ref vectorDocument) != 0;
            EmitDiagnostic("Vector preview dispatched via macOS bridge.", context: new { result, options.UseManagedPdfExporter });
            return result;
        }
        finally
        {
            handle.Free();
        }
    }

    private static bool TryCommitVectorPrint(PrintSession session, IReadOnlyList<PrintPage> pages)
    {
        var options = session.Options;
        if (!options.UseVectorRenderer || options.ShowPrintDialog)
        {
            return false;
        }

        if (pages.Count == 0)
        {
            return false;
        }

        var bytes = PrintRenderPipeline.TryCreateVectorDocument(pages, VectorRenderer);
        if (bytes is null)
        {
            return false;
        }

        session.NotifyJobEvent(PrintJobEventKind.Started, "Submitting macOS vector print job.");
        var result = PrintingToolsInterop.RunPdfPrintOperation(bytes, bytes.Length, 0) != 0;
        EmitDiagnostic("Vector print dispatched via macOS bridge.", context: new { result });
        if (result)
        {
            session.NotifyJobEvent(PrintJobEventKind.Completed, "macOS vector print job completed.");
        }
        else
        {
            session.NotifyJobEvent(PrintJobEventKind.Failed, "macOS vector print job failed.");
        }
        return result;
    }

    private static void ApplyPrintInfoToSession(PrintSession session, PrintingToolsInterop.PrintInfo info, int totalPages)
    {
        ArgumentNullException.ThrowIfNull(session);

        var ticket = session.Ticket.Clone();
        ticket.Orientation = info.Orientation == 1 ? PageOrientation.Landscape : PageOrientation.Portrait;
        ticket.Copies = Math.Clamp(info.Copies > 0 ? info.Copies : 1, 1, 999);
        ticket.ColorMode = ConvertColorModeFromInfo(info.ColorMode);
        ticket.Duplex = ConvertDuplexFromInfo(info.Duplex);

        var paperName = info.PaperName != IntPtr.Zero && info.PaperNameLength > 0
            ? Marshal.PtrToStringUni(info.PaperName, info.PaperNameLength)
            : null;

        if (info.PaperWidth > 0 && info.PaperHeight > 0)
        {
            var name = string.IsNullOrWhiteSpace(paperName)
                ? ticket.PageMediaSize.Name
                : paperName!;

            ticket.PageMediaSize = new PageMediaSize(name, info.PaperWidth, info.PaperHeight);
        }

        ticket.Extensions["macos.margin.left"] = info.MarginLeft.ToString(CultureInfo.InvariantCulture);
        ticket.Extensions["macos.margin.top"] = info.MarginTop.ToString(CultureInfo.InvariantCulture);
        ticket.Extensions["macos.margin.right"] = info.MarginRight.ToString(CultureInfo.InvariantCulture);
        ticket.Extensions["macos.margin.bottom"] = info.MarginBottom.ToString(CultureInfo.InvariantCulture);

        session.UpdateTicket(ticket, adoptWarnings: false);

        session.Options.SelectionOnlyRequested = info.SelectionOnly != 0;

        if (info.PageRangeEnabled != 0)
        {
            var start = Math.Clamp(info.FromPage, 1, totalPages);
            var end = Math.Clamp(Math.Max(info.ToPage, info.FromPage), start, totalPages);
            session.Options.PageRange = new PrintPageRange((int)start, (int)end);
        }
        else
        {
            session.Options.PageRange = null;
        }
    }

    private static int ConvertOrientation(PageOrientation orientation) => orientation == PageOrientation.Landscape ? 1 : 0;

    private static int ConvertColorMode(ColorMode mode) => mode switch
    {
        ColorMode.Color => MacColorColor,
        ColorMode.Monochrome => MacColorMonochrome,
        _ => MacColorAuto
    };

    private static ColorMode ConvertColorModeFromInfo(int value) => value switch
    {
        MacColorColor => ColorMode.Color,
        MacColorMonochrome => ColorMode.Monochrome,
        _ => ColorMode.Auto
    };

    private static int ConvertDuplex(DuplexingMode mode) => mode switch
    {
        DuplexingMode.TwoSidedLongEdge => MacDuplexLongEdge,
        DuplexingMode.TwoSidedShortEdge => MacDuplexShortEdge,
        _ => MacDuplexNone
    };

    private static DuplexingMode ConvertDuplexFromInfo(int value) => value switch
    {
        MacDuplexLongEdge => DuplexingMode.TwoSidedLongEdge,
        MacDuplexShortEdge => DuplexingMode.TwoSidedShortEdge,
        _ => DuplexingMode.OneSided
    };

    private static int ConvertColorSpacePreference(PrintColorSpace preference) => preference switch
    {
        PrintColorSpace.SRgb => MacColorSpaceSrgb,
        PrintColorSpace.DisplayP3 => MacColorSpaceDisplayP3,
        _ => MacColorSpaceAuto
    };

    private static void ConfigureNativeOperation(IntPtr operationHandle, PrintSession session, IReadOnlyList<PrintPage> pages)
    {
        if (operationHandle == IntPtr.Zero)
        {
            return;
        }

        var firstPageMetrics = pages.Count > 0 ? pages[0].Metrics : null;
        firstPageMetrics ??= pages.Count > 0 ? PrintPageMetrics.Create(pages[0].Visual, pages[0].Settings, TargetPrintDpi) : null;

        var pageSize = firstPageMetrics?.PageSize ?? DefaultPageSize;
        var margins = firstPageMetrics?.Margins ?? DefaultMargins;

        var options = session.Options;
        var ticket = session.Ticket;
        var layout = LayoutMetadata.FromTicket(ticket);

        var hasRange = false;
        var fromPage = 1;
        var toPage = Math.Max(fromPage, pages.Count);

        if (options.PageRange is { } range)
        {
            hasRange = true;
            fromPage = range.StartPage;
            toPage = range.EndPage;
            var maximumPage = Math.Max(pages.Count, fromPage);
            toPage = Math.Clamp(toPage, fromPage, maximumPage);
        }
        else
        {
            toPage = Math.Max(fromPage, pages.Count);
        }

        if (toPage < fromPage)
        {
            toPage = fromPage;
        }

        var orientation = ConvertOrientation(ticket.Orientation);
        var copies = Math.Clamp(ticket.Copies, 1, 999);
        var colorMode = ConvertColorMode(ticket.ColorMode);
        var duplex = ConvertDuplex(ticket.Duplex);
        var colorSpacePreference = ConvertColorSpacePreference(session.Options.ColorSpacePreference);

        var jobTitle = !string.IsNullOrWhiteSpace(options.JobName)
            ? options.JobName
            : string.IsNullOrWhiteSpace(session.Description)
                ? "Avalonia Print Job"
                : session.Description!;

        var pdfPath = options.PdfOutputPath;
        if (!string.IsNullOrWhiteSpace(pdfPath))
        {
            pdfPath = Path.GetFullPath(pdfPath);
            var directory = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        var enablePdfExport = !string.IsNullOrWhiteSpace(pdfPath);
        var showPrintPanel = options.ShowPrintDialog && !enablePdfExport;
        var showProgressPanel = showPrintPanel ? 1 : 0;

        using var jobName = new NativeString(jobTitle);
        using var printerName = new NativeString(options.PrinterName);
        using var pdfPathString = new NativeString(pdfPath);

        var settings = new PrintingToolsInterop.PrintSettings
        {
            PaperWidth = DipToPoints(pageSize.Width),
            PaperHeight = DipToPoints(pageSize.Height),
            MarginLeft = DipToPoints(margins.Left),
            MarginTop = DipToPoints(margins.Top),
            MarginRight = DipToPoints(margins.Right),
            MarginBottom = DipToPoints(margins.Bottom),
            HasPageRange = hasRange ? 1 : 0,
            FromPage = fromPage,
            ToPage = toPage,
            Orientation = orientation,
            Copies = copies,
            ColorMode = colorMode,
            Duplex = duplex,
            ShowPrintPanel = showPrintPanel ? 1 : 0,
            ShowProgressPanel = showProgressPanel,
            JobName = jobName.Pointer,
            JobNameLength = jobName.Length,
            PrinterName = printerName.Pointer,
            PrinterNameLength = printerName.Length,
            EnablePdfExport = enablePdfExport ? 1 : 0,
            PdfPath = pdfPathString.Pointer,
            PdfPathLength = pdfPathString.Length,
            PageCount = pages.Count,
            DpiX = TargetPrintDpi.X,
            DpiY = TargetPrintDpi.Y,
            PreferredColorSpace = colorSpacePreference,
            LayoutMode = (int)layout.Kind,
            LayoutNUpRows = layout.NUpRows,
            LayoutNUpColumns = layout.NUpColumns,
            LayoutNUpOrder = (int)layout.NUpOrder,
            LayoutBookletBinding = layout.BookletBindLongEdge ? 1 : 0,
            LayoutPosterRows = layout.PosterRows,
            LayoutPosterColumns = layout.PosterColumns
        };

        PrintingToolsInterop.ConfigurePrintOperation(operationHandle, ref settings);
    }

    private static Task SubmitToNativePrintOperationAsync(PrintSession session, IReadOnlyList<PrintPage> pages, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("macOS printing is only supported on macOS.");
        }

        var printContext = new PrintContext(session, pages, TargetPrintDpi, ConvertColorSpacePreference(session.Options.ColorSpacePreference));
        var contextHandle = GCHandle.Alloc(printContext);
        var callbacks = new PrintingToolsInterop.ManagedCallbacks
        {
            Context = GCHandle.ToIntPtr(contextHandle),
            RenderPage = RenderPageDelegate,
            GetPageCount = GetPageCountDelegate,
            LogDiagnostic = LogDiagnosticDelegate,
            JobEvent = JobEventDelegate
        };

        var nativeCallbacks = callbacks.ToNative();
        try
        {
            using var operation = new PrintingToolsInterop.PrintOperationHandle(PrintingToolsInterop.CreatePrintOperation(nativeCallbacks));
            if (operation.IsInvalid)
            {
                throw new InvalidOperationException("Failed to create macOS print operation.");
            }

            ConfigureNativeOperation(operation.DangerousGetHandle(), session, pages);

            int result = session.Options.ShowPrintDialog
                ? PrintingToolsInterop.RunModalPrintOperation(operation.DangerousGetHandle())
                : PrintingToolsInterop.CommitPrint(operation.DangerousGetHandle());

            if (result == 0)
            {
                if (session.Options.ShowPrintDialog)
                {
                    session.NotifyJobEvent(PrintJobEventKind.Cancelled, "macOS print job was cancelled during modal execution.");
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }

                EmitDiagnostic("macOS print job did not complete successfully during submission.", context: new { session.Description });
                session.NotifyJobEvent(PrintJobEventKind.Failed, "macOS print job did not complete successfully.");
                throw new InvalidOperationException("macOS print job did not complete successfully.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        finally
        {
            PrintingToolsInterop.ManagedCallbacks.FreeNative(nativeCallbacks);
            if (contextHandle.IsAllocated)
            {
                contextHandle.Free();
            }
        }
    }

    private static void EnsureSupported()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("The macOS print adapter can only be used on macOS.");
        }
    }

    private static async Task<string?> TryGetDefaultPrinterAsync(CancellationToken cancellationToken)
    {
        var result = await RunCupsCommandAsync("lpstat", new[] { "-d" }, cancellationToken).ConfigureAwait(false);
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
            return raw[Prefix.Length..].Trim();
        }

        var colonIndex = raw.LastIndexOf(':');
        return colonIndex >= 0 ? raw[(colonIndex + 1)..].Trim() : raw;
    }

    private static async Task<CommandResult> RunCupsCommandAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ArgumentException("Executable must be provided.", nameof(executable));
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            if (arguments is { Count: > 0 })
            {
                foreach (var argument in arguments)
                {
                    if (!string.IsNullOrWhiteSpace(argument))
                    {
                        startInfo.ArgumentList.Add(argument);
                    }
                }
            }

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using var registration = cancellationToken.Register(static state =>
            {
                if (state is Process proc)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Suppress failures that occur while terminating the process.
                    }
                }
            }, process);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stdout.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderr.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString(), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, $"Failed to run '{executable}'.", ex, new { arguments = arguments.ToArray() });
            return new CommandResult(-1, string.Empty, ex.Message, ex);
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

    private static string MapDuplexOption(string value) => value.ToLowerInvariant() switch
    {
        "duplexnotumble" => "two-sided-long-edge",
        "duplextumble" => "two-sided-short-edge",
        "two-sided-short-edge" => "two-sided-short-edge",
        "two-sided-long-edge" => "two-sided-long-edge",
        _ => "one-sided"
    };

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

    private readonly record struct MediaDescriptor(PageMediaSize Size, bool IsDefault, IReadOnlyDictionary<string, string> Metadata);

    private readonly record struct LpOptionChoice(string RawValue, string Value, bool IsDefault);

    private readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError, Exception? Exception)
    {
        public bool IsSuccess => Exception is null && ExitCode == 0;
    }


    private static readonly RenderPageCallback RenderPageHandler = RenderPage;
    private static readonly GetPageCountCallback GetPageCountHandler = GetPageCount;
    private static readonly DiagnosticCallback DiagnosticHandler = HandleNativeDiagnostic;
    private static readonly JobEventCallback JobEventHandler = HandleJobEvent;

    private static readonly IntPtr RenderPageDelegate = Marshal.GetFunctionPointerForDelegate(RenderPageHandler);
    private static readonly IntPtr GetPageCountDelegate = Marshal.GetFunctionPointerForDelegate(GetPageCountHandler);
    private static readonly IntPtr LogDiagnosticDelegate = Marshal.GetFunctionPointerForDelegate(DiagnosticHandler);
    private static readonly IntPtr JobEventDelegate = Marshal.GetFunctionPointerForDelegate(JobEventHandler);

    private delegate ulong RenderPageCallback(IntPtr context, IntPtr cgContext, ulong pageIndex);
    private delegate ulong GetPageCountCallback(IntPtr context);
    private delegate void JobEventCallback(IntPtr context, int eventId, IntPtr message, int messageLength, int errorCode);
    private delegate void DiagnosticCallback(IntPtr context, IntPtr message, int length);

    private static ulong RenderPage(IntPtr context, IntPtr cgContext, ulong pageIndex)
    {
        if (context == IntPtr.Zero || cgContext == IntPtr.Zero)
        {
            return 0;
        }

        var handle = GCHandle.FromIntPtr(context);
        if (!handle.IsAllocated || handle.Target is not PrintContext printContext)
        {
            return 0;
        }

        if (pageIndex >= (ulong)printContext.Pages.Count)
        {
            return 0;
        }

        var page = printContext.Pages[(int)pageIndex];
        var controlTag = (page.Visual as Control)?.Tag ?? "<null>";
        EmitTrace(
            $"RenderPage index={pageIndex}",
            new
            {
                PageIndex = pageIndex,
                Tag = controlTag
            });
#if DEBUG
        var originalMetrics = page.Metrics;
        System.Diagnostics.Debug.WriteLine($"[PrintingTools] RenderPage index={pageIndex} offset={originalMetrics?.ContentOffset ?? new Avalonia.Point()} visualBounds={originalMetrics?.VisualBounds}");
#endif
        var metrics = page.Metrics ?? PrintPageMetrics.Create(page.Visual, page.Settings, printContext.TargetDpi);
        if (EnableRenderDiagnostics)
        {
            LogRenderDiagnostics(page, metrics);
        }

        return TryRenderPageToContext(page, metrics, cgContext, printContext.TargetDpi, printContext.ColorSpace) ? 1UL : 0UL;
    }

    private static ulong GetPageCount(IntPtr context)
    {
        if (context == IntPtr.Zero)
        {
            return 0;
        }

        var handle = GCHandle.FromIntPtr(context);
        if (!handle.IsAllocated || handle.Target is not PrintContext printContext)
        {
            return 0;
        }

        return (ulong)printContext.Pages.Count;
    }

    private static void HandleNativeDiagnostic(IntPtr context, IntPtr messagePtr, int length)
    {
        try
        {
            string? message = null;
            if (messagePtr != IntPtr.Zero && length > 0)
            {
                message = Marshal.PtrToStringUni(messagePtr, length);
            }

            object? diagContext = null;
            if (context != IntPtr.Zero)
            {
                var handle = GCHandle.FromIntPtr(context);
                if (handle.IsAllocated && handle.Target is PrintContext printContext)
                {
                    diagContext = new
                    {
                        PageCount = printContext.Pages.Count,
                        Description = printContext.Session.Description
                    };
                }
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                EmitDiagnostic("AppKit emitted an empty diagnostic message.", context: diagContext);
                return;
            }

            EmitDiagnostic($"AppKit: {message}", context: diagContext);
        }
        catch (Exception ex)
        {
            EmitDiagnostic("Failed to process AppKit diagnostic callback.", ex);
        }
    }

    private static void HandleJobEvent(IntPtr context, int eventId, IntPtr messagePtr, int messageLength, int errorCode)
    {
        var message = messagePtr != IntPtr.Zero && messageLength > 0
            ? Marshal.PtrToStringUni(messagePtr, messageLength)
            : null;

        if (context == IntPtr.Zero)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr(context);
        if (!handle.IsAllocated || handle.Target is not PrintContext printContext)
        {
            return;
        }

        var session = printContext.Session;
        var kind = eventId switch
        {
            JobEventWillRun => PrintJobEventKind.Started,
            JobEventCompleted => PrintJobEventKind.Completed,
            JobEventFailed => PrintJobEventKind.Failed,
            JobEventCancelled => PrintJobEventKind.Cancelled,
            _ => PrintJobEventKind.Unknown
        };

        Exception? exception = null;
        if (kind == PrintJobEventKind.Failed)
        {
            var details = message ?? "macOS print job failed.";
            exception = new InvalidOperationException($"{details} (code {errorCode})");
        }

        session.NotifyJobEvent(kind, message, exception);
    }

    private static bool TryRenderPageToContext(PrintPage page, PrintPageMetrics metrics, IntPtr cgContext, Vector targetDpi, int colorSpace)
    {
        try
        {
            using var renderTarget = PrintPageRenderer.RenderToBitmap(page, metrics);
            return TryBlitToContext(renderTarget, metrics, cgContext, colorSpace);
        }
        catch (Exception ex)
        {
            EmitDiagnostic(
                "RenderPageToContext failed.",
                ex,
                new
                {
                    VisualTag = (page.Visual as Control)?.Tag,
                    Metrics = page.Metrics,
                    TargetDpi = targetDpi
                });
            return false;
        }
    }

    private static bool TryBlitToContext(RenderTargetBitmap bitmap, PrintPageMetrics metrics, IntPtr cgContext, int colorSpace)
    {
        if (cgContext == IntPtr.Zero)
        {
            return false;
        }

        var pixelSize = bitmap.PixelSize;
        if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
        {
            return false;
        }

        var format = bitmap.Format ?? PixelFormats.Bgra8888;
        var pixelFormatCode = GetPixelFormatCode(format);
        if (pixelFormatCode < 0)
        {
            return false;
        }

        var destWidth = DipToPoints(metrics.PageSize.Width);
        var destHeight = DipToPoints(metrics.PageSize.Height);
        if (destWidth <= 0 || destHeight <= 0)
        {
            return false;
        }

        var stride = pixelSize.Width * BytesPerPixel;
        var bufferSize = stride * pixelSize.Height;
        var buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            bitmap.CopyPixels(new PixelRect(pixelSize), buffer, bufferSize, stride);

            PrintingToolsInterop.DrawBitmap(
                cgContext,
                buffer,
                pixelSize.Width,
                pixelSize.Height,
                stride,
                0,
                0,
                destWidth,
                destHeight,
                pixelFormatCode,
                colorSpace);

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static double DipToPoints(double value) => value * PointsPerInch / DipsPerInch;
    private static double PointsToDip(double value) => value * DipsPerInch / PointsPerInch;

    private readonly struct NativeString : IDisposable
    {
        public NativeString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Pointer = IntPtr.Zero;
                Length = 0;
            }
            else
            {
                Pointer = Marshal.StringToHGlobalUni(value);
                Length = value.Length;
            }
        }

        public IntPtr Pointer { get; }

        public int Length { get; }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Pointer);
            }
        }

    }

    private static int GetPixelFormatCode(PixelFormat format)
    {
        if (format == PixelFormats.Bgra8888)
        {
            return PixelFormatBgra8888;
        }

        if (format == PixelFormats.Rgba8888)
        {
            return PixelFormatRgba8888;
        }

        return -1;
    }

    private static void LogRenderDiagnostics(PrintPage page, PrintPageMetrics metrics)
    {
        try
        {
            var metadata = VisualRenderAudit.Collect(page.Visual);
            Debug.WriteLine($"[PrintingTools] Page metrics: Size={metrics.PageSize}, ContentRect={metrics.ContentRect}, Offset={metrics.ContentOffset}");
            foreach (var entry in metadata)
            {
                var visualName = string.IsNullOrWhiteSpace(entry.Name) ? "<unnamed>" : entry.Name;
                Debug.WriteLine(
                    $"[PrintingTools] Visual={entry.TypeName} Name={visualName} Bounds={entry.LocalBounds} World={entry.WorldBounds} Children={entry.ChildCount} Opacity={entry.Opacity:0.###} ClipToBounds={entry.ClipToBounds} HasClip={entry.HasGeometryClip} HasOpacityMask={entry.HasOpacityMask}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PrintingTools] Render diagnostics failed: {ex.Message}");
        }
    }

    private sealed class PrintContext
    {
        public PrintContext(PrintSession session, IReadOnlyList<PrintPage> pages, Vector targetDpi, int colorSpace)
        {
            Session = session;
            Pages = pages;
            TargetDpi = targetDpi;
            ColorSpace = colorSpace;
        }

        public PrintSession Session { get; }

        public IReadOnlyList<PrintPage> Pages { get; }

        public Vector TargetDpi { get; }

        public int ColorSpace { get; }
    }
}

public sealed class MacPrintAdapterFactory
{
    public bool IsSupported { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public IPrintAdapter? CreateAdapter()
    {
        return IsSupported ? new MacPrintAdapter() : null;
    }
}
