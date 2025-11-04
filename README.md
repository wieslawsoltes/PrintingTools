# PrintingTools

Cross-platform printing toolkit for .NET applications that need consistent dialogs, previews, and job submission on Windows, macOS, and Linux. The solution packages platform adapters, pagination helpers, diagnostics, and optional Avalonia UI so teams can ship feature-parity printing experiences without per-platform forks.

## Key Features
- Unified `IPrintManager` API that coordinates sessions, previews, print submissions, and capability discovery.
- Platform adapters for Win32/XPS, macOS AppKit, and Linux CUPS/GTK with managed fallback dialogs for headless runs.
- Built-in pagination, layout modes (standard, N-up, booklet, poster), vector/PDF export via Skia, and job diagnostics.
- Optional Avalonia UI components for page setup, preview windows, and native preview hosting on macOS.
- Harnesses and samples that exercise printing scenarios end-to-end, capture metrics, and integrate with CI.

## Architecture Overview
| Package | Purpose |
| --- | --- |
| `src/PrintingTools.Core` | Cross-platform contracts (`IPrintManager`, `PrintServiceRegistry`), pagination/rendering pipeline, diagnostics, and option models. |
| `src/PrintingTools.Windows` | Win32 adapter for queue discovery, XPS/PDF export, native dialog orchestration, and job monitoring. |
| `src/PrintingTools.MacOS` | AppKit bridge with preview hosting (`MacPreviewHost`), sandbox-aware ticket handling, and Quartz PDF output. |
| `src/PrintingTools.Linux` | CUPS/IPP adapter that shells through `lp`/`lpoptions`, detects GTK dialogs, and supports Flatpak/Snap portals. |
| `src/PrintingTools.UI` | Avalonia page setup dialog, preview window, and supporting view models. |
| `src/PrintingTools` | `AppBuilder.UsePrintingTools` extension that wires the right adapter at runtime and exposes helper accessors. |

The adapters register themselves through `PrintingToolsOptions.AdapterFactory`. Consumers call `PrintServiceRegistry.Configure` (or `AppBuilder.UsePrintingTools`) once during startup to hydrate the service registry, after which `PrintServiceRegistry.EnsureManager()` returns the active `IPrintManager`.

## Repository Layout
| Path | Description |
| --- | --- |
| `src/` | Library implementation projects described above. |
| `samples/` | Avalonia desktop sample and platform harnesses for Windows, macOS, and Linux. |
| `tests/PrintingTools.Tests` | Unit and integration tests plus harness metric assertions (`HarnessMetricsThresholdTests`). |
| `docs/` | Deep-dive design notes, platform guides, migration instructions, and feature parity tracking. |
| `exten/` | Vendored prerequisites used by the harnesses (Avalonia fork, WPF bridge). |

## Getting Started
### Prerequisites
- [.NET SDK 10.0.100-rc.2](global.json) (allow prerelease enabled).
- Windows: Win32 spooler available and XPS support enabled.  
  macOS: macOS 14+ with Xcode command line tools (for signing/notarisation of sandbox harness).  
  Linux: CUPS 2.3+, `cups-client` (`lp`, `lpoptions`), and GTK 3/4 libraries for native dialogs.
- Optional: Skia dependencies for hardware-accelerated PDF/vector export (installed automatically via Avalonia packages).

### Restore and Build
```bash
git clone https://github.com/your-org/PrintingTool.git
cd PrintingTool
dotnet restore PrintingTool.sln
dotnet build PrintingTool.sln
```

### Configure Printing in Your App
```csharp
using PrintingTools.Core;

var options = new PrintingToolsOptions
{
    DiagnosticSink = evt => Console.WriteLine($"[{evt.Category}] {evt.Message}")
    // AdapterFactory can be overridden to supply custom adapters or mocks.
};

PrintServiceRegistry.Configure(options);
var manager = PrintServiceRegistry.EnsureManager();
```

#### Avalonia applications
```csharp
using Avalonia;
using PrintingTools;
using PrintingTools.Core;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UsePrintingTools(options =>
    {
        options.EnablePreview = true;
        options.DefaultTicket = PrintTicketModel.CreateDefault();
    })
    .StartWithClassicDesktopLifetime(args);
```

### Request a Preview or Print Session
```csharp
var document = PrintDocument.FromVisual(myVisual);
var request = new PrintRequest(document)
{
    Options = new PrintOptions { ShowPrintDialog = true },
    Ticket = PrintTicketModel.CreateDefault(),
    Description = "Quarterly report"
};
var session = await manager.RequestSessionAsync(request);
var preview = await manager.CreatePreviewAsync(session);

await manager.PrintAsync(session);
```
The preview returns a `PrintPreviewModel` that you can render in the Avalonia `PrintPreviewWindow` or a native preview host (macOS). Session events (`PrintSession.JobStatusChanged`) surface job lifecycle updates; subscribe to relay progress to users or logging sinks.

## Samples and Harnesses
| Sample | Command | Highlights |
| --- | --- | --- |
| `samples/AvaloniaSample` | `dotnet run --project samples/AvaloniaSample/AvaloniaSample.csproj` | Desktop UI demonstrating preview, native dialogs, page setup, and job history. |
| `samples/WindowsPrintHarness` | `dotnet run --project samples/WindowsPrintHarness/WindowsPrintHarness.csproj -- --output=artifacts/windows/output.pdf --metrics=artifacts/windows/metrics.json --stress=3` | Headless smoke tests for Win32 adapter, managed PDF export, and metrics capture. |
| `samples/MacSandboxHarness` | `dotnet run --project samples/MacSandboxHarness/MacSandboxHarness.csproj -- --headless --output=artifacts/macos/output.pdf --metrics=artifacts/macos/metrics.json --stress=3` | Validates AppKit bridge in/out of sandbox, publishes Quartz PDF, logs entitlement diagnostics. |
| `samples/LinuxSandboxHarness` | `GTK_USE_PORTAL=1 GIO_USE_PORTALS=1 dotnet run --project samples/LinuxSandboxHarness/LinuxSandboxHarness.csproj -- --print --output=artifacts/linux/output.pdf --metrics=artifacts/linux/metrics.json --stress=3` | Exercises CUPS adapter, portal-aware dialogs, and produces PDF/metrics for CI. |

Harness outputs feed CI via `.github/workflows/printingtools-harness.yml`, and threshold enforcement lives in `tests/PrintingTools.Tests/Baselines/harness-thresholds.json`.

## Testing and Validation
- Run `dotnet test` to execute unit tests and baseline checks.
- Harness metrics are validated by `HarnessMetricsThresholdTests`; update `tests/PrintingTools.Tests/Baselines/harness-thresholds.json` when expected performance characteristics change.
- For manual regression, review the walkthroughs in [`docs/printing-sample-walkthroughs.md`](docs/printing-sample-walkthroughs.md).

## Diagnostics and Troubleshooting
- Register sinks with `PrintDiagnostics.RegisterSink` or the global `PrintingToolsOptions.DiagnosticSink` to capture warnings/errors from adapters.
- Inspect platform guides for environment-specific requirements:
  - [`docs/windows-printing-notes.md`](docs/windows-printing-notes.md)
  - [`docs/macos-printing-notes.md`](docs/macos-printing-notes.md)
  - [`docs/linux-printing-notes.md`](docs/linux-printing-notes.md)
- The platform support matrix (`docs/platform-support-matrix.md`) lists validated OS versions and packaging environments with known caveats.
- For sandbox packaging (Flatpak, Snap, notarised macOS apps), follow the dedicated harness guides under `docs/*-sandbox-harness.md`.

## Additional Documentation
- API surface: [`docs/printing-api-reference.md`](docs/printing-api-reference.md)
- Migration help: [`docs/printing-migration-guide.md`](docs/printing-migration-guide.md)
- Feature planning & parity: [`docs/wpf-printing-parity-plan.md`](docs/wpf-printing-parity-plan.md), [`docs/feature-parity-matrix.md`](docs/feature-parity-matrix.md)
- Architecture roadmap: [`docs/phase2-architecture.md`](docs/phase2-architecture.md)
- Rendering diagnostics and layout notes: [`docs/rendering-and-diagnostics.md`](docs/rendering-and-diagnostics.md)

## License
Distributed under the terms of the [MIT License](LICENSE).
