---
title: "Print Session Lifecycle"
---

# Print Session Lifecycle

The central unit of work in PrintingTools is <xref:PrintingTools.Core.PrintSession>.

## Lifecycle stages

1. Build a <xref:PrintingTools.Core.PrintDocument>.
2. Wrap it in a <xref:PrintingTools.Core.PrintRequest>.
3. Call `RequestSessionAsync` on <xref:PrintingTools.Core.IPrintManager>.
4. Optionally generate a <xref:PrintingTools.Core.PrintPreviewModel>.
5. Print, export, or both.

## Session contents

A session captures:

- the source document
- the active <xref:PrintingTools.Core.PrintOptions>
- the merged <xref:PrintingTools.Core.PrintTicketModel>
- the selected printer and resolved capabilities
- lifecycle notifications through `JobStatusChanged`

## Why capabilities are attached to the session

Capabilities depend on the selected device. By resolving them during session creation, PrintingTools can merge requested settings with what the target printer actually supports before the preview or print step executes.

## Preview path

`CreatePreviewAsync` produces <xref:PrintingTools.Core.PrintPreviewModel> from the same session that will be printed. That is important because preview and final output should share the same pagination, ticket data, and rendering options.

## Print path

`PrintAsync` delegates to the active adapter:

- Windows can submit XPS or managed PDF-backed jobs.
- macOS can stream PDF payloads into `NSPrintOperation`.
- Linux shells through CUPS utilities and optionally opens GTK or managed dialogs.

## Related

- [Pagination and Layout Model](pagination-and-layout-model.md)
- [Previews, Rendering, and Diagnostics](previews-rendering-and-diagnostics.md)
