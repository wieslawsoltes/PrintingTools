---
title: "Samples and Harnesses"
---

# Samples and Harnesses

The repository ships both an interactive Avalonia sample and platform-specific harnesses used in CI.

## Sample inventory

| Project | Purpose | Command |
| --- | --- | --- |
| `samples/AvaloniaSample` | End-user style preview, page setup, and print UX. | `dotnet run --project samples/AvaloniaSample/AvaloniaSample.csproj` |
| `samples/WindowsPrintHarness` | Headless Win32 adapter validation, PDF output, and metrics capture. | `dotnet run -c Release --project samples/WindowsPrintHarness/WindowsPrintHarness.csproj -- --output=artifacts/windows/output.pdf --metrics=artifacts/windows/metrics.json --stress=3` |
| `samples/MacSandboxHarness` | AppKit bridge validation in headless and UI-capable modes. | `dotnet run -c Release --project samples/MacSandboxHarness/MacSandboxHarness.csproj -- --headless --output=artifacts/macos/output.pdf --metrics=artifacts/macos/metrics.json --stress=3` |
| `samples/LinuxSandboxHarness` | CUPS, GTK, and portal-aware Linux validation. | `GTK_USE_PORTAL=1 GIO_USE_PORTALS=1 dotnet run -c Release --project samples/LinuxSandboxHarness/LinuxSandboxHarness.csproj -- --print --output=artifacts/linux/output.pdf --metrics=artifacts/linux/metrics.json --stress=3` |

## What the harnesses validate

- printer enumeration and capability lookup
- session creation and pagination
- preview or export output
- performance and memory thresholds
- accessibility label coverage for packaged UI

## Where results go

Each harness emits:

- a generated PDF under `artifacts/<platform>/`
- a metrics JSON file consumed by `HarnessMetricsThresholdTests`
- a log file uploaded by GitHub Actions

## CI integration

`.github/workflows/ci.yml` runs the three harnesses on their native runners and then executes the threshold tests against the emitted metrics.

## Related

- [Harness Baselines and Golden Metrics](../advanced/harness-baselines-and-golden-metrics.md)
- [Support Matrix](../platforms/support-matrix.md)
