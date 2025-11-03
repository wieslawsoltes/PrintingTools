# Phase 2 – PrintingTools Architecture Notes

## 2.1 Core Abstractions Draft

- Candidate namespaces: `PrintingTools.Core` for cross-platform orchestration, `PrintingTools.MacOS` for native backend glue.
- Primary service graph:
  - `IPrintManager` orchestrates job lifecycle (preview, setup, submission).
  - `PrintDocument` encapsulates page sequence backed by Avalonia `Visual` snapshots or live render callbacks.
  - `IPrintAdapter` bridges to platform backends; macOS implementation now renders Avalonia visuals into Quartz contexts through the native Objective-C bridge.
  - `PrintPreviewModel` exposes pagination data alongside rendered `RenderTargetBitmap` frames for preview surfaces and implements `IDisposable` to release them.
- `PrintSessionBuilder` (`src/PrintingTools.Core/PrintSessionBuilder.cs`) offers fluent session construction, cloning `PrintOptions` and page sources into `PrintDocument`.
- `PrintDocument` now composes multiple `IPrintPageEnumerator` instances (see `src/PrintingTools.Core/PrintDocument.cs`) and supports per-page metadata via `PrintPageSettings`.
- `PrintOptions`, `PrintPageRange`, and `PrintServiceRegistry` provide session/job configuration (now including `PdfOutputPath` for headless export flows) and lazy creation of shared `IPrintManager` / `IPrintAdapterResolver` instances.
- `PrintDiagnostics` (`src/PrintingTools.Core/PrintDiagnostics.cs`) exposes structured diagnostic events wired through `PrintingToolsOptions.DiagnosticSink`; the macOS backend now raises per-page render traces and failure details to aid debugging.
- Vector rendering abstractions now live behind `IVectorPageRenderer` (`src/PrintingTools.Core/Rendering/IVectorPageRenderer.cs`); the macOS implementation (`SkiaVectorPageRenderer`) feeds both managed export and the native preview bridge.
- `MacPrinterCatalog` (`src/PrintingTools.MacOS/MacPrinterCatalog.cs`) surfaces installed printer names through the native bridge so sample/tooling UIs can offer picker experiences without duplicating interop code.
- `MacPrintUtilities` (`src/PrintingTools.MacOS/MacPrintUtilities.cs`) exposes helpers for launching vector previews from managed code, wrapping the native bridge for sample and tooling scenarios.
- `PrintPreviewModel` (`src/PrintingTools.Core/PrintPreviewModel.cs`) packages enumerated `PrintPage` instances for preview consumption.
- `DefaultPrintPaginator` (`src/PrintingTools.Core/Pagination/DefaultPrintPaginator.cs`) now enriches pages with `PrintPageMetrics` and leverages `PrintPaginationUtilities.ExpandPage` to slice tall visuals; advanced flow layout remains future work.
- `PrintingTools.MacOS` project hosts `MacPrintAdapter`/factory types that gate creation on runtime OS detection (`src/PrintingTools.MacOS/MacPrintAdapter.cs`).
- Adapter now normalizes page metrics, renders into high-DPI bitmaps, streams them to Quartz via `PrintingToolsInterop`, and supports headless PDF export while the same renderer powers preview bitmaps used in the sample UI.
- Native interop highlights:
  - `PrintingTools_ConfigurePrintOperation` maps managed `PrintOptions` and first-page `PrintPageMetrics` into `NSPrintInfo`/`NSPrintOperation` (paper size, margins, orientation, page range, job/printer selection, PDF save targets).
  - Raster prototype uses Avalonia `RenderTargetBitmap` for high-DPI output; PDF prototype will require access to Skia picture recording APIs (pending).
- Native interop TODOs:
  - Provide PDF export hooks (`NSPrintOperation.PDFPanel`) and physical print commit coordination.
  - Propagate errors/cancellation back through managed adapter, ensuring consistent disposal patterns across native calls.
- Pagination pipeline: `PrintLayoutHints.IsPageBreakAfter` feeds the `PagedCompositeEnumerator` to split visual content into logical pages ahead of rendering.

## 2.2 Extensibility Plan

- Abstraction layers separate platform concerns: `IPrintAdapterFactory` resolves per-platform adapters via runtime platform detection.
- Future targets (Windows, Linux, Browser) plug in by implementing `IPrintAdapter` and optional capability reporting interface.
- Align option containers with WPF analogues (`PrintTicket`, `PrintQueue`) through adapter model while avoiding direct dependency.
- `IPrintAdapterResolver` + `DefaultPrintAdapterResolver` (in `src/PrintingTools.Core/PrintManager.cs`) funnel adapter creation, honoring `PrintingToolsOptions.AdapterFactory` overrides.
- `UnsupportedPrintAdapter` acts as the default guardrail, surfacing a predictable `NotSupportedException` until a real backend is injected.

## 2.3 Avalonia Integration Surface

- Provide a fluent API `PrintSessionBuilder` for easy job creation from `Visual`, `Control`, or custom page enumerators.
- Introduce attached properties via `PrintLayoutHints` to tag visuals with printable metadata (enabled state, margins, scale, target page size, page breaks).
- Internals access needs: `Visual.Render`, `ImmediateRenderer`, `IRenderRoot` for capturing scene graph → enabling via `InternalsVisibleTo` entry (`PrintingTools`).
- `PrintLayoutHints` (attached properties defined in `src/PrintingTools.Core/PrintLayoutHints.cs`) surface printable toggles, margins, and scale factors that the builder translates into `PrintPageSettings`.
- macOS bridge callbacks now route through `PrintingToolsMacBridge` to managed delegates for page count/rendering and leverage the new `PrintingTools_DrawBitmap` helper to paint into Quartz contexts.
- Managed callbacks (`RenderPage`, `GetPageCount`) hydrate a `PrintContext` (GCHandle) exposing pages/metrics so the adapter reuses the same renderer for preview and print.
- `PrintingToolsAppBuilderExtensions.UsePrintingTools` seeds `PrintServiceRegistry` with cloned options and exposes helpers (`GetPrintManager`, `GetPrintingOptions`) for application-level wiring without taking a hard dependency on Avalonia’s locator.

## 2.4 Threading & Lifetime Considerations

- `PrintSessionBuilder` and `PrintDocument.FromFactories(...)` are thread-affine to the caller; construction should be performed on the UI thread or within a controlled background context before invocation.
- `PrintSession` instances are immutable thread-safe containers; they may be passed to adapters running on background threads.
- `IPrintPageEnumerator.MoveNext` and `Current` are invoked on adapter-selected threads. Implementations must be re-entrant safe and honor the supplied `CancellationToken`.
- `CompositePrintPageEnumerator` handles page batching sequentially, guaranteeing `Dispose` is called on each nested enumerator even in cancellation scenarios.
- `PrintManager.PrintAsync` / `CreatePreviewAsync` can execute on arbitrary threads; adapters are responsible for marshaling back to the UI dispatcher when interacting with live Avalonia visuals.
- Future work: evaluate adding `IAsyncDisposable` to `PrintSession`/enumerators when backing resources (e.g., native surfaces) necessitate asynchronous cleanup.

---

**Action Items**

- Finalize `PrintSession` async disposal semantics once platform backends materialize.
- Extend `PrintLayoutHints` with pagination metadata (page breaks, target sizes) as scenarios demand. (Basic support landed; evaluate advanced hints like column flow.)
- Prototype macOS adapter implementation that plugs into `DefaultPrintAdapterResolver`.
- Capture adapter-specific threading requirements (macOS dispatcher usage) during implementation.
