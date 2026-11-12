using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia.Helpers;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;
using SkiaSharp;

namespace PrintingTools.Windows.Rendering;

internal sealed class SkiaXpsExporter
{
    private const double DipsPerInch = 96d;
    private const double PointsPerInch = 72d;
    private const string DiagnosticsCategory = "SkiaXpsExporter";

    public void ExportXps(string path, IReadOnlyList<PrintPage> pages, IReadOnlyList<RenderTargetBitmap>? bitmaps = null)
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
            $"Exporting XPS to '{path}'.",
            context: new { PageCount = pages.Count });

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var document = SKDocument.CreateXps(stream) ?? throw new InvalidOperationException("Unable to create XPS document via Skia.");

        RenderDocumentOnRequiredThread(document, pages, bitmaps);
        document.Close();
    }

    public byte[] CreateXpsBytes(IReadOnlyList<PrintPage> pages, IReadOnlyList<RenderTargetBitmap>? bitmaps = null)
    {
        if (pages.Count == 0)
        {
            return Array.Empty<byte>();
        }

        using var wStream = new SKDynamicMemoryWStream();
        using (var document = SKDocument.CreateXps(wStream) ?? throw new InvalidOperationException("Unable to create XPS document via Skia."))
        {
            RenderDocumentOnRequiredThread(document, pages, bitmaps);
            document.Close();
        }

        using var data = wStream.DetachAsData();
        return data?.ToArray() ?? Array.Empty<byte>();
    }

    private static void RenderDocumentOnRequiredThread(SKDocument document, IReadOnlyList<PrintPage> pages, IReadOnlyList<RenderTargetBitmap>? bitmaps)
    {
        if (bitmaps is null)
        {
            AvaloniaDispatcherHelper.Invoke(() => RenderDocument(document, pages, bitmaps));
            return;
        }

        RenderDocument(document, pages, bitmaps);
    }

    private static void RenderDocument(SKDocument document, IReadOnlyList<PrintPage> pages, IReadOnlyList<RenderTargetBitmap>? bitmaps)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            RenderPage(document, pages[i], bitmaps != null && i < bitmaps.Count ? bitmaps[i] : null, i);
        }
    }

    private static void RenderPage(SKDocument document, PrintPage page, RenderTargetBitmap? bitmap, int index)
    {
        var metrics = page.Metrics ?? PrintPageMetrics.Create(page.Visual, page.Settings);

        var width = (float)(metrics.PageSize.Width * PointsPerInch / DipsPerInch);
        var height = (float)(metrics.PageSize.Height * PointsPerInch / DipsPerInch);

        using var canvas = document.BeginPage(width, height);
        canvas.Clear(SKColors.White);

        var tag = (page.Visual as Control)?.Tag;

        if (bitmap is not null)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            var buffer = ms.ToArray();
            using var skData = SKData.CreateCopy(buffer);
            using var skImage = SKImage.FromEncodedData(skData);

            if (skImage is not null)
            {
                var destRect = new SKRect(
                    (float)(metrics.ContentRect.X * PointsPerInch / DipsPerInch),
                    (float)(metrics.ContentRect.Y * PointsPerInch / DipsPerInch),
                    (float)((metrics.ContentRect.X + metrics.ContentRect.Width) * PointsPerInch / DipsPerInch),
                    (float)((metrics.ContentRect.Y + metrics.ContentRect.Height) * PointsPerInch / DipsPerInch));

                canvas.DrawImage(skImage, destRect);

                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    $"Rendering XPS page {index} via raster fallback.",
                    context: new { Index = index, Tag = tag });
            }
        }
        else
        {
            // Same as SkiaVectorPageRenderer: the page transforms and the DIP -> point conversion
            // both go on the DrawingContext PushTransform stack (the XPS canvas is point-based too,
            // BeginPage receives point sizes); a scale concatenated onto the canvas beforehand is
            // discarded when DrawingContextImpl rewrites the canvas matrix.
            var unitScale = PointsPerInch / DipsPerInch;
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                $"Rendering XPS page {index}",
                context: new
                {
                    Index = index,
                    Tag = (page.Visual as Control)?.Tag,
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
                // Without the wrapper the RenderAsync replay carries neither the page transforms nor
                // the unit conversion, so the output is oversized, but better than a blank page.
                PrintDiagnostics.Report(
                    DiagnosticsCategory,
                    "Skia canvas wrapper unavailable; rendered without page-level transforms.",
                    context: new { Index = index, Failure = wrapperFailure });
                DrawingContextHelper.RenderAsync(canvas, page.Visual, page.Visual.Bounds, new Vector(DipsPerInch, DipsPerInch)).GetAwaiter().GetResult();
            }
        }

        document.EndPage();
    }
}
