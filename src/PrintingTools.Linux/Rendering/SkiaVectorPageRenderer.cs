using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Skia.Helpers;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;
using SkiaSharp;

namespace PrintingTools.Linux.Rendering;

internal sealed class SkiaVectorPageRenderer : IVectorPageRenderer
{
    private const double DipsPerInch = 96d;
    private const double PointsPerInch = 72d;
    private const string DiagnosticsCategory = "Linux.SkiaVectorPageRenderer";

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

        RenderDocument(document, pages);
        document.Close();
    }

    public byte[] CreatePdfBytes(IReadOnlyList<PrintPage> pages)
    {
        if (pages.Count == 0)
        {
            return Array.Empty<byte>();
        }

        using var memoryStream = new SKDynamicMemoryWStream();
        using (var document = SKDocument.CreatePdf(memoryStream) ?? throw new InvalidOperationException("Unable to create PDF document via Skia."))
        {
            RenderDocument(document, pages);
            document.Close();
        }

        using var data = memoryStream.DetachAsData();
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

        var dipsToPoints = (float)(PointsPerInch / DipsPerInch);
        canvas.Scale(dipsToPoints);
        canvas.Translate((float)metrics.ContentRect.X, (float)metrics.ContentRect.Y);
        canvas.Scale((float)metrics.ContentScale);
        canvas.Translate(
            (float)(-metrics.ContentOffset.X - metrics.VisualBounds.X),
            (float)(-metrics.ContentOffset.Y - metrics.VisualBounds.Y));

        var tag = (page.Visual as Control)?.Tag;
        PrintDiagnostics.Report(
            DiagnosticsCategory,
            $"Rendering PDF page {index}",
            context: new { Index = index, Tag = tag });

        DrawingContextHelper.RenderAsync(canvas, page.Visual, page.Visual.Bounds, metrics.Dpi).GetAwaiter().GetResult();

        document.EndPage();
    }
}
