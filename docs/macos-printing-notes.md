# macOS Printing Adapter Adoption Notes

The macOS adapter integrates Avalonia with AppKit print infrastructure through an Objective-C bridge. Use this guide to configure entitlements, present native dialogs, and diagnose issues.

## 1. Supported Targets
- **Operating system:** macOS 12 Monterey or later (Intel and Apple Silicon tested up to macOS 14).
- **Framework:** .NET 8 or later with Avalonia 11.
- **Sandbox readiness:** Works in both sandboxed and unsandboxed apps. Sandboxed apps must supply `com.apple.security.print` and `com.apple.security.files.user-selected.read-write` where file exports are saved.

## 2. Installation & Wiring
```xml
<PackageReference Include="PrintingTools.MacOS" Version="1.0.0-preview" />
```

```csharp
var options = new PrintingToolsOptions
{
    AdapterFactory = () => new MacPrintAdapter(),
    DiagnosticSink = evt => Console.WriteLine($"[macOS] {evt.Category}: {evt.Message}")
};
PrintServiceRegistry.Configure(options);
```

The adapter exposes native preview hosting via `MacPreviewHost` and surfaces native dialog flows through `PrintOptions.ShowPrintDialog` / `ShowPageLayoutDialog`.

## 3. Feature Snapshot

| Capability | Status |
| --- | --- |
| Printer discovery (`NSPrinter printerNames`) | ✅ |
| Capability discovery (`NSPrintInfo`, dictionaries) | ✅ |
| Native print panel (`NSPrintPanel`) | ✅ (`ShowPrintDialog = true`)
| Page layout sheet (`NSPageLayout`) | ✅ (`ShowPageLayoutDialog = true`)
| Managed preview host (`MacPreviewHost`) | ✅ |
| Vector PDF submission (`PrintingToolsInterop.RunPdfPrintOperation`) | ✅ |
| Managed PDF export | ✅ (`PrintOptions.PdfOutputPath` or `UseManagedPdfExporter`)
| Sandbox logging | ✅ (container ID, entitlement summary)
| Selection-only printing | ✅ (accessory view toggles `PrintOptions.SelectionOnlyRequested`)

## 4. Sandbox & Entitlements
- **Required keys:** `com.apple.security.print`, `com.apple.security.files.user-selected.read-write`, and `com.apple.security.assets.movies.read-write` if PDF exports target non-user folders.
- **Temporary files:** The adapter writes preview PDFs to app-specific temporary locations. Ensure `NSTemporaryDirectory()` is writable.
- **Portal diagnostics:** The harness prints entitlement info (`LogSandboxContext`). Refer to `docs/macos-sandbox-harness.md` for codesign/notarisation walkthroughs.

## 5. Runtime Behaviour
- Native dialogs run on the main thread; the adapter marshals calls via GCD to ensure UI safety.
- `MacPrintAdapter.ShowNativePrintPanel` synchronises `PrintOptions` and `PrintTicketModel` with user selections, including paper sizes, copies, duplex, and selection ranges.
- Vector rendering uses `SkiaVectorPageRenderer` to emit PDF bytes that are streamed into `NSPrintOperation`. When `UseManagedPdfExporter` is true, the adapter saves the PDF and optionally re-opens the panel.
- Accessibility overlays reuse the AppKit preview; add automation names to managed preview controls when embedding Avalonia UI.

## 6. Troubleshooting
| Symptom | Action |
| --- | --- |
| Print panel closes immediately | Verify `ShowPrintDialog` is true and the Avalonia window provides a valid native handle (headless flows skip the panel). |
| PDF export fails in sandbox | Confirm the export path is user-selected or lies within an allowed container (e.g., `~/Library/Containers/<bundle>/Data`). |
| Missing printers | `NSPrinter printerNames` only lists system printers. Check System Settings > Printers & Scanners and confirm the app has print entitlement enabled. |
| Crash with `Trace/BPT trap: 5` | Happens when running unsigned binaries in hardened runtime; sign/notarise binaries per `docs/macos-sandbox-harness.md`. |

## 7. References
- Harness walkthrough: [`docs/printing-sample-walkthroughs.md`](printing-sample-walkthroughs.md)
- API reference: [`docs/printing-api-reference.md`](printing-api-reference.md)
- Diagnostics: [`docs/rendering-and-diagnostics.md`](rendering-and-diagnostics.md)
- Sandbox checklist: [`docs/macos-sandbox-harness.md`](macos-sandbox-harness.md)
