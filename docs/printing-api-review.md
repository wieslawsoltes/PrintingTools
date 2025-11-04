# Printing API Review Package (Draft)

## Overview
This package summarizes the proposed public API surface for PrintingTools as we align Avalonia printing with WPF semantics. It includes key types, usage examples, and outstanding questions for stakeholder review.

## Primary Types
- `IPrintManager`
  - `GetPrintersAsync(CancellationToken)`
  - `GetCapabilitiesAsync(PrinterId, PrintTicketModel?, CancellationToken)`
  - `RequestSessionAsync(PrintRequest, CancellationToken)`
  - `PrintAsync(PrintSession, CancellationToken)`
  - `CreatePreviewAsync(PrintSession, CancellationToken)`
- `PrintRequest`
  - Required `PrintDocument` payload plus optional ticket, options, and preferred printer.
- `PrintSession`
  - Exposes `Document`, `Options`, `Printer`, `Ticket`, `Capabilities`, lifecycle helpers.
- Capability models (`PrinterInfo`, `PrintTicketModel`, `PrintCapabilities`, `PageMediaSize`, enums). These map directly to WPF `PrintQueue`, `PrintTicket`, and `PrintCapabilities` concepts.
- `IPrintAdapter`
  - Platform bridge surface for native print capability discovery, session creation, printing, and preview.
- `PrintRenderPipeline`
  - Shared service that collects pages via the session paginator, normalizes DPI, renders bitmaps, and produces optional vector documents for preview/print adapters.

## Sample Usage
```csharp
// Service registration in AppBuilder
builder.UsePrintingTools(options =>
{
    options.EnablePreview = true;
    options.DefaultTicket.PageMediaSize = CommonPageMediaSizes.A4;
});

// View-model consumption
public class InvoiceViewModel
{
    private readonly IPrintManager _printManager;
    private readonly Visual _invoiceVisual;

    public InvoiceViewModel(IPrintManager printManager, Visual invoiceVisual)
    {
        _printManager = printManager;
        _invoiceVisual = invoiceVisual;
    }

    public async Task PrintAsync()
    {
        var document = PrintDocument.FromVisual(_invoiceVisual);
        var request = new PrintRequest(document)
        {
            Description = "Invoice",
            PreferredPrinterId = "macos-default"
        };

        var session = await _printManager.RequestSessionAsync(request);
        await _printManager.PrintAsync(session);
    }
}
```

## Migration Notes (WPF → Avalonia)
- Replace `System.Windows.Controls.PrintDialog` usage with `IPrintManager.RequestSessionAsync` + `PrintSession`.
- WPF `PrintQueue`/`PrintServer` map to `IPrintManager.GetPrintersAsync` and `PrinterInfo`.
- `PrintTicket` transitions to `PrintTicketModel`; capability negotiation mirrors `MergeAndValidatePrintTicket` via `PrintTicketModel.MergeWithCapabilities`.
- `DocumentPaginator` scenarios migrate to `PrintDocument`/`IPaginator`.

## Outstanding Questions
1. Do we expose synchronous helpers (`PrintSession.Submit()`) for simple workflows?
2. Should printer enumeration and capability calls be cached per adapter instance?
3. Do we require additional metadata (manufacturer, location) in `PrinterInfo` for parity with `PrintQueue`?
4. Is a `PrintPreviewRequest` abstraction necessary for background preview generation without `PrintSession`?
5. How do we version extension payloads carried in `PrintTicketModel.Extensions`?

## Next Steps
- Gather feedback during the 2024-07-24 stakeholder review.
- Finalize DI/XAML samples and integrate into developer documentation (`Phase 8`).
- Track answers to outstanding questions as GitHub issues linked from the roadmap.
