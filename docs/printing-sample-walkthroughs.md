# Printing Sample Walkthroughs

Use these walkthroughs to verify the MVP end-to-end and to train teams adopting the new APIs.

## 1. Avalonia Sample App (`samples/AvaloniaSample`)
1. **Run the sample**
   ```bash
   dotnet run --project samples/AvaloniaSample/AvaloniaSample.csproj
   ```
2. **Explore scenarios**
   - **Preview**: Opens the Avalonia preview window (`PrintPreviewWindow`) and generates a `PrintPreviewModel`.
   - **Native Preview**: Invokes platform-specific dialogs via the adapter (macOS sheet, Windows `PrintDlgEx`, Linux GTK dialog when available).
   - **Page Setup**: Launches the `PageSetupDialog` control and applies changes back to the active `PrintOptions`.
   - **Job History**: Listen to `PrintSession.JobStatusChanged` events; the sample renders these in the UI for quick diagnostics.
3. **Review code**
   - `MainWindow.axaml.cs` demonstrates resolving `IPrintManager`, creating sessions, and wiring `PrintDiagnostics`.
   - `ViewModels/` contains preview view models and data binding patterns you can copy into your application.

## 2. Windows Harness (`samples/WindowsPrintHarness`)
Purpose: Headless smoke tests for queue discovery, native dialog opt-out, PDF export, and metrics capture.

```powershell
dotnet run --project samples/WindowsPrintHarness/WindowsPrintHarness.csproj `
    -- --output=artifacts/windows/printingtools-windows.pdf `
       --metrics=artifacts/windows/metrics.json `
       --stress=3
```

- **What it tests**
  - Enumerates printers via the Win32 adapter and logs attributes.
  - Generates managed PDF output by default; still submits a print job to the selected queue.
  - Measures session creation, preview timings, print duration, peak memory, and accessibility label coverage.
- **Where metrics go**
  - `artifacts/windows/metrics.json` is consumed by the CI validation test (`HarnessMetricsThresholdTests`).

## 3. macOS Harness (`samples/MacSandboxHarness`)
Purpose: Validate AppKit bridge behaviour in and outside sandboxed environments.

```bash
dotnet run --project samples/MacSandboxHarness/MacSandboxHarness.csproj \
    -- --headless \
       --output=artifacts/macos/printingtools-macos.pdf \
       --metrics=artifacts/macos/metrics.json \
       --stress=3
```

- **Headless vs UI mode**: Pass `--headless` (default in CI) to skip native dialogs and ensure automation runs unattended. Omit it locally to open the AppKit print panel.
- **Sandbox guidance**: The harness logs entitlement and environment details; refer to `docs/macos-sandbox-harness.md` for signing/notarisation steps.
- **Outputs**: Managed PDF, metrics JSON, and log files are produced under `artifacts/macos/`.

## 4. Linux Harness (`samples/LinuxSandboxHarness`)
Purpose: Exercise the CUPS adapter across desktop environments and sandbox modes.

```bash
GTK_USE_PORTAL=1 GIO_USE_PORTALS=1 \
dotnet run --project samples/LinuxSandboxHarness/LinuxSandboxHarness.csproj \
    -- --print \
       --output=artifacts/linux/printingtools-linux.pdf \
       --metrics=artifacts/linux/metrics.json \
       --stress=3
```

- **Dialog logic**: Prefers GTK native dialogs; falls back to the managed Avalonia dialog when GTK is absent or running headless.
- **Portal awareness**: Environment variables above route interactions through Flatpak/Snap portals where required.
- **Metrics**: Includes stress iteration timing and accessibility counts similar to the Windows harness.

## 5. CI Workflow Integration
- `.github/workflows/printingtools-harness.yml` runs each harness per platform, uploads generated PDFs/logs, and executes `dotnet test` with platform-specific environment variables (`HARNESS_METRICS_PATH`, `HARNESS_PLATFORM`).
- Threshold enforcement lives in `tests/PrintingTools.Tests/Baselines/harness-thresholds.json`. Adjust these values when legitimate performance changes are introduced.

## 6. Extending the Samples
- **Add new scenarios**: Append to `ValidationScenarios.All` to capture additional layouts. Regenerate golden hashes via `dotnet test` and update `Baselines/golden-metrics.json`.
- **Custom metrics**: The harnesses structure their payloads via serializable classes—augment them with new fields (e.g., GPU usage) and extend `HarnessMetricsThresholdTests` as needed.
- **Localization & Accessibility**: Reuse the sample automation checks (`AnalyzeAccessibility`) when integrating into your own preview windows to catch missing automation names early.

For step-by-step migration guidance, consult [`docs/printing-migration-guide.md`](printing-migration-guide.md). Platform-specific notes live alongside this document and should be reviewed before distributing builds.
