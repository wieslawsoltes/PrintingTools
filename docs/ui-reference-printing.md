# Windows Printing UX Reference (WPF Baseline)

## Capture Session Overview
- **Session date:** 2024-07-12 (scheduled)
- **Host OS:** Windows 11 Pro 23H2
- **Hardware:** Surface Laptop 5 (Intel) + external USB laser printer (HP LaserJet) + Microsoft XPS Document Writer
- **User account:** Local admin with WPF sample apps pre-installed
- **Tools:** Snipping Tool (Win+Shift+S), OBS Studio for video capture, OneNote for annotation
- **Output location:** `assets/printing/wpf/` (screenshots, video clips) + narrative notes in this document

## Capture Checklist
1. Launch WPF sample app (`PrintPreviewSample.exe`) demonstrating FlowDocument printing.
2. Trigger `PrintDialog` via menu command, capture default view, advanced preferences button, page range validation.
3. Record `PrintDialog` interactions for XPS printer (Microsoft XPS Document Writer) including queue selection, page setup, page range error messaging.
4. Switch to OEM driver printer, capture driver-specific tabs (quality, color) to illustrate deviations.
5. Invoke `PageSetupDialog` (legacy) where available; capture layout controls and limitations.
6. Start print job, document progress dialog (`Printing...` status, cancel button behavior) for both XPS and OEM flows.
7. Monitor `Control Panel > Devices and Printers > See what's printing` to capture queue/status window and job lifecycle updates.
8. Demonstrate `DocumentViewer` print command flow (toolbar button, preview pane) and note differences from modal dialog route.
9. Annotate all captures with callouts for controls, default values, and localization/keyboard access hints.
10. Export annotated screenshots to PNG, ensure filenames reflect flow step (`print-dialog-xps-default.png`, etc.).

## Post-Capture Tasks
- Transcribe narration into structured notes (sections per flow) highlighting UX behaviors to replicate.
- Summarize accessibility observations (tab order, screen reader labels) for Avalonia designers.
- Update `docs/wpf-printing-parity-plan.md` Phase 1 notes with capture findings once complete.
- File follow-up action items for any uncovered edge cases (e.g., driver-specific dialogs requiring native bridge).

## Open Questions
- Are there regional formatting differences (decimal separators, paper size defaults) requiring multiple locale captures?
- Should we script automated UI navigation (e.g., WinAppDriver) for regression snapshots?
- Do OEM drivers expose custom dialogs via modal HWND we must host inside Avalonia?

