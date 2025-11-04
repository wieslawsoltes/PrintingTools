# Harness Baseline Maintenance Guide

This guide explains how to inspect and refresh the printing harness baselines that guard the Windows, macOS, and Linux adapters. Follow these steps whenever rendering behaviour changes or performance characteristics shift.

## 1. When to Update
- You modified rendering, pagination, layout, or adapter logic that affects preview/print output.
- CI reports a metrics deviation (`HarnessMetricsThresholdTests` failure) or golden hash mismatch (`GoldenMetricsTests` failure).
- You intentionally change acceptable performance thresholds (e.g., slower but more accurate rendering).

## 2. Collect Harness Metrics

Run each platform harness to generate fresh metrics and artefacts. Replace `<repo>` with the solution root.

### Windows
```powershell
dotnet run --project samples/WindowsPrintHarness/WindowsPrintHarness.csproj `
    -- --output=artifacts/windows/printingtools-windows.pdf `
       --metrics=artifacts/windows/metrics.json `
       --stress=3
```

### macOS
```bash
dotnet run --project samples/MacSandboxHarness/MacSandboxHarness.csproj \
    -- --headless \
       --output=artifacts/macos/printingtools-macos.pdf \
       --metrics=artifacts/macos/metrics.json \
       --stress=3
```

### Linux
```bash
GTK_USE_PORTAL=1 GIO_USE_PORTALS=1 \
dotnet run --project samples/LinuxSandboxHarness/LinuxSandboxHarness.csproj \
    -- --print \
       --output=artifacts/linux/printingtools-linux.pdf \
       --metrics=artifacts/linux/metrics.json \
       --stress=3
```

Each command produces a metrics JSON file and the managed PDF artefact used in CI comparisons.

## 3. Update Golden Metrics
1. Run the validation tests to see the new hashes:
   ```bash
   dotnet test tests/PrintingTools.Tests/PrintingTools.Tests.csproj
   ```
   Failing tests log the computed hash values in the console output.
2. Edit `tests/PrintingTools.Tests/Baselines/golden-metrics.json` and replace the `metricsHash` (and page counts if they changed) with the new values surfaced by the test run.
3. Re-run `dotnet test` to confirm the `GoldenMetricsTests` pass.

## 4. Adjust Harness Thresholds (if required)
The file `tests/PrintingTools.Tests/Baselines/harness-thresholds.json` governs allowable timing, memory, and accessibility limits.

- Increase thresholds only when a deliberate regression is accepted. Document the rationale in the PR description.
- Decrease thresholds when performance improves, to keep the guardrails tight.
- After editing the JSON, run `dotnet test` again to ensure `HarnessMetricsThresholdTests` accept the new bounds.

## 5. Review & Publish Artefacts
- Inspect the generated PDFs under `artifacts/<platform>/` to confirm visual output looks correct.
- Keep the metrics JSON files in the PR for auditability (CI uploads them as build artefacts but does not commit them).

## 6. Pull Request Checklist
- [ ] Harness commands run for each affected platform.
- [ ] `golden-metrics.json` hashes updated (if applicable).
- [ ] `harness-thresholds.json` adjusted with justification (when needed).
- [ ] `dotnet test tests/PrintingTools.Tests/PrintingTools.Tests.csproj` passes locally.
- [ ] PR description summarises metrics deltas and links to any supporting screenshots.

Maintaining these baselines ensures the printing MVP remains stable across adapters and that regressions are caught before they reach production. Refer to `.github/workflows/printingtools-harness.yml` for CI specifics and `docs/printing-sample-walkthroughs.md` for broader harness usage.
