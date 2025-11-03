using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Platform;
using PrintingTools.Core;
using PrintingTools.Core.Pagination;
using PrintingTools.Core.Rendering;
using PrintingTools.MacOS.Native;
using PrintingTools.MacOS.Rendering;

namespace PrintingTools.MacOS;

/// <summary>
/// Placeholder macOS implementation that will bridge Avalonia visuals to AppKit printing APIs.
/// </summary>
public sealed class MacPrintAdapter : IPrintAdapter
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

    private static void EmitDiagnostic(string message, Exception? exception = null, object? context = null) =>
        PrintDiagnostics.Report(DiagnosticsCategory, message, exception, context);

    private static void EmitTrace(string message, object? context = null)
    {
        if (EnableRenderDiagnostics)
        {
            PrintDiagnostics.Report($"{DiagnosticsCategory}.Trace", message, null, context);
        }
    }

    public async Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        var pages = CollectPages(session, cancellationToken, TargetPrintDpi);

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

        if (TryRunVectorPreview(session.Options, pages))
        {
            return;
        }

        if (TryRunVectorPreview(session.Options, pages))
        {
            return;
        }

        if (session.Options.UseManagedPdfExporter)
        {
            HandleManagedPdfExport(session.Options, pages);
            return;
        }

        if (TryExportManagedPdfIfRequested(session, pages))
        {
            return;
        }

        if (TryCommitVectorPrint(session.Options, pages))
        {
            return;
        }

        await SubmitToNativePrintOperationAsync(session, pages, cancellationToken).ConfigureAwait(false);
    }

    public Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureSupported();

        var pages = CollectPages(session, cancellationToken, TargetPreviewDpi);

        if (EnableRenderDiagnostics)
        {
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metrics = EnsureMetrics(page, TargetPreviewDpi);
                LogRenderDiagnostics(page, metrics);
            }
        }

        byte[]? vectorDocument = null;
        if (session.Options.UseVectorRenderer)
        {
            vectorDocument = VectorRenderer.CreatePdfBytes(pages);
            if (vectorDocument.Length == 0)
            {
                vectorDocument = null;
            }
        }

        return Task.FromResult(new PrintPreviewModel(pages, vectorDocument: vectorDocument));
    }

    private static List<PrintPage> CollectPages(PrintSession session, CancellationToken cancellationToken, Vector desiredDpi)
    {
        var expandedPages = new List<PrintPage>();
        using var enumerator = session.Document.CreateEnumerator();

        while (enumerator.MoveNext(cancellationToken))
        {
            var normalized = NormalizePage(enumerator.Current, desiredDpi);
            foreach (var expanded in PrintPaginationUtilities.ExpandPage(normalized))
            {
                expandedPages.Add(expanded);
            }
        }

        var range = session.Options.PageRange;
        if (range is null)
        {
            return expandedPages;
        }

        var result = new List<PrintPage>();
        for (var i = 0; i < expandedPages.Count; i++)
        {
            var pageNumber = i + 1;
            if (IsWithinRange(pageNumber, range.Value))
            {
                result.Add(expandedPages[i]);
            }

            if (pageNumber > range.Value.EndPage)
            {
                break;
            }
        }

        return result;
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

        VectorRenderer.ExportPdf(options.PdfOutputPath!, pages);
        return true;
    }

    private static void HandleManagedPdfExport(PrintOptions options, IReadOnlyList<PrintPage> pages)
    {
        if (pages.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(options.PdfOutputPath))
        {
            VectorRenderer.ExportPdf(options.PdfOutputPath!, pages);
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

        var pdfBytes = VectorRenderer.CreatePdfBytes(pages);
        if (pdfBytes.Length == 0)
        {
            return false;
        }

        var vectorDocument = new PrintingToolsInterop.VectorDocument
        {
            Length = pdfBytes.Length,
            ShowPrintPanel = options.ShowPrintDialog ? 1 : 0
        };

        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
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

    private static bool TryCommitVectorPrint(PrintOptions options, IReadOnlyList<PrintPage> pages)
    {
        if (!options.UseVectorRenderer || options.ShowPrintDialog)
        {
            return false;
        }

        if (pages.Count == 0)
        {
            return false;
        }

        var pdfBytes = VectorRenderer.CreatePdfBytes(pages);
        if (pdfBytes.Length == 0)
        {
            return false;
        }

        var result = PrintingToolsInterop.RunPdfPrintOperation(pdfBytes, pdfBytes.Length, 0) != 0;
        EmitDiagnostic("Vector print dispatched via macOS bridge.", context: new { result });
        return result;
    }

    private static bool IsWithinRange(int index, PrintPageRange range) =>
        index >= range.StartPage && index <= range.EndPage;

    private static PrintPage NormalizePage(PrintPage page, Vector desiredDpi)
    {
        ArgumentNullException.ThrowIfNull(page);

        var metrics = EnsureMetrics(page, desiredDpi);
        if (ReferenceEquals(metrics, page.Metrics))
        {
            return page;
        }

        return new PrintPage(page.Visual, page.Settings, page.IsPageBreakAfter, metrics);
    }

    private static PrintPageMetrics EnsureMetrics(PrintPage page, Vector desiredDpi)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.Metrics is { } metrics && metrics.Dpi.NearlyEquals(desiredDpi))
        {
            return metrics;
        }

        return PrintPageMetrics.Create(page.Visual, page.Settings, desiredDpi);
    }

    private static void ConfigureNativeOperation(IntPtr operationHandle, PrintSession session, IReadOnlyList<PrintPage> pages)
    {
        if (operationHandle == IntPtr.Zero)
        {
            return;
        }

        var firstPageMetrics = pages.Count > 0 ? pages[0].Metrics : null;
        firstPageMetrics ??= pages.Count > 0 ? EnsureMetrics(pages[0], TargetPrintDpi) : null;

        var pageSize = firstPageMetrics?.PageSize ?? DefaultPageSize;
        var margins = firstPageMetrics?.Margins ?? DefaultMargins;

        var options = session.Options;

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

        var orientation = pageSize.Width > pageSize.Height ? 1 : 0;

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
            ShowPrintPanel = showPrintPanel ? 1 : 0,
            ShowProgressPanel = showProgressPanel,
            JobName = jobName.Pointer,
            JobNameLength = jobName.Length,
            PrinterName = printerName.Pointer,
            PrinterNameLength = printerName.Length,
            EnablePdfExport = enablePdfExport ? 1 : 0,
            PdfPath = pdfPathString.Pointer,
            PdfPathLength = pdfPathString.Length,
            PageCount = pages.Count
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

        var printContext = new PrintContext(pages);
        var contextHandle = GCHandle.Alloc(printContext);
        var callbacks = new PrintingToolsInterop.ManagedCallbacks
        {
            Context = GCHandle.ToIntPtr(contextHandle),
            RenderPage = RenderPageDelegate,
            GetPageCount = GetPageCountDelegate
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

            if (result == 0 && !session.Options.ShowPrintDialog)
            {
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


    private static readonly IntPtr RenderPageDelegate = Marshal.GetFunctionPointerForDelegate<RenderPageCallback>(RenderPage);
    private static readonly IntPtr GetPageCountDelegate = Marshal.GetFunctionPointerForDelegate<GetPageCountCallback>(GetPageCount);

    private delegate ulong RenderPageCallback(IntPtr context, IntPtr cgContext, ulong pageIndex);
    private delegate ulong GetPageCountCallback(IntPtr context);

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
        var metrics = page.Metrics;
        System.Diagnostics.Debug.WriteLine($"[PrintingTools] RenderPage index={pageIndex} offset={metrics?.ContentOffset ?? new Avalonia.Point()} visualBounds={metrics?.VisualBounds}");
#endif
        return TryRenderPageToContext(page, cgContext) ? 1UL : 0UL;
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

    private static bool TryRenderPageToContext(PrintPage page, IntPtr cgContext)
    {
        try
        {
            var metrics = EnsureMetrics(page, TargetPrintDpi);
            if (EnableRenderDiagnostics)
            {
                LogRenderDiagnostics(page, metrics);
            }

            using var renderTarget = PrintPageRenderer.RenderToBitmap(page, metrics);
            return TryBlitToContext(renderTarget, metrics, cgContext);
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
                    TargetDpi = TargetPrintDpi
                });
            return false;
        }
    }

    private static bool TryBlitToContext(RenderTargetBitmap bitmap, PrintPageMetrics metrics, IntPtr cgContext)
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
                pixelFormatCode);

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static double DipToPoints(double value) => value * PointsPerInch / DipsPerInch;

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
        public PrintContext(IReadOnlyList<PrintPage> pages)
        {
            Pages = pages;
        }

        public IReadOnlyList<PrintPage> Pages { get; }
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
