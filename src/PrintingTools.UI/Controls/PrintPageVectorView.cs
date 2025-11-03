using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;

namespace PrintingTools.UI.Controls;

/// <summary>
/// Displays a <see cref="PrintPage"/> using the shared vector renderer.
/// </summary>
public class PrintPageVectorView : Control
{
    public static readonly StyledProperty<PrintPage?> PrintPageProperty =
        AvaloniaProperty.Register<PrintPageVectorView, PrintPage?>(nameof(PrintPage));

    static PrintPageVectorView()
    {
        AffectsRender<PrintPageVectorView>(PrintPageProperty);
        AffectsMeasure<PrintPageVectorView>(PrintPageProperty);
    }

    public PrintPage? PrintPage
    {
        get => GetValue(PrintPageProperty);
        set => SetValue(PrintPageProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (PrintPage is { } page)
        {
            var metrics = EnsureMetrics(page);
            return metrics.PageSize;
        }

        return default;
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (PrintPage is not { } page)
        {
            return;
        }

        var metrics = EnsureMetrics(page);
        PrintPageRenderer.RenderToDrawingContext(context, page, metrics);
    }

    private static PrintPageMetrics EnsureMetrics(PrintPage page) =>
        page.Metrics ?? PrintPageMetrics.Create(page.Visual, page.Settings);
}
