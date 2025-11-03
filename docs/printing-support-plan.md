# Printing Support Roadmap for Avalonia via PrintingTools

1. [x] Phase 1 – Source Recon & Requirements Alignment
1.1. [x] Inventory WPF printing assets (e.g., `exten/wpf/src/Microsoft.DotNet.Wpf/src/System.Printing`, `.../PresentationFramework/System/Windows/Controls/PrintDialog.cs`) to understand pipeline boundaries (dialog, ticketing, layout, device abstraction).
1.2. [x] Map Avalonia internals that affect rendering/printing (render loop, `ImmediateRenderer`, `DrawingContext`, deferred compositor) and document gaps because `exten/Avalonia` currently lacks dedicated printing APIs.
1.3. [x] Capture macOS functional requirements (print preview, paper/PDF selection, color modes, duplex) including AppKit APIs (`NSPrintOperation`, `NSPrintInfo`, `NSView` drawing semantics) and sandbox implications for M-series hardware.
1.4. [x] Define supported document/visual scenarios (single `Visual`, `VisualTree` subtree, multi-page flow content) and non-goals for initial milestone.

2. [ ] Phase 2 – PrintingTools Architecture Definition
   - See `docs/phase2-architecture.md` for evolving design notes.
2.1. [x] Design core abstractions in `src/PrintingTools` (e.g., `PrintSession`, `PrintDocument`, `IPrintAdapter`, `PrintPreviewModel`) that decouple Avalonia rendering from platform-specific back-ends.
2.2. [x] Specify extensibility points for future platforms (Windows, Linux, browser) and alignment with WPF concepts (`PrintQueue`, `PrintTicket`) to ease API parity where practical.
2.3. [x] Plan integration surface for Avalonia apps (service registration, fluent API, attached properties) keeping `PrintingTools` external but able to reach Avalonia internals through conditional `InternalsVisibleTo` or friend assembly keys.
2.4. [x] Document threading, lifetime, and resource-management expectations (dispatcher usage, async print jobs, disposal semantics).

3. [ ] Phase 3 – Avalonia Rendering Capture Strategy
   - See `docs/pagination-roadmap.md` for Phase 3.3 planning details.
3.1. [x] Audit internal rendering helpers (e.g., `Avalonia.Rendering.SceneGraph`, `Avalonia.VisualTree.VisualExtensions`) to determine minimal hooks required for high DPI vector output.
    - Introduced `VisualRenderAudit` and optional diagnostics tracing (`PRINTINGTOOLS_TRACE_RENDER`) to capture per-visual transforms, opacity, and clipping during macOS rendering.
    - Added `docs/avalonia-internals-audit.md` summarizing the exact Avalonia internals used, associated risks, and upstream changes needed (friend assemblies, renderer abstractions, diagnostic hooks).
3.2. [x] Prototype render-to-vector pipeline (PDF/CG context) leveraging `RenderTargetBitmap`, `DrawingContext`, or scene graph export, noting any internal APIs that must be exposed via build flags.
    - Added `SkiaPdfExporter` using Avalonia's `DrawingContextHelper` and SkiaSharp PDF documents for vector output when `UseManagedPdfExporter` is enabled.
3.3. [x] Establish layout pagination strategy for tall visual trees (page breaks, margins) inspired by WPF `DocumentPaginator` while remaining Avalonia-friendly. (Layout hints groundwork in place; next step is pagination engine.)
   - `PrintPaginationUtilities.ExpandPage` now slices tall visuals into multiple `PrintPage`s via `PrintPageMetrics.ContentOffset`, and `MacPrintAdapter` consumes the expanded sequence for both preview and print/PDF flows. Future work covers horizontal flow/layout diagnostics.
3.4. [x] Propose diagnostics hooks (logging, pixel inspector) to aid print fidelity debugging during development.
    - Introduced `PrintDiagnostics` with configurable sinks via `PrintingToolsOptions` and sample subscription in `AvaloniaSample`; macOS adapter and Skia exporter now emit structured events including exception details for failed renders.

4. [ ] Phase 4 – macOS Backend Implementation
4.1. [x] Build native interop layer (likely via `Objective-C` bindings within Avalonia macOS platform assembly) exposing `NSPrintOperation`, `NSView` subclassing, and custom drawing callbacks.
4.2. [x] Implement CG/Quartz drawing adapter that receives Avalonia vector content and renders onto the print context, ensuring color profiles and DPI scaling match macOS expectations.
    - Managed `MacPrintAdapter` now renders pages into high-DPI `RenderTargetBitmap` surfaces and blits them via the native `PrintingTools_DrawBitmap` helper; Objective-C bridge handles CTM transforms and interpolation.
    - `PrintPageMetrics` calculates target page/margin geometry so native drawing uses consistent DPI across preview and print.
4.3. [x] Add print settings translation layer to map Avalonia `PrintOptions` → `NSPrintInfo` (paper size, orientation, margins, duplex, copies).
    - Managed adapter now builds a `PrintSettings` payload combining `PrintPageMetrics` with session options; native `PrintingTools_ConfigurePrintOperation` applies paper size, margins, orientation, page range, job title, and printer selection to `NSPrintInfo`/`NSPrintOperation`.
4.4. [x] Support print-to-PDF/export by wiring `NSPrintOperation.PDFPanel` and file destinations for sandboxed environments.
    - Managed options now accept `PdfOutputPath`; native bridge switches `NSPrintOperation` to `NSPrintSaveJob`, targets the supplied file, and skips UI when exporting headlessly. `PrintingTools_RunPdfPrintOperation` rehydrates Skia-generated bytes via `NSPDFImageRep`, letting AppKit reuse the managed PDF for native previews.
4.5. [ ] Introduce smoke tests/sample code on macOS (M3 Pro) validating preview and physical print flows with multiple visuals.
    - Sample `AvaloniaSample` app now drives the adapter to produce bitmap previews, includes an `Export PDF` action, and exposes a native `macOS Print Preview` button wired to the Skia PDF pipeline; native build now precompiles/copies `PrintingToolsMacBridge.dylib` into `runtimes/osx/native`, resolving the earlier `dlopen` failure. Physical print validation still pending.
    - Drafted `docs/mac-smoke-tests.md` outlining the on-device validation matrix and how to capture diagnostics/logs during M3 Pro smoke runs; schedule execution and record outcomes before closing this item.

5. [ ] Phase 5 – Print Preview UX in Avalonia
5.1. [x] Design reusable preview controls (e.g., `PrintPreviewHost`, `PageThumbnail`) hosted in `PrintingTools` but stylable via Avalonia themes.
    - Sample app now includes a `PrintPreviewWindow` using `PrintPreviewViewModel`/`PreviewPageViewModel` to display thumbnails; foundation for reusable control landed.
5.2. [x] Implement virtualization/zoom/paging logic consuming the intermediate `PrintDocument` to avoid re-rendering overhead.
    - Preview window now hosts a virtualized `ListBox` of cached bitmaps with zoom slider, page navigation, and selection syncing; thumbnails reuse `PrintPreviewViewModel` data without re-rendering visuals.
    - Keep dual render paths: native macOS preview continues to use AppKit’s pipeline, while the managed window reuses cached bitmaps or Skia PDF export as appropriate.
5.3. [x] Provide default toolbar actions (printer selection, settings, export, scaling) and hook for application customizations.
    - Preview window now exposes Print, Export PDF, and Refresh Printers actions alongside a bound printer selector backed by `MacPrinterCatalog`; actions propagate through `PrintPreviewViewModel` so host apps can extend/override behaviour.
5.4. [x] Ensure preview leverages same rendering path as actual print to guarantee WYSIWYG parity.
    - Page thumbnails now bind to the same `PrintPage` metrics used for native printing; preview overlays printable margins and displays metrics summaries so developers can verify page geometry matches macOS output.

6. [ ] Phase 6 – Build & MSBuild Integration
6.1. [x] Configure solution to opt-in to Avalonia internals (e.g., extend `exten/Avalonia/build/ExternalConsumers.props` or define custom `Friend` key) so `PrintingTools` can consume necessary types without forking Avalonia.
6.2. [ ] Add conditional compilation symbols/targets enabling macOS-only backend initially while leaving hooks for other OS implementations.
6.3. [ ] Establish packaging strategy (NuGet for `PrintingTools`, sample app references) and CI build steps within `PrintingTool.sln`.
6.4. [ ] Draft developer guidance describing how consumers enable the feature (props files, service registration, capability detection).

7. [ ] Phase 7 – Validation & Quality Gates
7.1. [ ] Write unit/integration tests covering pagination math, DPI scaling, and option translation using Avalonia test harnesses or headless compositor.
7.2. [ ] Automate macOS UI/regression checks (e.g., Golden image comparison via print-to-PDF snapshots) on Apple Silicon runners.
7.3. [ ] Prepare performance benchmarks for large visual trees and multi-page documents to monitor render throughput and memory.
7.4. [ ] Conduct accessibility review for preview UI (keyboard navigation, high contrast) and document open issues.

8. [ ] Phase 8 – Documentation & Rollout
8.1. [ ] Produce developer docs & samples (update `samples/AvaloniaSample` or add dedicated `PrintingSample`) showcasing print preview and job submission.
8.2. [ ] Publish migration guidance for WPF teams (mapping from `System.Printing`/`PrintDialog` usage to `PrintingTools`).
8.3. [ ] Plan public announcement milestones (alpha, beta) including feedback loops with Avalonia community.
8.4. [ ] Track future platform extensions (Windows XPS spooler bridge, Linux CUPS integration, browser print-to-PDF) in backlog with rough scope estimates.

9. [ ] Phase 9 – Risk & Dependency Management
9.1. [ ] Validate legal/licensing implications of reusing WPF insights and ensure no direct code copying from `exten/wpf` violates licensing terms.
9.2. [ ] Identify Avalonia upstream changes required (e.g., new public hooks) and initiate contributions or patches.
9.3. [ ] Define fallback behavior when printing is unavailable (graceful degradation, feature flags) to protect applications.
9.4. [ ] Maintain a risk register (render fidelity, macOS sandbox permissions, printer driver variability) with mitigation strategies.

---

10. [ ] Phase 10 – Vector Pipeline Integration (macOS first)
10.1. [x] Promote the existing `SkiaPdfExporter` into a reusable vector renderer so both preview and native print paths can emit PDF pages without raster intermediaries.
    - Introduced `IVectorPageRenderer` in core and refactored the Skia implementation into `SkiaVectorPageRenderer`, keeping diagnostics intact.
10.2. [x] Extend `MacPrintAdapter` to toggle between raster and vector flows per session, passing vector output through the native bridge while preserving diagnostics.
    - Adapter now respects `PrintOptions.UseVectorRenderer`; macOS preview and silent print jobs dispatch PDF bytes through the bridge while retaining legacy raster fallback when the vector path is disabled.
10.3. [x] Update `PrintingToolsMacBridge` with a vector-friendly entry point (e.g., consume managed PDF bytes or issue Quartz drawing callbacks) so AppKit preview/print uses the same vector content.
    - Added `PrintingTools_RunVectorPreview`, allowing managed vector bytes to drive AppKit preview without bitmap blitting.
10.4. [x] Replace the managed preview bitmap overlay with the vector renderer once parity is validated, keeping the margin diagnostics as optional overlays.
    - Managed preview now renders via `PrintPageVectorView`, reusing the shared renderer for on-screen display while still providing the optional margin overlay; vector bytes are available for native preview launch when desired.
10.5. [ ] Leverage Avalonia’s MSBuild friend access (`ExternalConsumers.props`) to call required internals (`ImmediateRenderer`, scene graph helpers) so work can proceed without waiting on upstream API exposure.

---

**Key Findings Informing the Plan**

- Avalonia (`exten/Avalonia`) currently exposes no dedicated printing surfaces; internal access will hinge on extending `build/ExternalConsumers.props` to include `PrintingTools`.
- WPF (`exten/wpf`) offers mature printing via `System.Printing`, `ReachFramework`, and `PresentationFramework` components (e.g., `System.Windows.Controls.PrintDialog`, C++ pipeline in `System.Printing/CPP/src/PrintQueue.cpp`) that serve as architectural references but require macOS-specific reimplementation.
- macOS printing relies on AppKit/Quartz components (`NSPrintOperation`, `NSPrintPanel`), necessitating a native interop layer and careful DPI/color management for Apple Silicon hardware.
- Delivering a consistent preview experience demands a shared rendering pipeline between on-screen preview and physical printing to avoid discrepancies.
