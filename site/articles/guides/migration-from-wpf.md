---
title: "Migration from WPF"
---

# Migration from WPF

PrintingTools is designed to replace the usual WPF printing stack with an Avalonia-friendly, cross-platform API.

## Common mappings

| WPF concept | PrintingTools replacement |
| --- | --- |
| `PrintDialog` | `PrintOptions.ShowPrintDialog` plus the active platform adapter |
| `PrintQueue` / `PrintServer` | `IPrintManager` and `GetPrintersAsync()` |
| `PrintTicket` | <xref:PrintingTools.Core.PrintTicketModel> |
| `DocumentPaginator` | <xref:PrintingTools.Core.Pagination.IPrintPaginator> |
| `FixedDocument` / `FlowDocument` print path | <xref:PrintingTools.Core.PrintDocument> plus Avalonia visuals or custom enumerators |
| `DocumentViewer` preview flow | <xref:PrintingTools.Core.PrintPreviewModel> plus `PrintingTools.UI` or native preview hosts |

## Migration sequence

1. Replace direct dialog invocation with `RequestSessionAsync`.
2. Convert printable WPF content into Avalonia visuals or page enumerators.
3. Map print-ticket concepts into `PrintTicketModel` and `PrintOptions`.
4. Move preview logic to `CreatePreviewAsync`.
5. Route diagnostics into your normal application logging.

## Behavior differences to expect

- PrintingTools is async-first.
- Capabilities are negotiated per session instead of being read from static dialog state.
- Linux and macOS flows can run in headless or sandboxed environments that WPF never targeted.
- Vector vs raster output is an explicit runtime choice.

## Related

- [Quickstart Avalonia](../getting-started/quickstart-avalonia.md)
- [Windows](../platforms/windows.md)
- [macOS](../platforms/macos.md)
- [Linux](../platforms/linux.md)
