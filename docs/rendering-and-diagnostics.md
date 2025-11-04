# Rendering & Diagnostics Notes (Phase 4)

## Layout & Margin Rules
- `PrintLayoutHints` drive per-visual configuration: `TargetPageSize`, `Margins`, and `Scale` flow into `PrintPageSettings` via `PrintSession`.
- `PrintPageMetrics.Create` normalizes page size, margin thickness, and content rectangles, producing:
  - `PageSize` / `PagePixelSize` derived from logical DPI (default 96, overridable per render pass).
  - `ContentRect` adjusted for margins; `ContentOffset` updated by pagination slices for overflow.
- `PrintRenderPipeline.CollectPages` reuses the session paginator and ensures each page receives recalculated metrics at the requested DPI (preview 144 DPI, print 300 DPI in the macOS adapter).
- Page range filtering now happens within `PrintSession.Paginate` (using `PrintPaginationUtilities.ApplyPageRange`), guaranteeing consistent preview/print subsets.
- `DefaultPrintPaginator` centralizes `ExpandPage` logic for tall visuals, preserving `IsPageBreakAfter` semantics set via `PrintLayoutHints`.

## Preview & Rendering Hooks
- `PrintRenderPipeline.RenderBitmaps` generates high-DPI bitmaps for thumbnails/zoom panes without duplicating adapter-specific code.
- `PrintRenderPipeline.TryCreateVectorDocument` wraps the vector renderer (Skia PDF today) so preview and native adapters share identical vector payloads.
- `PrintPreviewModel.Create` provides a single entry point for MVVM layers to request pages/bitmaps/vector bytes using the shared pipeline, simplifying preview control wiring.

## Diagnostics Enhancements
- `PrintDiagnostics` remains the central logging hub; macOS adapter now reports page counts, vector preview/print dispatch, and per-page metadata before rendering.
- The macOS adapter uses the shared pipeline while continuing to emit diagnostic traces (`PRINTINGTOOLS_TRACE_RENDER`) with consistent metrics (offset, bounds, DPI).
- Rendering helpers (`PrintPageRenderer.RenderToDrawingContext`/`RenderToBitmap`) prefer Avalonia's `ImmediateRenderer` when available, falling back to managed traversal to avoid silent failures.
- Vector/Raster generation failures surface via `PrintDiagnostics.Report` with contextual details (visual tag, DPI, job options) to accelerate debugging.

## Regression Coverage
- **Harnesses**: `MacSandboxHarness` and `LinuxSandboxHarness` route diagnostics to STDOUT and exercise native dialogs, managed fallbacks, and PDF output. Use these to capture platform-specific logs before filing parity issues.
- **Golden outputs**: Planned workflow emits managed PDFs per OS and stores them under `artifacts/printing/baselines/<platform>/<scenario>.pdf`. Compare via checksum or vector diff to catch rendering regressions.
- **CI integration**: The `PrintingTools Harnesses` GitHub workflow executes Linux (`linux-harness`), macOS (`macos-harness` headless), and Windows (`windows-harness`) runs each commit; extend jobs with containerised CUPS mounts/notarized signing once available.
- **Metrics**: Capture pagination/render timings and `PrintDiagnostics` counters during harness runs. Feed data into trend dashboards once CI integration lands.

## Next Steps
- Introduce horizontal pagination hints (columns) in `DefaultPrintPaginator`.
- Extend diagnostics with optional per-page timing metrics to monitor expensive renders.
- Evaluate exposing `PrintRenderPipeline` hooks for custom preview pipelines (e.g., caching strategies) while maintaining shared DPI normalization.
