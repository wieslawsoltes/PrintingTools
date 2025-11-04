using System;
using Avalonia;

namespace PrintingTools.Core;

public sealed class PrintOptions
{
    public bool ShowPrintDialog { get; set; } = true;

    public string? PrinterName { get; set; }

    public string? JobName { get; set; }

    public PrintPageRange? PageRange { get; set; }

    public bool CollectPreviewFirst { get; set; } = true;

    public string? PdfOutputPath { get; set; }

    public bool UseManagedPdfExporter { get; set; }

    /// <summary>
    /// When true, the adapter should prefer vector (PDF) rendering over raster surfaces where supported.
    /// </summary>
    public bool UseVectorRenderer { get; set; }

    /// <summary>
    /// Preferred color space for print output; platforms may fall back when unsupported.
    /// </summary>
    public PrintColorSpace ColorSpacePreference { get; set; } = PrintColorSpace.Auto;

    /// <summary>
    /// When true, show the native page layout dialog (`NSPageLayout`) prior to the print panel on supported platforms.
    /// </summary>
    public bool ShowPageLayoutDialog { get; set; }

    /// <summary>
    /// When true, enables content selection options (e.g., selection-only printing) in native print dialogs.
    /// </summary>
    public bool EnableSelection { get; set; }

    /// <summary>
    /// Indicates whether the user chose to print the current selection from the native dialog.
    /// </summary>
    public bool SelectionOnlyRequested { get; set; }

    /// <summary>
    /// Indicates whether pagination should respect the printable area reported by the printer.
    /// </summary>
    public bool UsePrintableArea { get; set; } = true;

    /// <summary>
    /// Desired page orientation for managed previews and back-ends.
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>
    /// Margins expressed in inches.
    /// </summary>
    public Thickness Margins { get; set; } = new Thickness(0.5);

    /// <summary>
    /// Indicates whether pages should be centered horizontally when rendered.
    /// </summary>
    public bool CenterHorizontally { get; set; }

    /// <summary>
    /// Indicates whether pages should be centered vertically when rendered.
    /// </summary>
    public bool CenterVertically { get; set; }

    /// <summary>
    /// Preferred paper size expressed in inches.
    /// </summary>
    public Size PaperSize { get; set; } = new Size(8.5, 11);

    /// <summary>
    /// Specifies which advanced layout strategy should be applied when rendering pages.
    /// </summary>
    public PrintLayoutKind LayoutKind
    {
        get => _layoutKind;
        set => _layoutKind = Enum.IsDefined(typeof(PrintLayoutKind), value) ? value : PrintLayoutKind.Standard;
    }

    /// <summary>
    /// Defines the number of rows used when <see cref="LayoutKind"/> is <see cref="PrintLayoutKind.NUp"/>.
    /// </summary>
    public int NUpRows
    {
        get => _nUpRows;
        set => _nUpRows = Math.Clamp(value, 1, 16);
    }

    /// <summary>
    /// Defines the number of columns used when <see cref="LayoutKind"/> is <see cref="PrintLayoutKind.NUp"/>.
    /// </summary>
    public int NUpColumns
    {
        get => _nUpColumns;
        set => _nUpColumns = Math.Clamp(value, 1, 16);
    }

    /// <summary>
    /// Controls the ordering of pages when they are arranged using an N-up layout.
    /// </summary>
    public NUpPageOrder NUpOrder
    {
        get => _nUpOrder;
        set => _nUpOrder = Enum.IsDefined(typeof(NUpPageOrder), value) ? value : NUpPageOrder.LeftToRightTopToBottom;
    }

    /// <summary>
    /// When set, hints that booklet imposition should bind along the long edge (true) or short edge (false).
    /// </summary>
    public bool BookletBindLongEdge { get; set; } = true;

    /// <summary>
    /// Indicates how many logical tiles should be produced per physical sheet when using poster mode.
    /// </summary>
    public int PosterTileCount
    {
        get => _posterTileCount;
        set => _posterTileCount = Math.Clamp(value, 1, 64);
    }

    private PrintLayoutKind _layoutKind = PrintLayoutKind.Standard;
    private int _nUpRows = 1;
    private int _nUpColumns = 1;
    private NUpPageOrder _nUpOrder = NUpPageOrder.LeftToRightTopToBottom;
    private int _posterTileCount = 1;

    public PrintOptions Clone() =>
        new()
        {
            ShowPrintDialog = ShowPrintDialog,
            PrinterName = PrinterName,
            JobName = JobName,
            PageRange = PageRange,
            CollectPreviewFirst = CollectPreviewFirst,
            PdfOutputPath = PdfOutputPath,
            UseManagedPdfExporter = UseManagedPdfExporter,
            UseVectorRenderer = UseVectorRenderer,
            ColorSpacePreference = ColorSpacePreference,
            ShowPageLayoutDialog = ShowPageLayoutDialog,
            EnableSelection = EnableSelection,
            SelectionOnlyRequested = SelectionOnlyRequested,
            UsePrintableArea = UsePrintableArea,
            Orientation = Orientation,
            Margins = Margins,
            CenterHorizontally = CenterHorizontally,
            CenterVertically = CenterVertically,
            PaperSize = PaperSize,
            LayoutKind = LayoutKind,
            NUpRows = NUpRows,
            NUpColumns = NUpColumns,
            NUpOrder = NUpOrder,
            BookletBindLongEdge = BookletBindLongEdge,
            PosterTileCount = PosterTileCount
        };
}

public enum PrintColorSpace
{
    Auto = 0,
    SRgb = 1,
    DisplayP3 = 2
}

public readonly record struct PrintPageRange(int StartPage, int EndPage)
{
    public int StartPage { get; } = StartPage <= 0
        ? throw new ArgumentOutOfRangeException(nameof(StartPage), StartPage, "The first page must be greater than zero.")
        : StartPage;

    public int EndPage { get; } = EndPage < StartPage
        ? throw new ArgumentOutOfRangeException(nameof(EndPage), EndPage, "The last page must not be less than the first page.")
        : EndPage;

    public void Deconstruct(out int startPage, out int endPage)
    {
        startPage = StartPage;
        endPage = EndPage;
    }
}

public enum PrintLayoutKind
{
    Standard = 0,
    NUp = 1,
    Booklet = 2,
    Poster = 3
}

public enum NUpPageOrder
{
    LeftToRightTopToBottom = 0,
    TopToBottomLeftToRight = 1,
    RightToLeftTopToBottom = 2,
    TopToBottomRightToLeft = 3
}
