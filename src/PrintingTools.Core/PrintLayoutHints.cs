using Avalonia;
using Avalonia.Media;

namespace PrintingTools.Core;

/// <summary>
/// Provides attached properties that describe how Avalonia visuals should participate in printing.
/// </summary>
public class PrintLayoutHints
{
    private PrintLayoutHints()
    {
    }

    public static readonly AttachedProperty<bool> IsPrintableProperty =
        AvaloniaProperty.RegisterAttached<PrintLayoutHints, Visual, bool>(
            "IsPrintable", true, inherits: true);

    public static readonly AttachedProperty<Thickness?> MarginsProperty =
        AvaloniaProperty.RegisterAttached<PrintLayoutHints, Visual, Thickness?>(
            "Margins", inherits: true);

    public static readonly AttachedProperty<double?> ScaleProperty =
        AvaloniaProperty.RegisterAttached<PrintLayoutHints, Visual, double?>(
            "Scale", inherits: true);

    public static readonly AttachedProperty<bool> IsPageBreakAfterProperty =
        AvaloniaProperty.RegisterAttached<PrintLayoutHints, Visual, bool>(
            "IsPageBreakAfter", false, inherits: false);

    public static readonly AttachedProperty<Size?> TargetPageSizeProperty =
        AvaloniaProperty.RegisterAttached<PrintLayoutHints, Visual, Size?>(
            "TargetPageSize", inherits: true);

    public static readonly AttachedProperty<Rect?> SelectionBoundsProperty =
        AvaloniaProperty.RegisterAttached<PrintLayoutHints, Visual, Rect?>(
            "SelectionBounds", inherits: true);

    public static bool GetIsPrintable(Visual element) =>
        element.GetValue(IsPrintableProperty);

    public static void SetIsPrintable(Visual element, bool value) =>
        element.SetValue(IsPrintableProperty, value);

    public static Thickness? GetMargins(Visual element) =>
        element.GetValue(MarginsProperty);

    public static void SetMargins(Visual element, Thickness? value) =>
        element.SetValue(MarginsProperty, value);

    public static double? GetScale(Visual element) =>
        element.GetValue(ScaleProperty);

    public static void SetScale(Visual element, double? value) =>
        element.SetValue(ScaleProperty, value);

    public static bool GetIsPageBreakAfter(Visual element) =>
        element.GetValue(IsPageBreakAfterProperty);

    public static void SetIsPageBreakAfter(Visual element, bool value) =>
        element.SetValue(IsPageBreakAfterProperty, value);

    public static Size? GetTargetPageSize(Visual element) =>
        element.GetValue(TargetPageSizeProperty);

    public static void SetTargetPageSize(Visual element, Size? value) =>
        element.SetValue(TargetPageSizeProperty, value);

    public static Rect? GetSelectionBounds(Visual element) =>
        element.GetValue(SelectionBoundsProperty);

    public static void SetSelectionBounds(Visual element, Rect? value) =>
        element.SetValue(SelectionBoundsProperty, value);

    /// <summary>
    /// Creates <see cref="PrintPageSettings"/> from the attached properties on the supplied visual.
    /// </summary>
    public static PrintPageSettings CreateSettings(Visual visual, PrintPageSettings fallback)
    {
        var margins = GetMargins(visual);
        var scale = GetScale(visual);
        var targetSize = GetTargetPageSize(visual);

        var selectionBounds = GetSelectionBounds(visual);

        if (margins is null && scale is null && targetSize is null && selectionBounds is null)
        {
            return fallback;
        }

        return new PrintPageSettings
        {
            Margins = margins ?? fallback.Margins,
            Scale = scale ?? fallback.Scale,
            TargetSize = targetSize ?? fallback.TargetSize,
            SelectionBounds = selectionBounds ?? fallback.SelectionBounds
        };
    }
}
