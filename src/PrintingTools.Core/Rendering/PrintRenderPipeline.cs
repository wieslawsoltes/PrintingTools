using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using PrintingTools.Core.Pagination;

namespace PrintingTools.Core.Rendering;

/// <summary>
/// Provides reusable helpers for collecting paginated content and rendering it to various targets.
/// </summary>
public static class PrintRenderPipeline
{
    public static IReadOnlyList<PrintPage> CollectPages(PrintSession session, Vector targetDpi, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var pages = session.Paginate(cancellationToken);
        if (pages.Count == 0)
        {
            return pages;
        }

        var laidOut = PrintPaginationUtilities.ApplyAdvancedLayout(pages, session.Options);
        var normalized = new List<PrintPage>(laidOut.Count);
        foreach (var page in laidOut)
        {
            cancellationToken.ThrowIfCancellationRequested();
            normalized.Add(NormalizePage(page, targetDpi));
        }

        return normalized;
    }

    public static IReadOnlyList<RenderTargetBitmap> RenderBitmaps(IReadOnlyList<PrintPage> pages, Vector targetDpi, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var results = new List<RenderTargetBitmap>(pages.Count);
        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metrics = EnsureMetrics(page, targetDpi);
            results.Add(PrintPageRenderer.RenderToBitmap(page, metrics));
        }

        return results;
    }

    public static byte[]? TryCreateVectorDocument(IReadOnlyList<PrintPage> pages, IVectorPageRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(renderer);

        if (pages.Count == 0)
        {
            return null;
        }

        var bytes = renderer.CreatePdfBytes(pages);
        return bytes.Length == 0 ? null : bytes;
    }

    private static PrintPage NormalizePage(PrintPage page, Vector targetDpi)
    {
        var metrics = EnsureMetrics(page, targetDpi);
        if (ReferenceEquals(metrics, page.Metrics))
        {
            return page;
        }

        return new PrintPage(page.Visual, page.Settings, page.IsPageBreakAfter, metrics);
    }

    private static PrintPageMetrics EnsureMetrics(PrintPage page, Vector targetDpi)
    {
        if (page.Metrics is { } metrics && NearlyEquals(metrics.Dpi, targetDpi))
        {
            return metrics;
        }

        return PrintPageMetrics.Create(page.Visual, page.Settings, targetDpi);
    }

    private static bool NearlyEquals(Vector left, Vector right, double tolerance = 0.1)
    {
        return Math.Abs(left.X - right.X) <= tolerance && Math.Abs(left.Y - right.Y) <= tolerance;
    }
}
