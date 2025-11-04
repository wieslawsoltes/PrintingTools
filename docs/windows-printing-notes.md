# Windows Printing Adapter Adoption Notes

These notes summarise how to onboard the Windows adapter that ships with `PrintingTools.Windows`. Use them alongside the migration guide and API reference when rolling out the MVP.

## 1. Supported Targets
- **Operating system:** Windows 10 1809 or later (Win32 print APIs). Windows Server 2019+ is supported for desktop workloads.
- **Framework:** .NET 8 or later (the binaries compile against `net10.0`).
- **Architecture:** x64 and ARM64. Ensure matching Skia dependencies are available if you rely on vector export.

## 2. Installation & Wiring
1. Add the package:
   ```xml
   <PackageReference Include="PrintingTools.Windows" Version="1.0.0-preview" />
   ```
   The package brings in `PrintingTools.Core` automatically.
2. Register services on startup:
   ```csharp
   PrintingToolsAppBuilderExtensions.UsePrintingTools(appBuilder);
   var options = new PrintingToolsOptions
   {
       AdapterFactory = () => new Win32PrintAdapterFactory().CreateAdapter(),
       DiagnosticSink = evt => Logger.LogInformation("[Printing] {Category}: {Message}", evt.Category, evt.Message)
   };
   PrintServiceRegistry.Configure(options);
   ```
3. Request a session via `IPrintManager` (`PrintServiceRegistry.EnsureManager()`).

## 3. Feature Snapshot

| Capability | Status |
| --- | --- |
| Printer enumeration (`EnumPrinters`) | ✅ |
| Capability discovery via `DEVMODE` | ✅ |
| Native dialog (`PrintDlgEx`) | ✅ (`PrintOptions.ShowPrintDialog = true`) |
| XPS submission (`StartDocPrinter` + `WritePrinter`) | ✅ |
| Managed PDF export | ✅ (Skia) |
| Raster fallback (`RenderTargetBitmap`) | ✅ (`PrintOptions.UseVectorRenderer = false`) |
| Job monitoring (`FindFirstPrinterChangeNotification`, `GetJob`) | ✅ |
| Ticket merge (`PrintTicketModel.MergeWithCapabilities`) | ✅ |
| Selection printing | ⏳ (planned post-MVP) |

## 4. Runtime Behaviour
- **Vector vs raster:** Jobs default to vector XPS payloads. Force raster by clearing `UseVectorRenderer`. Vector payloads produce smaller spool files and honour N-up/booklet/poster metadata via `LayoutMetadata`.
- **Capability merge:** After `RequestSessionAsync`, the adapter fetches device capabilities and merges them into the `PrintTicketModel`, emitting warnings when the driver downgrades settings (e.g., unsupported duplex).
- **Diagnostics:** Exceptions and error codes from the Win32 API are routed through `PrintDiagnostics` with a `"Win32PrintAdapter"` category. Surface them in your logging pipeline to troubleshoot driver issues.
- **Job tracking:** After submission the adapter registers a change notification handle. If the driver does not support notifications, it falls back to polling once per second for 60 seconds. Extend the timeout through `PrintOptions.Extensions["windows.monitor.timeout"]` if needed.

## 5. Deployment Considerations
- **App containerisation:** Store apps must broker print access via the Windows print contract; `PrintingTools.Windows` targets classic desktop applications. UWP/WinUI 3 support is out of scope.
- **Dependencies:** No admin rights are required. Ensure `PrintingToolsMacBridge` is not inadvertently packaged on Windows installers to avoid size bloat.
- **PDF output:** Setting `PrintOptions.PdfOutputPath` writes managed PDF/XPS files before (or instead of) spooling. Combine with automated verification to compare golden outputs.

## 6. Troubleshooting
| Symptom | Suggested action |
| --- | --- |
| `OpenPrinter` fails with access denied | Verify the user has permission on the target queue; check group policies blocking shared printers. |
| Native dialog returns immediately | Confirm the process hosts a top-level window (CI sets `ShowPrintDialog = false`). |
| Spool job completes with blank pages | Toggle `UseVectorRenderer`; some legacy drivers mishandle vector heavy XPS payloads. |
| Capabilities missing duplex/color | Confirm the queue’s `DEVMODE` exposes those fields; some virtual printers drop optional fields until the dialog is shown once. |

## 7. References
- Samples: [`samples/WindowsPrintHarness`](../samples/WindowsPrintHarness)
- API overview: [`docs/printing-api-reference.md`](printing-api-reference.md)
- Migration guidance: [`docs/printing-migration-guide.md`](printing-migration-guide.md)
- Diagnostics cheat sheet: [`docs/rendering-and-diagnostics.md`](rendering-and-diagnostics.md)
