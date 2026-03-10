---
title: "Pagination and Layout Model"
---

# Pagination and Layout Model

PrintingTools separates document construction from pagination and output formatting.

## Main building blocks

- <xref:PrintingTools.Core.PrintDocument> packages printable visuals or enumerators.
- <xref:PrintingTools.Core.Pagination.IPrintPaginator> decides how pages are split and expanded.
- <xref:PrintingTools.Core.PrintPage> and <xref:PrintingTools.Core.PrintPageMetrics> capture the actual page geometry.
- <xref:PrintingTools.Core.PrintOptions> controls margins, paper size, page ranges, and layout modes.

## Layout modes

<xref:PrintingTools.Core.PrintLayoutKind> currently supports:

- `Standard`
- `NUp`
- `Booklet`
- `Poster`

Platform adapters consume the normalized layout metadata rather than inventing their own per-platform rules.

## Layout hints

<xref:PrintingTools.Core.PrintLayoutHints> lets you annotate visuals with printable metadata such as page breaks, page names, and layout-affecting hints. Those hints feed the default paginator and remain available to custom paginator implementations.

## Page metrics

<xref:PrintingTools.Core.PrintPageMetrics> normalizes:

- logical page size
- pixel page size
- content rectangle after margins
- content offsets used when a single visual expands to multiple pages

That shared model keeps preview thumbnails, vector export, and native print output aligned.

## Related

- [Previews, Rendering, and Diagnostics](previews-rendering-and-diagnostics.md)
- [Page Setup and Preview UI](../guides/page-setup-and-preview-ui.md)
