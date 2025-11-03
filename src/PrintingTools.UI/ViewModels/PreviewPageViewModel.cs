using System;
using Avalonia;
using PrintingTools.Core;

namespace PrintingTools.UI.ViewModels;

public sealed class PreviewPageViewModel
{
    public PreviewPageViewModel(int number, PrintPage page)
    {
        Number = number;
        Page = page ?? throw new ArgumentNullException(nameof(page));
    }

    public int Number { get; }

    public PrintPage Page { get; }

    public Size PageSize => Page.Metrics?.PageSize ?? Page.Visual.Bounds.Size;

    public Thickness PrintableMargins
    {
        get
        {
            var metrics = Page.Metrics;
            if (metrics is null)
            {
                return new Thickness();
            }

            var pageSize = metrics.PageSize;
            var contentRect = metrics.ContentRect;
            var right = Math.Max(0, pageSize.Width - contentRect.Right);
            var bottom = Math.Max(0, pageSize.Height - contentRect.Bottom);

            return new Thickness(
                contentRect.X,
                contentRect.Y,
                right,
                bottom);
        }
    }

    public string Summary =>
        Page.Metrics is { } metrics
            ? $"Paper {metrics.PageSize.Width:0}×{metrics.PageSize.Height:0} DIP · Margins {PrintableMargins.Left:0}/{PrintableMargins.Top:0}/{PrintableMargins.Right:0}/{PrintableMargins.Bottom:0}"
            : "Metrics unavailable";
}
