using System;

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
            UseVectorRenderer = UseVectorRenderer
        };
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
