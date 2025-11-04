using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using PrintingTools.Core.Rendering;

namespace PrintingTools.Core.Pagination;

public static class PrintPaginationUtilities
{
    private const double InchesToDeviceIndependentPixels = 96d;
    private const double InchesToPoints = 72d;
    private const double DefaultTileSpacing = 12d;
    private const double DefaultTilePadding = 8d;
    private static readonly Size DefaultPaperSizeInches = new(8.5, 11);

    public static IEnumerable<PrintPage> ExpandPage(PrintPage page, PrintOptions options)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(options);

        var normalizedPage = ApplyPageSetup(page, options);
        var metrics = normalizedPage.Metrics ?? PrintPageMetrics.Create(normalizedPage.Visual, normalizedPage.Settings);
        var effectivePage = ReferenceEquals(metrics, normalizedPage.Metrics)
            ? normalizedPage
            : new PrintPage(normalizedPage.Visual, normalizedPage.Settings, normalizedPage.IsPageBreakAfter, metrics);

        if (options.SelectionOnlyRequested)
        {
            var trimmed = TrimToSelection(effectivePage, metrics);
            if (trimmed is null)
            {
                yield break;
            }

            effectivePage = trimmed.Value.page;
            metrics = trimmed.Value.metrics;
        }

        foreach (var expanded in ExpandWithMetrics(effectivePage, metrics))
        {
            yield return expanded;
        }
    }

    public static IReadOnlyList<PrintPage> ApplyPageRange(IReadOnlyList<PrintPage> pages, PrintPageRange? range)
    {
        if (range is null || pages.Count == 0)
        {
            return pages;
        }

        var (start, end) = range.Value;
        var filtered = new List<PrintPage>();
        for (var i = 0; i < pages.Count; i++)
        {
            var pageNumber = i + 1;
            if (pageNumber < start)
            {
                continue;
            }

            if (pageNumber > end)
            {
                break;
            }

            filtered.Add(pages[i]);
        }

        return filtered;
    }

    public static IReadOnlyList<PrintPage> ApplyAdvancedLayout(IReadOnlyList<PrintPage> pages, PrintOptions options)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(options);

        if (pages.Count == 0)
        {
            return pages;
        }

        return options.LayoutKind switch
        {
            PrintLayoutKind.NUp => ApplyNUpLayout(pages, options),
            PrintLayoutKind.Booklet => ApplyBookletLayout(pages, options),
            PrintLayoutKind.Poster => ApplyPosterLayout(pages, options),
            _ => pages
        };
    }

    private static PrintPage ApplyPageSetup(PrintPage page, PrintOptions options)
    {
        var visual = page.Visual;
        var baseSettings = page.Settings ?? PrintPageSettings.Default;
        var pageSize = GetPageSize(options);
        var margins = GetBaseMargins(baseSettings, options);

        if (options.CenterHorizontally || options.CenterVertically)
        {
            margins = ApplyCentering(margins, pageSize, baseSettings, visual, options);
        }

        var settings = new PrintPageSettings
        {
            TargetSize = pageSize,
            Margins = margins,
            Scale = baseSettings.Scale,
            SelectionBounds = baseSettings.SelectionBounds
        };

        var metrics = PrintPageMetrics.Create(visual, settings);
        return new PrintPage(page.Visual, settings, page.IsPageBreakAfter, metrics);
    }

    private static Size GetPageSize(PrintOptions options)
    {
        var paper = options.PaperSize;
        if (!IsFinitePositive(paper.Width) || !IsFinitePositive(paper.Height))
        {
            paper = DefaultPaperSizeInches;
        }

        var width = paper.Width;
        var height = paper.Height;
        if (options.Orientation == PageOrientation.Landscape)
        {
            (width, height) = (height, width);
        }

        width = Math.Max(width, 0.1);
        height = Math.Max(height, 0.1);

        return new Size(width * InchesToDeviceIndependentPixels, height * InchesToDeviceIndependentPixels);
    }

    private static Thickness GetBaseMargins(PrintPageSettings baseSettings, PrintOptions options)
    {
        var margins = options.UsePrintableArea && baseSettings.Margins is { } printableMargins
            ? printableMargins
            : ConvertMargins(options.Margins);

        return EnsureValidMargins(margins);
    }

    private static Thickness ConvertMargins(Thickness marginsInInches)
    {
        static double Convert(double value) => Math.Max(0, value * InchesToDeviceIndependentPixels);

        return new Thickness(
            Convert(marginsInInches.Left),
            Convert(marginsInInches.Top),
            Convert(marginsInInches.Right),
            Convert(marginsInInches.Bottom));
    }

    private static Thickness EnsureValidMargins(Thickness margins)
    {
        static double Sanitize(double value) => double.IsFinite(value) && value >= 0 ? value : 0;

        return new Thickness(
            Sanitize(margins.Left),
            Sanitize(margins.Top),
            Sanitize(margins.Right),
            Sanitize(margins.Bottom));
    }

    private static Thickness ApplyCentering(
        Thickness margins,
        Size pageSize,
        PrintPageSettings baseSettings,
        Visual visual,
        PrintOptions options)
    {
        var scale = baseSettings.Scale <= 0 ? 1d : baseSettings.Scale;
        var visualSize = baseSettings.SelectionBounds?.Size ?? visual.Bounds.Size;
        var adjustedMargins = margins;

        if (options.CenterHorizontally)
        {
            var contentWidth = Math.Max(0, pageSize.Width - (margins.Left + margins.Right));
            var visualWidth = visualSize.Width * scale;
            if (visualWidth < contentWidth - 0.1)
            {
                var delta = (contentWidth - visualWidth) / 2;
                adjustedMargins = new Thickness(
                    adjustedMargins.Left + delta,
                    adjustedMargins.Top,
                    adjustedMargins.Right + delta,
                    adjustedMargins.Bottom);
            }
        }

        if (options.CenterVertically)
        {
            var contentHeight = Math.Max(0, pageSize.Height - (margins.Top + margins.Bottom));
            var visualHeight = visualSize.Height * scale;
            if (visualHeight < contentHeight - 0.1)
            {
                var delta = (contentHeight - visualHeight) / 2;
                adjustedMargins = new Thickness(
                    adjustedMargins.Left,
                    adjustedMargins.Top + delta,
                    adjustedMargins.Right,
                    adjustedMargins.Bottom + delta);
            }
        }

        return EnsureValidMargins(adjustedMargins);
    }

    internal static PageMediaSize CreatePageMediaSize(PrintOptions options)
    {
        var paper = options.PaperSize;
        if (!IsFinitePositive(paper.Width) || !IsFinitePositive(paper.Height))
        {
            paper = DefaultPaperSizeInches;
        }

        var orientedWidth = paper.Width;
        var orientedHeight = paper.Height;
        if (options.Orientation == PageOrientation.Landscape)
        {
            (orientedWidth, orientedHeight) = (orientedHeight, orientedWidth);
        }

        orientedWidth = Math.Max(orientedWidth, 0.1);
        orientedHeight = Math.Max(orientedHeight, 0.1);

        var widthPoints = orientedWidth * InchesToPoints;
        var heightPoints = orientedHeight * InchesToPoints;
        var name = $"Custom {orientedWidth:0.##}x{orientedHeight:0.##} in";

        return new PageMediaSize(name, widthPoints, heightPoints);
    }

    private static bool IsFinitePositive(double value) =>
        double.IsFinite(value) && value > 0;

    private static IReadOnlyList<PrintPage> ApplyNUpLayout(IReadOnlyList<PrintPage> pages, PrintOptions options)
    {
        var rows = Math.Max(1, options.NUpRows);
        var columns = Math.Max(1, options.NUpColumns);
        var tilesPerSheet = rows * columns;

        if (tilesPerSheet <= 1)
        {
            return pages;
        }

        var sheetSize = GetPageSize(options);
        if (sheetSize.Width <= 0 || sheetSize.Height <= 0)
        {
            return pages;
        }

        var tileSpacingX = columns > 1 ? Math.Min(DefaultTileSpacing, sheetSize.Width / (columns * 6d)) : 0d;
        var tileSpacingY = rows > 1 ? Math.Min(DefaultTileSpacing, sheetSize.Height / (rows * 6d)) : 0d;
        var tileWidth = (sheetSize.Width - (columns - 1) * tileSpacingX) / columns;
        var tileHeight = (sheetSize.Height - (rows - 1) * tileSpacingY) / rows;

        if (tileWidth <= 0 || tileHeight <= 0)
        {
            return pages;
        }

        var tileOrder = BuildTileOrder(rows, columns, options.NUpOrder);
        var result = new List<PrintPage>();
        var index = 0;

        while (index < pages.Count)
        {
            var tiles = new List<NUpTile>(tilesPerSheet);
            var anyPageBreak = false;

            foreach (var (row, column) in tileOrder)
            {
                if (index >= pages.Count)
                {
                    break;
                }

                var page = pages[index++];
                var originX = column * (tileWidth + tileSpacingX);
                var originY = row * (tileHeight + tileSpacingY);
                var bounds = new Rect(originX, originY, tileWidth, tileHeight);
                var tile = CreateTile(page, bounds);
                tiles.Add(tile);

                if (page.IsPageBreakAfter)
                {
                    anyPageBreak = true;
                }
            }

            if (tiles.Count == 0)
            {
                break;
            }

            var composite = CreateCompositePage(sheetSize, tiles, anyPageBreak);
            result.Add(composite);
        }

        return new ReadOnlyCollection<PrintPage>(result);
    }

    private static IReadOnlyList<PrintPage> ApplyBookletLayout(IReadOnlyList<PrintPage> pages, PrintOptions options)
    {
        if (pages.Count == 0)
        {
            return pages;
        }

        var padded = new List<PrintPage>(pages);
        var remainder = padded.Count % 4;
        if (remainder != 0)
        {
            var blanksNeeded = 4 - remainder;
            for (var i = 0; i < blanksNeeded; i++)
            {
                padded.Add(CreateBlankPage(options));
            }
        }

        if (padded.Count < 4)
        {
            // Not enough content to form a booklet spread; fall back to n-up behavior.
            var fallback = options.Clone();
            fallback.LayoutKind = PrintLayoutKind.NUp;
            fallback.NUpRows = 1;
            fallback.NUpColumns = 2;
            fallback.NUpOrder = NUpPageOrder.LeftToRightTopToBottom;
            return ApplyNUpLayout(padded, fallback);
        }

        var sequence = new List<PrintPage>(padded.Count);
        var left = padded.Count - 1;
        var right = 0;

        while (right < left)
        {
            // Front side: outer spread
            sequence.Add(padded[left--]);  // left position
            sequence.Add(padded[right++]); // right position

            if (right > left)
            {
                break;
            }

            // Back side: inner spread
            sequence.Add(padded[right++]); // left position (after flipping)
            sequence.Add(padded[left--]);  // right position
        }

        var bookletOptions = options.Clone();
        bookletOptions.LayoutKind = PrintLayoutKind.NUp;
        bookletOptions.NUpRows = 1;
        bookletOptions.NUpColumns = 2;
        bookletOptions.NUpOrder = NUpPageOrder.LeftToRightTopToBottom;

        return ApplyNUpLayout(sequence, bookletOptions);
    }

    private static IReadOnlyList<PrintPage> ApplyPosterLayout(IReadOnlyList<PrintPage> pages, PrintOptions options)
    {
        var tileCount = Math.Max(1, options.PosterTileCount);
        if (tileCount <= 1)
        {
            return pages;
        }

        var result = new List<PrintPage>();

        foreach (var page in pages)
        {
            var baseMetrics = page.Metrics ?? PrintPageMetrics.Create(page.Visual, page.Settings);
            var contentRect = baseMetrics.ContentRect;
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                result.Add(page);
                continue;
            }

            var (rows, columns) = DeterminePosterGrid(tileCount, baseMetrics.PageSize);
            var maxTiles = rows * columns;
            var effectiveTiles = Math.Min(tileCount, maxTiles);
            var tilesProduced = 0;

            for (var row = 0; row < rows && tilesProduced < effectiveTiles; row++)
            {
                for (var column = 0; column < columns && tilesProduced < effectiveTiles; column++)
                {
                    var tileVisual = new PosterTileVisual(page, baseMetrics, row, column, rows, columns);
                    tileVisual.Measure(baseMetrics.PageSize);
                    tileVisual.Arrange(new Rect(baseMetrics.PageSize));

                    var tileSettings = new PrintPageSettings
                    {
                        TargetSize = baseMetrics.PageSize,
                        Margins = baseMetrics.Margins,
                        Scale = 1d
                    };

                    var tileMetrics = PrintPageMetrics.Create(tileVisual, tileSettings);
                    var isLastTile = tilesProduced == effectiveTiles - 1 && page.IsPageBreakAfter;

                    result.Add(new PrintPage(tileVisual, tileSettings, isLastTile, tileMetrics));
                    tilesProduced++;
                }
            }
        }

        return new ReadOnlyCollection<PrintPage>(result);
    }

    private static (int rows, int columns) DeterminePosterGrid(int tileCount, Size pageSize)
    {
        var count = Math.Max(tileCount, 1);
        var targetAspect = pageSize.Height <= 0 ? 1d : pageSize.Width / pageSize.Height;
        var bestRows = 1;
        var bestColumns = count;
        var bestScore = double.MaxValue;

        for (var rows = 1; rows <= count; rows++)
        {
            var columns = (int)Math.Ceiling(count / (double)rows);
            if (rows * columns < count)
            {
                continue;
            }

            var aspect = columns / (double)rows;
            var score = Math.Abs(aspect - targetAspect);
            if (score < bestScore)
            {
                bestScore = score;
                bestRows = rows;
                bestColumns = columns;
            }
        }

        return (bestRows, bestColumns);
    }

    private static PrintPage CreateBlankPage(PrintOptions options)
    {
        var pageSize = GetPageSize(options);
        var blank = new Border
        {
            Width = pageSize.Width,
            Height = pageSize.Height,
            Background = Brushes.White,
            IsHitTestVisible = false
        };

        blank.Measure(pageSize);
        blank.Arrange(new Rect(pageSize));

        var settings = new PrintPageSettings
        {
            TargetSize = pageSize,
            Margins = new Thickness(),
            Scale = 1d
        };

        var metrics = PrintPageMetrics.Create(blank, settings);
        return new PrintPage(blank, settings, false, metrics);
    }

    private static double ComputeTileScale(PrintPageMetrics sourceMetrics, Rect bounds, Thickness margins)
    {
        var availableWidth = Math.Max(0d, bounds.Width - (margins.Left + margins.Right));
        var availableHeight = Math.Max(0d, bounds.Height - (margins.Top + margins.Bottom));
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return 1d;
        }

        var sourceSize = sourceMetrics.PageSize;
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return 1d;
        }

        var scaleX = availableWidth / sourceSize.Width;
        var scaleY = availableHeight / sourceSize.Height;
        var scale = Math.Min(scaleX, scaleY);
        return double.IsFinite(scale) && scale > 0 ? scale : 1d;
    }

    private static NUpTile CreateTile(PrintPage page, Rect bounds)
    {
        var baseSettings = page.Settings ?? PrintPageSettings.Default;
        var sourceMetrics = page.Metrics ?? PrintPageMetrics.Create(page.Visual, baseSettings);
        var marginValue = Math.Min(DefaultTilePadding, Math.Min(bounds.Width, bounds.Height) / 6d);
        var tileMargins = new Thickness(marginValue);

        var scale = ComputeTileScale(sourceMetrics, bounds, tileMargins);
        var tileSettings = new PrintPageSettings
        {
            TargetSize = bounds.Size,
            Margins = tileMargins,
            Scale = scale,
            SelectionBounds = baseSettings.SelectionBounds
        };

        var tileMetrics = PrintPageMetrics.Create(page.Visual, tileSettings);
        return new NUpTile(page, tileMetrics, bounds);
    }

    private static PrintPage CreateCompositePage(Size sheetSize, IReadOnlyList<NUpTile> tiles, bool isPageBreakAfter)
    {
        var compositeSettings = new PrintPageSettings
        {
            TargetSize = sheetSize,
            Margins = new Thickness()
        };

        var visual = new NUpCompositeVisual(sheetSize, tiles);
        visual.Measure(sheetSize);
        visual.Arrange(new Rect(sheetSize));

        var metrics = PrintPageMetrics.Create(visual, compositeSettings);
        return new PrintPage(visual, compositeSettings, isPageBreakAfter, metrics);
    }

    private static IReadOnlyList<(int row, int column)> BuildTileOrder(int rows, int columns, NUpPageOrder order)
    {
        var sequence = new List<(int row, int column)>(rows * columns);

        switch (order)
        {
            case NUpPageOrder.TopToBottomLeftToRight:
                for (var column = 0; column < columns; column++)
                {
                    for (var row = 0; row < rows; row++)
                    {
                        sequence.Add((row, column));
                    }
                }
                break;

            case NUpPageOrder.RightToLeftTopToBottom:
                for (var row = 0; row < rows; row++)
                {
                    for (var column = columns - 1; column >= 0; column--)
                    {
                        sequence.Add((row, column));
                    }
                }
                break;

            case NUpPageOrder.TopToBottomRightToLeft:
                for (var column = columns - 1; column >= 0; column--)
                {
                    for (var row = 0; row < rows; row++)
                    {
                        sequence.Add((row, column));
                    }
                }
                break;

            default:
                for (var row = 0; row < rows; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        sequence.Add((row, column));
                    }
                }
                break;
        }

        return sequence;
    }

    private readonly record struct NUpTile(PrintPage Page, PrintPageMetrics Metrics, Rect Bounds);

    private sealed class NUpCompositeVisual : Control
    {
        private readonly IReadOnlyList<NUpTile> _tiles;
        private readonly Size _sheetSize;

        public NUpCompositeVisual(Size sheetSize, IReadOnlyList<NUpTile> tiles)
        {
            _sheetSize = sheetSize;
            _tiles = tiles;
            Width = sheetSize.Width;
            Height = sheetSize.Height;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.FillRectangle(Brushes.White, new Rect(_sheetSize));

            foreach (var tile in _tiles)
            {
                using (context.PushTransform(Matrix.CreateTranslation(tile.Bounds.X, tile.Bounds.Y)))
                using (context.PushClip(new Rect(tile.Bounds.Size)))
                {
                    PrintPageRenderer.RenderToDrawingContext(context, tile.Page, tile.Metrics);
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize) => _sheetSize;

        protected override Size ArrangeOverride(Size finalSize) => _sheetSize;
    }

    private sealed class PosterTileVisual : Control
    {
        private readonly PrintPage _sourcePage;
        private readonly PrintPageMetrics _sourceMetrics;
        private readonly int _row;
        private readonly int _column;
        private readonly int _rows;
        private readonly int _columns;

        public PosterTileVisual(PrintPage sourcePage, PrintPageMetrics sourceMetrics, int row, int column, int rows, int columns)
        {
            _sourcePage = sourcePage ?? throw new ArgumentNullException(nameof(sourcePage));
            _sourceMetrics = sourceMetrics ?? throw new ArgumentNullException(nameof(sourceMetrics));
            _row = row;
            _column = column;
            _rows = Math.Max(1, rows);
            _columns = Math.Max(1, columns);

            Width = sourceMetrics.PageSize.Width;
            Height = sourceMetrics.PageSize.Height;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var pageSize = _sourceMetrics.PageSize;
            context.FillRectangle(Brushes.White, new Rect(pageSize));

            var contentRect = _sourceMetrics.ContentRect;
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                return;
            }

            var baseScale = _sourceMetrics.ContentScale <= 0 ? 1d : _sourceMetrics.ContentScale;
            var tileWidthVisual = (contentRect.Width / baseScale) / _columns;
            var tileHeightVisual = (contentRect.Height / baseScale) / _rows;

            var offsetX = _sourceMetrics.ContentOffset.X + _column * tileWidthVisual;
            var offsetY = _sourceMetrics.ContentOffset.Y + _row * tileHeightVisual;

            var scaleX = baseScale * _columns;
            var scaleY = baseScale * _rows;

            using (context.PushTransform(Matrix.CreateTranslation(contentRect.X, contentRect.Y)))
            using (context.PushClip(new Rect(contentRect.Size)))
            using (context.PushTransform(Matrix.CreateScale(scaleX, scaleY)))
            using (context.PushTransform(Matrix.CreateTranslation(-offsetX, -offsetY)))
            using (context.PushTransform(Matrix.CreateTranslation(-_sourceMetrics.VisualBounds.X, -_sourceMetrics.VisualBounds.Y)))
            {
                PosterTileVisualRenderer.Render(_sourcePage.Visual, context);
            }
        }
    }

    private static class PosterTileVisualRenderer
    {
        private static readonly MethodInfo? ImmediateRendererRenderMethod =
            Type.GetType("Avalonia.Rendering.ImmediateRenderer, Avalonia.Base", throwOnError: false)
                ?.GetMethod(
                    "Render",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null,
                    new[] { typeof(Visual), typeof(DrawingContext) },
                    modifiers: null);

        public static void Render(Visual visual, DrawingContext context)
        {
            ArgumentNullException.ThrowIfNull(visual);
            ArgumentNullException.ThrowIfNull(context);

            if (ImmediateRendererRenderMethod is { } method)
            {
                method.Invoke(null, new object[] { visual, context });
                return;
            }

            RenderVisualTree(context, visual);
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

    private static IEnumerable<PrintPage> ExpandWithMetrics(PrintPage page, PrintPageMetrics metrics)
    {
        var scale = metrics.ContentScale <= 0 ? 1d : metrics.ContentScale;
        var availableWidth = metrics.ContentRect.Width / scale;
        var availableHeight = metrics.ContentRect.Height / scale;

        if (availableWidth <= 0 || availableHeight <= 0)
        {
            yield return EnsurePageHasMetrics(page, metrics);
            yield break;
        }

        var baseOffset = metrics.ContentOffset;
        var visualHeight = metrics.VisualBounds.Height;
        var remainingHeight = Math.Max(visualHeight - baseOffset.Y, 0);

        if (remainingHeight <= availableHeight + 0.5)
        {
            yield return EnsurePageHasMetrics(page, metrics);
            yield break;
        }

        var pageCount = Math.Max(1, (int)Math.Ceiling(remainingHeight / availableHeight));
        var maxOffset = Math.Max(visualHeight - availableHeight, 0);
        var basePageBreak = page.IsPageBreakAfter;

        for (var index = 0; index < pageCount; index++)
        {
            var offsetY = baseOffset.Y + index * availableHeight;
            offsetY = Math.Min(offsetY, maxOffset);

            var offset = new Point(baseOffset.X, offsetY);
            var sliceMetrics = index == 0 && offset.Equals(metrics.ContentOffset)
                ? metrics
                : metrics.WithContentOffset(offset);

            var isLast = index == pageCount - 1;
            yield return new PrintPage(
                page.Visual,
                page.Settings,
                isLast ? basePageBreak : false,
                sliceMetrics);
        }
    }

    private static PrintPage EnsurePageHasMetrics(PrintPage page, PrintPageMetrics metrics)
    {
        if (ReferenceEquals(metrics, page.Metrics))
        {
            return page;
        }

        return new PrintPage(page.Visual, page.Settings, page.IsPageBreakAfter, metrics);
    }

    private static (PrintPage page, PrintPageMetrics metrics)? TrimToSelection(PrintPage page, PrintPageMetrics metrics)
    {
        var selection = page.Settings.SelectionBounds ?? CollectSelectionBounds(page.Visual);
        if (selection is null || selection.Value.Width <= 0 || selection.Value.Height <= 0)
        {
            return null;
        }

        var trimmedSettings = new PrintPageSettings
        {
            TargetSize = page.Settings.TargetSize,
            Margins = page.Settings.Margins,
            Scale = page.Settings.Scale,
            SelectionBounds = selection
        };

        var trimmedMetrics = metrics.WithSelection(selection.Value);
        var trimmedPage = new PrintPage(page.Visual, trimmedSettings, page.IsPageBreakAfter, trimmedMetrics);
        return (trimmedPage, trimmedMetrics);
    }

    private static Rect? CollectSelectionBounds(Visual visual)
    {
        Rect? union = NormalizeSelectionRect(PrintLayoutHints.GetSelectionBounds(visual));

        foreach (var descendant in visual.GetVisualDescendants())
        {
            var candidate = NormalizeSelectionRect(PrintLayoutHints.GetSelectionBounds(descendant));
            if (candidate is { } rect)
            {
                union = union is null ? rect : Union(union.Value, rect);
            }
        }

        return union;
    }

    private static Rect? NormalizeSelectionRect(Rect? candidate)
    {
        if (candidate is { Width: > 0, Height: > 0 } rect)
        {
            return rect;
        }

        return null;
    }

    private static Rect Union(Rect left, Rect right)
    {
        var x1 = Math.Min(left.X, right.X);
        var y1 = Math.Min(left.Y, right.Y);
        var x2 = Math.Max(left.Right, right.Right);
        var y2 = Math.Max(left.Bottom, right.Bottom);
        return new Rect(new Point(x1, y1), new Point(x2, y2));
    }
}
