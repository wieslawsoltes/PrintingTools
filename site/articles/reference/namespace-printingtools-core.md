---
title: "Namespace: PrintingTools.Core"
---

# Namespace: PrintingTools.Core

`PrintingTools.Core` contains the shared abstraction layer used by every platform package.

## Functional groups

### Startup and orchestration

- <xref:PrintingTools.Core.PrintServiceRegistry>
- <xref:PrintingTools.Core.PrintingToolsOptions>
- <xref:PrintingTools.Core.IPrintManager>
- <xref:PrintingTools.Core.IPrintAdapter>
- <xref:PrintingTools.Core.IPrintAdapterResolver>

### Documents, requests, and sessions

- <xref:PrintingTools.Core.PrintRequest>
- <xref:PrintingTools.Core.PrintDocument>
- <xref:PrintingTools.Core.PrintSession>
- <xref:PrintingTools.Core.PrintPage>
- <xref:PrintingTools.Core.PrintPageSettings>

### Tickets and capabilities

- <xref:PrintingTools.Core.PrinterInfo>
- <xref:PrintingTools.Core.PrintCapabilities>
- <xref:PrintingTools.Core.PrintTicketModel>
- <xref:PrintingTools.Core.PageMediaSize>
- <xref:PrintingTools.Core.CommonPageMediaSizes>

### Pagination, preview, and rendering

- <xref:PrintingTools.Core.Pagination.IPrintPaginator>
- <xref:PrintingTools.Core.Pagination.DefaultPrintPaginator>
- <xref:PrintingTools.Core.PrintPreviewModel>
- <xref:PrintingTools.Core.Rendering.PrintRenderPipeline>
- <xref:PrintingTools.Core.Rendering.IVectorPageRenderer>
- <xref:PrintingTools.Core.Preview.IPrintPreviewProvider>

### Diagnostics and layout helpers

- <xref:PrintingTools.Core.PrintDiagnostics>
- <xref:PrintingTools.Core.PrintDiagnosticEvent>
- <xref:PrintingTools.Core.PrintLayoutHints>
- <xref:PrintingTools.Core.PrintPageMetrics>

## Related

- [Architecture and Service Model](../concepts/architecture-and-service-model.md)
- [API Coverage Index](api-coverage-index.md)
