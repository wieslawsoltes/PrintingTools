using System.Collections.Generic;

namespace PrintingTools.Core.Rendering;

/// <summary>
/// Describes a renderer capable of producing vector (typically PDF) representations of a print page sequence.
/// </summary>
public interface IVectorPageRenderer
{
    /// <summary>
    /// Renders the supplied pages to a PDF file at the given path, overwriting any existing file.
    /// </summary>
    void ExportPdf(string path, IReadOnlyList<PrintPage> pages);

    /// <summary>
    /// Renders the supplied pages to a PDF byte payload that can be consumed by native preview/print surfaces.
    /// </summary>
    byte[] CreatePdfBytes(IReadOnlyList<PrintPage> pages);
}
