# macOS Smoke Test Plan (Phase 4.5)

## Goal
Validate the end-to-end macOS printing experience on physical hardware (M3 Pro) using the new diagnostics pipeline to capture regressions early.

## Test Matrix

| Scenario | Description | Expected Outcome | Diagnostics |
|----------|-------------|------------------|-------------|
| Native preview | Launch print preview from sample app with default printer. | `NSPrintOperation` dialog opens with correct page count/thumbnails. | `PrintDiagnostics` emits page metrics + render traces (`MacPrintAdapter`). |
| Vector preview | Enable `UseVectorRenderer` in sample and trigger the sample's Vector Preview action. | Preview renders via vector bridge (no raster artifacts). | `MacPrintAdapter` logs vector path message; `PrintingTools_RunVectorPreview` invoked. |
| Physical print | Submit job to a network printer from preview toolbar. | Printer receives job, paper margins match preview. | Capture `PrintDiagnostics` output + system print log. |
| PDF export | Export via preview toolbar and standalone button. | PDF file saved to Desktop/Documents with correct pages. | `SkiaPdfExporter` events show page indices + success. |
| Printer selection | Switch between at least two printers and print. | Options persist between preview and main window. | Diagnostics include selected printer metadata. |

## Execution Checklist

1. Enable verbose tracing (`PRINTINGTOOLS_TRACE_RENDER=1`) before launching `AvaloniaSample`.
2. Capture console output to `~/printingtools-smoke-<timestamp>.log` for later diffing.
3. For each scenario, annotate log with manual verification notes (paper size, orientation, margins).
4. After runs, archive generated PDFs alongside logs for regression comparison.

## Follow-up Actions

- File diagnostics or adapter bugs with log snippets attached.
- Update `docs/printing-support-plan.md` Phase 4.5 bullet with findings and mark task complete once two consecutive passes succeed without regressions.
