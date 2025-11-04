# Printing Migration Guide (WPF ➜ Avalonia)

This guide helps teams move existing WPF printing flows onto `PrintingTools` for Avalonia. It captures the common API mappings, upgrade steps, and behavioural differences you should be aware of before rolling out the MVP.

## 1. Quick Start Checklist
- **Target Frameworks:** Update your Avalonia application to .NET 8 or later; the printing packages are built against `net10.0`.
- **Package References:** Add `PrintingTools`, `PrintingTools.Windows` (Windows), `PrintingTools.MacOS` (macOS), and `PrintingTools.Linux` (Linux) to your project. Samples in `samples/*Harness` show per-platform wiring.
- **Service Registration:** Call `PrintingToolsAppBuilderExtensions.UsePrintingTools()` inside your `AppBuilder` configuration or wire `PrintServiceRegistry.Configure(...)` during startup.
- **Requesting a Session:** Replace `PrintDialog` calls with `IPrintManager.RequestSessionAsync(new PrintRequest(document))`.
- **Preview/Print:** Use `CreatePreviewAsync(session)` and `PrintAsync(session)` in place of `DocumentViewer.PrintCommand`.

## 2. API Mapping

| WPF concept | WPF API | Avalonia replacement | Notes |
| --- | --- | --- | --- |
| Print manager | `PrintQueue`, `PrintServer` | `IPrintManager`, `PrintServiceRegistry.EnsureManager()` | Manager resolves per-platform adapters via DI. |
| Print dialog | `System.Windows.Controls.PrintDialog` | `PrintOptions.ShowPrintDialog`, platform adapters | Windows uses `PrintDlgEx`, macOS uses `NSPrintPanel`, Linux bridges GTK or managed fallback. |
| Document pagination | `DocumentPaginator`, `FixedDocument` | `PrintDocument`, `PrintPage`, `PrintSession.Paginate()` | Convert Flow/Fixed content into Avalonia controls; custom paginators supported via `PrintSession.SetPaginator`. |
| Print tickets | `PrintTicket`, `MergeAndValidatePrintTicket` | `PrintTicketModel`, `PrintCapabilities` | Adapters merge ticket defaults with device capabilities, mirroring the WPF flow. |
| Capability discovery | `PrintQueue.GetPrintCapabilities` | `IPrintManager.GetCapabilitiesAsync(printerId)` | Windows inspects `DEVMODE`; macOS and Linux map to native attribute dictionaries. |
| Job progress | `PrintSystemJobInfo`, `PrintQueue.AddJob` events | `PrintSession.JobStatusChanged`, `PrintDiagnostics` | Subscribe for `Started`, `Completed`, `Failed`, and `Unknown` notifications. |
| Page setup | `PrintDialog.PrintableAreaWidth/Height`, `PageSetupDialog` | `PrintOptions` (paper, margins, layout kind), `PrintingTools.UI.PageSetupDialog` | Identical layout hints feed both preview and native adapters. |

## 3. Migration Steps by Scenario

1. **Replace Print Dialog Invocation**
   ```csharp
   // WPF
   var dialog = new PrintDialog();
   if (dialog.ShowDialog() == true) { ... }

   // Avalonia + PrintingTools
   var manager = PrintServiceRegistry.EnsureManager();
   var session = await manager.RequestSessionAsync(new PrintRequest(document)
   {
       Description = "Invoice Print",
       Options = new PrintOptions { ShowPrintDialog = true }
   });
   await manager.PrintAsync(session);
   ```

2. **FlowDocument to Avalonia Visuals**
   - Recreate FlowDocument templates as Avalonia `UserControl`/`Panel` trees.
   - Apply layout hints using `PrintLayoutHints.SetIsPrintable`, `PrintLayoutHints.SetPageBreakAfter`.
   - Use `PrintDocument.FromVisual(visual)` to convert the root control into printable pages.

3. **Custom Paginators**
   - If you previously derived from `DocumentPaginator`, create an `IPrintPaginator` implementation and plug it into `PrintSession.SetPaginator(paginator)`.
   - Use `PrintPaginationUtilities` for N-up/booklet/poster helpers.

4. **Print Ticket Extensions**
   - Map legacy vendor XML or custom properties into `PrintTicketModel.Extensions["vendor.key"]`.
   - Adapters will copy recognised extensions (e.g., `layout.*`, `macos.*`, `cups.*`) into native ticket payloads.

## 4. Behavioural Differences
- **Threading:** `PrintingTools` is async-first; await `RequestSessionAsync`, `CreatePreviewAsync`, and `PrintAsync`.
- **Sandbox Constraints:** macOS and Linux adapters honour sandbox portals/entitlements. Review `docs/macos-printing-notes.md` and `docs/linux-printing-notes.md` for prerequisites.
- **Vector vs Raster:** `PrintOptions.UseVectorRenderer` defaults to `true`. Toggle it if legacy drivers require raster payloads.
- **Selection Printing:** macOS exposes selection-only checkboxes through native accessory panels. Windows/Linux support is pending.
- **Diagnostics:** All adapters route issues through `PrintDiagnostics`. Configure `PrintingToolsOptions.DiagnosticSink` to hook existing logging frameworks.

## 5. Testing & Validation
- **Automated Metrics:** Run `dotnet test tests/PrintingTools.Tests/PrintingTools.Tests.csproj` to validate golden pagination metrics before shipping.
- **Harnesses:** Each `samples/*Harness` project exercises adapter-specific dialogs, PDF export, and logging. These harnesses now emit metrics JSON that the CI workflow validates.
- **Accessibility:** Ensure custom preview dialogs expose automation names; the harness metrics flag missing labels.

## 6. Resources
- Platform adoption notes: [`docs/windows-printing-notes.md`](windows-printing-notes.md), [`docs/macos-printing-notes.md`](macos-printing-notes.md), [`docs/linux-printing-notes.md`](linux-printing-notes.md)
- Diagnostics & rendering detail: [`docs/rendering-and-diagnostics.md`](rendering-and-diagnostics.md)
- API review snapshots: [`docs/printing-api-design-draft.md`](printing-api-design-draft.md), [`docs/printing-api-review.md`](printing-api-review.md)
- Sample walkthroughs: [`docs/printing-sample-walkthroughs.md`](printing-sample-walkthroughs.md)

For questions or to request additional parity items, log tickets against Phase 9 in `docs/wpf-printing-parity-plan.md`.
