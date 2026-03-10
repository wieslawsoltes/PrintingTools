---
title: "API Coverage Index"
---

# API Coverage Index

This index maps the public API surface to the main narrative documentation layers.

## Coverage summary

- API coverage spans all six packable assemblies and their public namespaces.
- Narrative docs focus on the workflow-level entry points that most consumers start from.
- generated API entry point: [API Documentation](../../api/index.md)

## Narrative entry points

- [Namespace: PrintingTools.Core](namespace-printingtools-core.md)
- [Namespace: Platform Packages](namespace-printingtools-platforms.md)
- [Namespace: PrintingTools.UI](namespace-printingtools-ui.md)

## Primary API-to-article map

| Area | Main types | Primary article |
| --- | --- | --- |
| Startup and registry | <xref:PrintingTools.PrintingToolsAppBuilderExtensions>, <xref:PrintingTools.Core.PrintServiceRegistry>, <xref:PrintingTools.Core.PrintingToolsOptions> | [concepts/architecture-and-service-model.md](../concepts/architecture-and-service-model.md) |
| Sessions and requests | <xref:PrintingTools.Core.IPrintManager>, <xref:PrintingTools.Core.PrintRequest>, <xref:PrintingTools.Core.PrintSession>, <xref:PrintingTools.Core.PrintDocument> | [concepts/print-session-lifecycle.md](../concepts/print-session-lifecycle.md) |
| Tickets and capabilities | <xref:PrintingTools.Core.PrintTicketModel>, <xref:PrintingTools.Core.PrintCapabilities>, <xref:PrintingTools.Core.PrinterInfo> | [concepts/pagination-and-layout-model.md](../concepts/pagination-and-layout-model.md) |
| Pagination and layout | <xref:PrintingTools.Core.Pagination.IPrintPaginator>, <xref:PrintingTools.Core.Pagination.DefaultPrintPaginator>, <xref:PrintingTools.Core.PrintLayoutHints> | [concepts/pagination-and-layout-model.md](../concepts/pagination-and-layout-model.md) |
| Preview and rendering | <xref:PrintingTools.Core.PrintPreviewModel>, <xref:PrintingTools.Core.Rendering.PrintRenderPipeline>, <xref:PrintingTools.Core.Rendering.IVectorPageRenderer> | [concepts/previews-rendering-and-diagnostics.md](../concepts/previews-rendering-and-diagnostics.md) |
| UI | <xref:PrintingTools.UI.Controls.PageSetupWindow>, <xref:PrintingTools.UI.Controls.PrintPreviewWindow>, <xref:PrintingTools.UI.ViewModels.PrintPreviewViewModel> | [guides/page-setup-and-preview-ui.md](../guides/page-setup-and-preview-ui.md) |
| Windows adapter | <xref:PrintingTools.Windows.Win32PrintAdapterFactory> | [platforms/windows.md](../platforms/windows.md) |
| macOS adapter | <xref:PrintingTools.MacOS.MacPrintAdapter>, <xref:PrintingTools.MacOS.MacPrintAdapterFactory>, <xref:PrintingTools.MacOS.Preview.MacPreviewHost> | [platforms/macos.md](../platforms/macos.md) |
| Linux adapter | <xref:PrintingTools.Linux.LinuxPrintAdapter>, <xref:PrintingTools.Linux.LinuxPrintAdapterFactory> | [platforms/linux.md](../platforms/linux.md) |

## Notes

- Generated API pages are the authoritative source for member-level documentation.
- The narrative docs focus on workflow, architecture, and platform behavior.
