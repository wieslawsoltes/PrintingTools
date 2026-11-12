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

        return AvaloniaDispatcherHelper.Invoke(() =>
        {
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

            return (IReadOnlyList<PrintPage>)normalized;
        }, cancellationToken);
    }

    public static IReadOnlyList<RenderTargetBitmap> RenderBitmaps(IReadOnlyList<PrintPage> pages, Vector targetDpi, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);

        return AvaloniaDispatcherHelper.Invoke(() =>
        {
            var results = new List<RenderTargetBitmap>(pages.Count);
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var metrics = EnsureMetrics(page, targetDpi);
                results.Add(PrintPageRenderer.RenderToBitmap(page, metrics));
            }

            return (IReadOnlyList<RenderTargetBitmap>)results;
        }, cancellationToken);
    }

    public static byte[]? TryCreateVectorDocument(IReadOnlyList<PrintPage> pages, IVectorPageRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(renderer);

        if (pages.Count == 0)
        {
            return null;
        }

        var bytes = AvaloniaDispatcherHelper.Invoke(() => renderer.CreatePdfBytes(pages));
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
        var existing = page.Metrics;
        if (existing is not null && NearlyEquals(existing.Dpi, targetDpi))
        {
            return existing;
        }

        var recreated = PrintPageMetrics.Create(page.Visual, page.Settings, targetDpi);

        // Pagination slices record where in the content a page starts drawing via ContentOffset.
        // Recreating the metrics for a different DPI zeroes it, so every split page would render the
        // same full content; the offset has to be carried over to the recreated metrics.
        if (existing is not null && !existing.ContentOffset.Equals(default(Point)))
        {
            recreated = recreated.WithContentOffset(existing.ContentOffset);
        }

        return recreated;
    }

    private static bool NearlyEquals(Vector left, Vector right, double tolerance = 0.1)
    {
        return Math.Abs(left.X - right.X) <= tolerance && Math.Abs(left.Y - right.Y) <= tolerance;
    }
}
