# PrintingTools

Cross-platform .NET 10 printing toolkit for applications that need consistent print dialogs, previews, pagination, and job submission across Windows, macOS, and Linux.

[![CI](https://github.com/wieslawsoltes/PrintingTools/actions/workflows/ci.yml/badge.svg)](https://github.com/wieslawsoltes/PrintingTools/actions/workflows/ci.yml)
[![Release](https://github.com/wieslawsoltes/PrintingTools/actions/workflows/release.yml/badge.svg)](https://github.com/wieslawsoltes/PrintingTools/actions/workflows/release.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.x-8b44ac)](https://avaloniaui.net)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/wieslawsoltes/PrintingTools/blob/main/LICENSE)

## NuGet Packages

### Primary Packages

| Package | NuGet | Description |
| --- | --- | --- |
| **PrintingTools** | [![NuGet](https://img.shields.io/nuget/v/PrintingTools.svg)](https://www.nuget.org/packages/PrintingTools) | Avalonia bootstrapper that wires the platform adapter at startup and exposes `UsePrintingTools` helpers. |
| **PrintingTools.Core** | [![NuGet](https://img.shields.io/nuget/v/PrintingTools.Core.svg)](https://www.nuget.org/packages/PrintingTools.Core) | Cross-platform contracts, pagination/rendering pipeline, diagnostics, and option models. |
| **PrintingTools.UI** | [![NuGet](https://img.shields.io/nuget/v/PrintingTools.UI.svg)](https://www.nuget.org/packages/PrintingTools.UI) | Reusable Avalonia dialogs and preview UI for page setup and print preview scenarios. |
| **PrintingTools.Windows** | [![NuGet](https://img.shields.io/nuget/v/PrintingTools.Windows.svg)](https://www.nuget.org/packages/PrintingTools.Windows) | Windows adapter with Win32 queue discovery, capability inspection, and XPS/PDF rendering flows. |
| **PrintingTools.MacOS** | [![NuGet](https://img.shields.io/nuget/v/PrintingTools.MacOS.svg)](https://www.nuget.org/packages/PrintingTools.MacOS) | macOS adapter with AppKit integration, Quartz PDF output, and a universal native preview bridge. |
| **PrintingTools.Linux** | [![NuGet](https://img.shields.io/nuget/v/PrintingTools.Linux.svg)](https://www.nuget.org/packages/PrintingTools.Linux) | Linux CUPS/GTK adapter with portal-aware dialog support and managed PDF submission. |

## Key Features

- Unified `IPrintManager` API for printer discovery, session creation, preview generation, and print submission.
- Platform adapters for Win32/XPS, macOS AppKit, and Linux CUPS/GTK with managed fallbacks for headless and CI runs.
- Built-in pagination, layout modes (`standard`, `N-up`, `booklet`, `poster`), vector/PDF export via Skia, and diagnostics hooks.
- Optional Avalonia UI components for page setup, preview windows, and macOS native preview hosting.
- Cross-platform harnesses and samples that validate output, metrics, and environment-specific behavior in CI.

## Package Selection

| Scenario | Recommended package set |
| --- | --- |
| Avalonia app that wants automatic platform registration | `PrintingTools` |
| Avalonia app that also wants ready-made dialogs and preview windows | `PrintingTools` + `PrintingTools.UI` |
| Custom UI or headless workflow with direct control over the pipeline | `PrintingTools.Core` + one or more platform packages |
| Platform-specific integration or diagnostics work | `PrintingTools.Windows`, `PrintingTools.MacOS`, or `PrintingTools.Linux` directly |

## Architecture Overview

| Package | Purpose |
| --- | --- |
| `src/PrintingTools.Core` | Cross-platform contracts (`IPrintManager`, `PrintServiceRegistry`), pagination/rendering pipeline, diagnostics, and option models. |
| `src/PrintingTools.Windows` | Win32 adapter for queue discovery, XPS/PDF export, native dialog orchestration, and job monitoring. |
| `src/PrintingTools.MacOS` | AppKit bridge with preview hosting (`MacPreviewHost`), sandbox-aware ticket handling, and Quartz PDF output. |
| `src/PrintingTools.Linux` | CUPS/IPP adapter that shells through `lp`/`lpoptions`, detects GTK dialogs, and supports Flatpak/Snap portals. |
| `src/PrintingTools.UI` | Avalonia page setup dialog, preview window, and supporting view models. |
| `src/PrintingTools` | `AppBuilder.UsePrintingTools` extension that wires the right adapter at runtime and exposes helper accessors. |

The adapters register through `PrintingToolsOptions.AdapterFactory`. Configure `PrintServiceRegistry` once during startup, or call `AppBuilder.UsePrintingTools(...)` in Avalonia, and then resolve the active `IPrintManager` through `PrintServiceRegistry.EnsureManager()`.

## Repository Layout

| Path | Description |
| --- | --- |
| `src/` | Library implementation projects described above. |
| `samples/` | Avalonia desktop sample and platform harnesses for Windows, macOS, and Linux. |
| `tests/PrintingTools.Tests` | Unit tests and harness metric threshold validation. |
| `docs/` | Design notes, platform guides, migration help, release-readiness notes, and feature parity tracking. |
| `exten/` | Vendored prerequisites used by the samples and research branches. |

## Getting Started

### Prerequisites

- [.NET SDK 10.0.100-rc.2](https://github.com/wieslawsoltes/PrintingTools/blob/main/global.json) with prerelease support enabled.
- Windows: Win32 spooler available and XPS support enabled.
- macOS: macOS 14+ with Xcode command line tools for building the native preview bridge.
- Linux: CUPS 2.3+, `cups-client` (`lp`, `lpoptions`), and GTK 3/4 libraries for native dialog integration.

### Install Packages

For a typical Avalonia app:

```bash
dotnet add package PrintingTools
dotnet add package PrintingTools.UI
```

For direct pipeline usage without the packaged UI:

```bash
dotnet add package PrintingTools.Core
dotnet add package PrintingTools.Windows
```

Swap the platform package as needed for macOS or Linux.

### Restore and Build

```bash
git clone https://github.com/wieslawsoltes/PrintingTools.git
cd PrintingTools
dotnet restore PrintingTool.sln
dotnet build PrintingTool.sln -c Release
```

### Configure Printing in Your App

```csharp
using PrintingTools.Core;

var options = new PrintingToolsOptions
{
    DiagnosticSink = evt => Console.WriteLine($"[{evt.Category}] {evt.Message}")
};

PrintServiceRegistry.Configure(options);
var manager = PrintServiceRegistry.EnsureManager();
```

### Avalonia Applications

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

`CreatePreviewAsync(...)` returns a `PrintPreviewModel` that you can render in `PrintingTools.UI.Controls.PrintPreviewWindow` or a platform-specific preview host.

## Samples and Harnesses

| Sample | Command | Highlights |
| --- | --- | --- |
| `samples/AvaloniaSample` | `dotnet run --project samples/AvaloniaSample/AvaloniaSample.csproj` | Desktop UI demonstrating preview, native dialogs, page setup, and job history. |
| `samples/WindowsPrintHarness` | `dotnet run -c Release --project samples/WindowsPrintHarness/WindowsPrintHarness.csproj -- --output=artifacts/windows/output.pdf --metrics=artifacts/windows/metrics.json --stress=3` | Headless smoke tests for the Win32 adapter, managed PDF export, and metrics capture. |
| `samples/MacSandboxHarness` | `dotnet run -c Release --project samples/MacSandboxHarness/MacSandboxHarness.csproj -- --headless --output=artifacts/macos/output.pdf --metrics=artifacts/macos/metrics.json --stress=3` | Validates AppKit bridge behavior, Quartz PDF output, and sandbox-aware diagnostics. |
| `samples/LinuxSandboxHarness` | `GTK_USE_PORTAL=1 GIO_USE_PORTALS=1 dotnet run -c Release --project samples/LinuxSandboxHarness/LinuxSandboxHarness.csproj -- --print --output=artifacts/linux/output.pdf --metrics=artifacts/linux/metrics.json --stress=3` | Exercises the CUPS adapter, portal-aware dialogs, and managed PDF generation for CI. |

## Documentation

- API reference: [docs/printing-api-reference.md](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/printing-api-reference.md)
- Migration guide: [docs/printing-migration-guide.md](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/printing-migration-guide.md)
- Platform notes: [Windows](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/windows-printing-notes.md), [macOS](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/macos-printing-notes.md), [Linux](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/linux-printing-notes.md)
- Platform support matrix: [docs/platform-support-matrix.md](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/platform-support-matrix.md)
- Sample walkthroughs: [docs/printing-sample-walkthroughs.md](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/printing-sample-walkthroughs.md)
- Architecture and planning notes: [phase2-architecture](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/phase2-architecture.md), [feature-parity-matrix](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/feature-parity-matrix.md), [wpf-printing-parity-plan](https://github.com/wieslawsoltes/PrintingTools/blob/main/docs/wpf-printing-parity-plan.md)

## Release and CI

- [`ci.yml`](https://github.com/wieslawsoltes/PrintingTools/blob/main/.github/workflows/ci.yml) runs cross-platform restore/build/test, executes the Windows/macOS/Linux harnesses, and validates that all NuGet packages can be packed.
- [`release.yml`](https://github.com/wieslawsoltes/PrintingTools/blob/main/.github/workflows/release.yml) runs on tags like `v0.2.0`, packs all six publishable libraries, verifies the macOS native bridge is present in the package, publishes to NuGet, and creates a GitHub release.
- Configure `NUGET_API_KEY` in repository secrets before using the release workflow for publishing.

## License

Distributed under the terms of the [MIT License](https://github.com/wieslawsoltes/PrintingTools/blob/main/LICENSE).
