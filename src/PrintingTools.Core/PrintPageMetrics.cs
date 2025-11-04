using System;
using Avalonia;

namespace PrintingTools.Core;

/// <summary>
/// Captures derived page layout information computed during pagination so platform adapters
/// can size their native drawing surfaces without duplicating measurement logic.
/// </summary>
public sealed class PrintPageMetrics
{
    private const double DefaultLogicalDpi = 96d;

    public PrintPageMetrics(
        Size pageSize,
        Thickness margins,
        Rect contentRect,
        double contentScale,
        Vector dpi,
        PixelSize pagePixelSize,
        PixelRect contentPixelRect,
        Rect visualBounds,
        Point contentOffset)
    {
        if (pageSize.Width <= 0 || pageSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        PageSize = pageSize;
        Margins = margins;
        ContentRect = contentRect;
        ContentScale = contentScale;
        Dpi = dpi;
        PagePixelSize = pagePixelSize;
        ContentPixelRect = contentPixelRect;
        VisualBounds = visualBounds;
        ContentOffset = contentOffset;
    }

    /// <summary>
    /// Gets the target page size in device-independent units (1/96th inch).
    /// </summary>
    public Size PageSize { get; }

    /// <summary>
    /// Gets the margins applied to the page in device-independent units.
    /// </summary>
    public Thickness Margins { get; }

    /// <summary>
    /// Gets the rectangle available for content after margins are applied (device-independent units).
    /// </summary>
    public Rect ContentRect { get; }

    /// <summary>
    /// Gets the scale factor that should be applied to the visual when rendering.
    /// </summary>
    public double ContentScale { get; }

    /// <summary>
    /// Gets the logical DPI the paginator used when computing pixel sizes.
    /// </summary>
    public Vector Dpi { get; }

    /// <summary>
    /// Gets the total page size expressed in device pixels.
    /// </summary>
    public PixelSize PagePixelSize { get; }

    /// <summary>
    /// Gets the pixel-space rectangle that represents the printable content area.
    /// </summary>
    public PixelRect ContentPixelRect { get; }

    /// <summary>
    /// Gets the bounds reported by the visual when the metrics were created.
    /// </summary>
    public Rect VisualBounds { get; }

    /// <summary>
    /// Gets the offset, in device-independent units, applied to the visual content before rendering (used for overflow pagination).
    /// </summary>
    public Point ContentOffset { get; }

    /// <summary>
    /// Creates metrics from the supplied visual and page settings.
    /// </summary>
    public static PrintPageMetrics Create(Visual visual, PrintPageSettings settings, Vector? dpi = null)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(settings);

        var visualBounds = visual.Bounds;
        var contentOffset = new Point();

        if (settings.SelectionBounds is { } selection && selection.Width > 0 && selection.Height > 0)
        {
            visualBounds = selection;
            contentOffset = selection.Position;
        }

        var pageSize = settings.TargetSize ?? visualBounds.Size;
        if (pageSize.Width <= 0 || pageSize.Height <= 0)
        {
            pageSize = new Size(
                Math.Max(visualBounds.Width, 1d),
                Math.Max(visualBounds.Height, 1d));
        }

        if (pageSize.Width <= 0)
        {
            pageSize = new Size(1d, pageSize.Height);
        }

        if (pageSize.Height <= 0)
        {
            pageSize = new Size(pageSize.Width, 1d);
        }

        var margins = settings.Margins ?? new Thickness();

        var contentWidth = Math.Max(0d, pageSize.Width - (margins.Left + margins.Right));
        var contentHeight = Math.Max(0d, pageSize.Height - (margins.Top + margins.Bottom));
        var contentRect = new Rect(new Point(margins.Left, margins.Top), new Size(contentWidth, contentHeight));

        var effectiveDpi = dpi ?? new Vector(DefaultLogicalDpi, DefaultLogicalDpi);
        var pagePixelSize = CreatePixelSize(pageSize, effectiveDpi);
        var contentPixelRect = new PixelRect(
            DipToPixels(margins.Left, effectiveDpi.X),
            DipToPixels(margins.Top, effectiveDpi.Y),
            Math.Max(0, DipToPixels(contentWidth, effectiveDpi.X)),
            Math.Max(0, DipToPixels(contentHeight, effectiveDpi.Y)));

        var scale = settings.Scale <= 0 ? 1d : settings.Scale;

        return new PrintPageMetrics(
            pageSize,
            margins,
            contentRect,
            scale,
            effectiveDpi,
            pagePixelSize,
            contentPixelRect,
            visualBounds,
            contentOffset);
    }

    public PrintPageMetrics WithContentOffset(Point offset) =>
        new PrintPageMetrics(
            PageSize,
            Margins,
            ContentRect,
            ContentScale,
            Dpi,
            PagePixelSize,
            ContentPixelRect,
            VisualBounds,
            offset);

    public PrintPageMetrics WithSelection(Rect selection)
    {
        if (selection.Width <= 0 || selection.Height <= 0)
        {
            return this;
        }

        var margins = Margins;
        var pageSize = new Size(
            Math.Max(selection.Width + margins.Left + margins.Right, 1d),
            Math.Max(selection.Height + margins.Top + margins.Bottom, 1d));

        var contentRect = new Rect(new Point(margins.Left, margins.Top), selection.Size);

        var pagePixelSize = CreatePixelSize(pageSize, Dpi);
        var contentPixelRect = new PixelRect(
            DipToPixels(contentRect.X, Dpi.X),
            DipToPixels(contentRect.Y, Dpi.Y),
            Math.Max(1, DipToPixels(selection.Width, Dpi.X)),
            Math.Max(1, DipToPixels(selection.Height, Dpi.Y)));

        var visualBounds = new Rect(selection.Position, selection.Size);

        return new PrintPageMetrics(
            pageSize,
            margins,
            contentRect,
            ContentScale,
            Dpi,
            pagePixelSize,
            contentPixelRect,
            visualBounds,
            selection.Position);
    }

    private static PixelSize CreatePixelSize(Size size, Vector dpi)
    {
        var width = Math.Max(1, DipToPixels(size.Width, dpi.X));
        var height = Math.Max(1, DipToPixels(size.Height, dpi.Y));
        return new PixelSize(width, height);
    }

    private static int DipToPixels(double dip, double dpi) =>
        (int)Math.Round(dip * dpi / DefaultLogicalDpi, MidpointRounding.AwayFromZero);
}
