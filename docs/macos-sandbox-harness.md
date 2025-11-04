# macOS Sandbox Harness Notes

## Goals

- Exercise the native macOS preview bridge (`MacPreviewHost`) under `com.apple.security.app-sandbox`.
- Validate entitlement requirements for print-to-PDF and physical printer submission.
- Capture diagnostics emitted by `NSPrintOperation` and surface them through `PrintDiagnostics`.

## Initial Entitlement Set

- `com.apple.security.app-sandbox` – required umbrella entitlement for sandboxed binaries.
- `com.apple.security.print` – unlocks `NSPrintOperation` and printer device access.
- `com.apple.security.files.user-selected.read-write` – supports user-selected destination files (print-to-PDF workflows).
- `com.apple.security.files.downloads.read-write` – allows writing PDFs into the Downloads folder during automated scenarios.

## Harness Layout

- Project: `samples/MacSandboxHarness/MacSandboxHarness.csproj`.
- Entry point wires `MacPreviewHost` to the `MacPrintAdapter` and emits console diagnostics for preview lifecycle events.
- `Resources/SandboxEntitlements.plist` is copied to the output directory for codesign/notarization hooks.

## Native Sheet Preview

- `MacPreviewHost.PresentPrintPanelSheet` now drives the macOS print panel as a sheet when given a host `NSWindow` handle.
- Helper methods (`MacPreviewHost.CreateHostWindow`, `ShowHostWindow`, `DestroyHostWindow`, `AttachManagedPreviewToWindow`) expose lightweight wrappers over the native bridge so test harnesses do not need to touch `PrintingToolsInterop` directly.
- `ShowNativePrintPanel` in `MacPrintAdapter` keeps the managed pagination pipeline aligned with the native preview surface, ensuring `PrintPreviewUpdateQueue` refreshes fire after the sheet completes.

## Codesign & Notarization Checklist

Run these steps from the repository root after publishing the harness:

1. `dotnet publish samples/MacSandboxHarness/MacSandboxHarness.csproj -c Release -r osx-arm64 --self-contained false /p:PublishSingleFile=true`.
2. `codesign --force --deep --options runtime --entitlements samples/MacSandboxHarness/Resources/SandboxEntitlements.plist -s "Developer ID Application: <team>" bin/Release/net10.0/osx-arm64/publish/PrintingTools.MacSandboxHarness`.
3. `xcrun notarytool submit bin/Release/net10.0/osx-arm64/publish/PrintingTools.MacSandboxHarness.zip --apple-id <apple-id> --team-id <team> --wait`.
4. `xcrun stapler staple bin/Release/net10.0/osx-arm64/publish/PrintingTools.MacSandboxHarness.app`.
5. Record success/failure plus entitlement notes in `docs/feature-parity-matrix.md` and link any anomalies back to the parity plan.

- During signing, verify with `codesign --display --entitlements :- bin/Release/net10.0/osx-arm64/publish/PrintingTools.MacSandboxHarness` and capture results in the entitlement matrix.

## 2025-11-03 Validation Log

- Publish command (Release/osx-arm64, single file) executed successfully.
- Ad-hoc codesign command: `codesign --force --deep --options runtime --entitlements samples/MacSandboxHarness/Resources/SandboxEntitlements.plist -s - samples/MacSandboxHarness/bin/Release/net10.0/osx-arm64/publish/PrintingTools.MacSandboxHarness`
- Entitlement dump:

```
com.apple.security.app-sandbox = true
com.apple.security.files.downloads.read-write = true
com.apple.security.files.user-selected.read-write = true
com.apple.security.print = true
```

- Runtime attempt from the publish directory terminated with `Trace/BPT trap: 5` (expected without full sandbox container / GUI session); diagnostics were emitted prior to exit.

## Next Validation Steps

- Integrate the harness into CI to exercise preview updates under the sandbox profile and capture emitted diagnostics.
- Automate `codesign --entitlements` verification in CI to guard the entitlement set.
- Record additional filesystem entitlements if printer drivers require spool directories outside the sandbox allowance.
