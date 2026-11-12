using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia.Helpers;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;
using SkiaSharp;

namespace PrintingTools.Windows.Rendering;

internal sealed class SkiaVectorPageRenderer : IVectorPageRenderer
{
    private const double DipsPerInch = 96d;
    private const double PointsPerInch = 72d;
    private const string DiagnosticsCategory = "SkiaVectorPageRenderer";

    public void ExportPdf(string path, IReadOnlyList<PrintPage> pages)
    {
        if (pages.Count == 0)
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        PrintDiagnostics.Report(
            DiagnosticsCategory,
            $"Exporting managed PDF to '{path}'.",
            context: new { PageCount = pages.Count });

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var document = SKDocument.CreatePdf(stream) ?? throw new InvalidOperationException("Unable to create PDF document via Skia.");

        AvaloniaDispatcherHelper.Invoke(() => RenderDocument(document, pages));
        document.Close();
    }

    public byte[] CreatePdfBytes(IReadOnlyList<PrintPage> pages)
    {
        if (pages.Count == 0)
        {
            return Array.Empty<byte>();
        }

        using var wStream = new SKDynamicMemoryWStream();
        using (var document = SKDocument.CreatePdf(wStream) ?? throw new InvalidOperationException("Unable to create PDF document via Skia."))
        {
            AvaloniaDispatcherHelper.Invoke(() => RenderDocument(document, pages));
            document.Close();
        }

        using var data = wStream.DetachAsData();
        return data?.ToArray() ?? Array.Empty<byte>();
    }

    private static void RenderDocument(SKDocument document, IReadOnlyList<PrintPage> pages)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            RenderPage(document, pages[i], i);
        }
    }

    private static void RenderPage(SKDocument document, PrintPage page, int index)
    {
        var metrics = page.Metrics ?? PrintPageMetrics.Create(page.Visual, page.Settings);

        var width = (float)(metrics.PageSize.Width * PointsPerInch / DipsPerInch);
        var height = (float)(metrics.PageSize.Height * PointsPerInch / DipsPerInch);

        using var canvas = document.BeginPage(width, height);
        canvas.Clear(SKColors.White);

        // Page-level transforms take the same path as the preview: the canvas is wrapped into a
        // DrawingContext and handed to PrintPageRenderer.RenderToDrawingContext, so translation,
        // scale and slice offset all travel on the PushTransform stack. The DIP -> point conversion
        // has to ride on that same stack: DrawingContextImpl rewrites the canvas matrix from its own
        // transform stack on every draw, so a scale concatenated onto the canvas beforehand is
        // discarded, and WrapSkiaCanvas only records dpi into CreateInfo.Dpi (consumed by
        // intermediate surfaces) without setting ScaleDrawingToDpi. Without the conversion content
        // lands on the point-based page at 1 DIP = 1 point, i.e. 133% oversized, and its right and
        // bottom edges are pushed off the media box.
        var tag = (page.Visual as Control)?.Tag;
        var unitScale = PointsPerInch / DipsPerInch;
        PrintDiagnostics.Report(
            DiagnosticsCategory,
            $"Rendering PDF page {index}",
            context: new
            {
                Index = index,
                Tag = tag,
                UnitScale = unitScale,
                metrics.PageSize,
                metrics.ContentRect,
                metrics.VisualBounds,
                metrics.ContentOffset,
                metrics.ContentScale
            });

        if (SkiaCanvasDrawingContext.TryCreate(canvas, new Vector(DipsPerInch, DipsPerInch), out var drawingContext, out var wrapperFailure))
        {
            using (drawingContext)
            using (drawingContext!.PushTransform(Matrix.CreateScale(unitScale, unitScale)))
            {
                PrintPageRenderer.RenderToDrawingContext(drawingContext!, page, metrics);
            }
        }
        else
        {
            // Without the wrapper the page falls back to a direct RenderAsync replay, which carries
            // neither the page transforms nor the unit conversion, so the output is oversized but
            // still better than a blank page. The diagnostic records the reflection failure so it can
            // be re-checked when Avalonia is upgraded.
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                "Skia canvas wrapper unavailable; rendered without page-level transforms.",
                context: new { Index = index, Failure = wrapperFailure });
            DrawingContextHelper.RenderAsync(canvas, page.Visual, page.Visual.Bounds, new Vector(DipsPerInch, DipsPerInch)).GetAwaiter().GetResult();
        }

        document.EndPage();
    }
}
