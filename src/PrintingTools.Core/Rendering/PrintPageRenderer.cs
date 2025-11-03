using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace PrintingTools.Core.Rendering;

/// <summary>
/// Provides utility helpers for rendering <see cref="PrintPage"/> content either to a drawing context or bitmap surface.
/// </summary>
public static class PrintPageRenderer
{
    /// <summary>
    /// Renders the supplied page to the provided drawing context using the supplied metrics.
    /// </summary>
    public static void RenderToDrawingContext(DrawingContext drawingContext, PrintPage page, PrintPageMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(metrics);

        drawingContext.FillRectangle(Brushes.White, new Rect(metrics.PageSize));

        if (metrics.ContentRect.Width <= 0 || metrics.ContentRect.Height <= 0)
        {
            return;
        }

        using (drawingContext.PushTransform(Matrix.CreateTranslation(metrics.ContentRect.X, metrics.ContentRect.Y)))
        using (drawingContext.PushClip(new Rect(metrics.ContentRect.Size)))
        using (drawingContext.PushTransform(Matrix.CreateScale(metrics.ContentScale, metrics.ContentScale)))
        using (drawingContext.PushTransform(Matrix.CreateTranslation(-metrics.ContentOffset.X, -metrics.ContentOffset.Y)))
        using (drawingContext.PushTransform(Matrix.CreateTranslation(-metrics.VisualBounds.X, -metrics.VisualBounds.Y)))
        {
            RenderVisualTree(drawingContext, page.Visual);
        }
    }

    /// <summary>
    /// Renders the supplied page into a <see cref="RenderTargetBitmap"/> using the provided metrics.
    /// </summary>
    public static RenderTargetBitmap RenderToBitmap(PrintPage page, PrintPageMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(metrics);

        var bitmap = new RenderTargetBitmap(metrics.PagePixelSize, metrics.Dpi);
        using (var drawingContext = bitmap.CreateDrawingContext(true))
        {
            RenderToDrawingContext(drawingContext, page, metrics);
        }

        return bitmap;
    }

    private static void RenderVisualTree(DrawingContext context, Visual visual)
    {
        if (!visual.IsVisible || visual.Opacity <= 0)
        {
            return;
        }

        var bounds = visual.Bounds;
        var translation = Matrix.CreateTranslation(bounds.Position);
        var renderTransformMatrix = Matrix.Identity;

        if (visual.HasMirrorTransform)
        {
            var mirrorMatrix = new Matrix(-1.0, 0.0, 0.0, 1.0, bounds.Width, 0);
            renderTransformMatrix = mirrorMatrix * renderTransformMatrix;
        }

        if (visual.RenderTransform is { } renderTransform)
        {
            var origin = visual.RenderTransformOrigin.ToPixels(bounds.Size);
            var offset = Matrix.CreateTranslation(origin);
            var finalTransform = (-offset) * renderTransform.Value * offset;
            renderTransformMatrix = finalTransform * renderTransformMatrix;
        }

        var combinedTransform = renderTransformMatrix * translation;

        using var transformState = context.PushTransform(combinedTransform);
        using var opacityState = context.PushOpacity(visual.Opacity);
        using var clipBoundsState = visual.ClipToBounds ? context.PushClip(new Rect(bounds.Size)) : default;
        using var clipGeometryState = visual.Clip is { } clipGeometry ? context.PushGeometryClip(clipGeometry) : default;
        using var opacityMaskState = visual.OpacityMask is { } opacityMask ? context.PushOpacityMask(opacityMask, new Rect(bounds.Size)) : default;

        visual.Render(context);

        foreach (var child in visual.GetVisualChildren())
        {
            RenderVisualTree(context, child);
        }
    }
}
