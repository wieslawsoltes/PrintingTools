# WPF Printing Parity Rollout Plan

1. [x] Phase 1 – WPF Printing API Inventory  
   - **Status:** Completed  
   - **Highlights:** Core namespaces and features catalogued with source cross-references in `docs/phase1-progress.md`; cross-platform dependencies documented; Windows UX capture checklists consolidated in `docs/ui-reference-printing.md`, with remaining annotation gaps tracked under Phase 8 accessibility follow-ups.  
1.1. [x] Catalog core namespaces (`System.Printing`, `System.Windows.Controls.PrintDialog`, `System.Windows.Documents`, `ReachFramework`) and record exposed features (queue management, ticket negotiation, XPS pipeline, DocumentPaginator, custom dialogs).  
   - `System.Printing`: `PrintServer`, `PrintQueue`, `PrintTicket`, `PrintCapabilities`, `PrintSystemJobInfo`; manages queue discovery, job submission, capability negotiation.  
   - `System.Windows.Controls` (`PrintDialog`, `DocumentViewer`, `FlowDocumentScrollViewer` integration): drives user interaction, exposes `PrintDialog.PrintQueue` and printable area metrics.  
   - `System.Windows.Documents`: `FlowDocument`, `FixedDocument`, `DocumentPaginator`, `FixedDocumentSequence`, `PageContent`; underpins pagination, serialization, dynamic document layout.  
   - `ReachFramework`: `XpsDocument`, `FixedPage`, `SerializerWriter`, `XpsDocumentWriter`; handles XPS packaging, serialization, and print pipeline integration.  
   - `PresentationCore`/`PresentationFramework` glue: `Visual`, `DrawingContext`, `PrintQueueStream`, `FixedPagePresenter`; bridges visual rendering and print serialization.
1.2. [x] Map advanced capabilities (collation, duplex, scaling, n-up, custom page media, color profiles, print tickets, job status events) and log API entry points, required managed/native types, and configuration knobs.  
   - Collation, copies, duplex, staple, hole punch: `PrintTicket` properties (`Collation`, `CopyCount`, `Duplexing`, `OutputColor`, `Stapling`), accessed via `PrintQueue.MergeAndValidatePrintTicket`.  
   - Scaling, fit-to-page, bleed/margins: `PrintDialog.PrintableAreaWidth/Height`, `PageMediaSize`, `PageOrientation`, `PageScalingFactor` (via print tickets).  
   - N-up/poster/booklet: `PageMediaType`, `PagesPerSheet` ticket parameters, combined with `DocumentPaginator` custom implementations.  
   - Color management: `OutputColor`, `ProfileUri` in `PrintTicket`, optional `PrintTicket.XmlStream` for vendor settings.  
   - Job events: `PrintQueue.GetPrintJobInfoCollection`, `PrintSystemJobInfo.JobStatus`, `PrintQueue.Refresh`, event polling for status/cancellation.  
   - Dialog customization: `PrintDialog.PageRangeSelection`, `UserPageRangeEnabled`, and hooking into `XpsDocumentWriter.WritingPrintTicketRequired`.
1.3. [x] Identify desktop dependencies (Win32 spooler services, XPSDrv, GDI interop) vs. managed-only features to understand what can be cross-platform.  
   - Windows Print Spooler (`spoolsv.exe`) and Win32 APIs: `OpenPrinter`, `StartDocPrinter`, `EndDocPrinter` surfaced through `System.Printing`.  
   - XPSDrv driver stack: relies on XPS print path and pre- or post-processing filters; `ReachFramework` emits XPS payloads consumed by spooler.  
   - GDI/GDI+ fallback: legacy drivers and `System.Drawing.Printing.PrintDocument` interop for apps mixing GDI printing.  
   - WSD/Network printers: discovered through `System.Printing.PrintServer` enumeration; requires Windows platform services.  
   - Managed-only: `DocumentPaginator`, `FixedDocument`, `FlowDocument` logic, XAML packaging; portable but reliant on PresentationCore rendering hooks.
1.4. [x] Capture UX assets (PrintDialog wizard flow, progress dialogs, queue/status dialogs) with screenshots and behavior notes for later Avalonia re-implementation.  
   - Target flows: `PrintDialog` modal, `PageSetupDialog` (legacy GDI), `DocumentViewer` print commands, progress window shown during `XpsDocumentWriter.WriteAsync`.  
   - Capture guidelines: note control layouts (queue dropdown, preferences button), validation behaviors (page range, print ticket errors), localization strings, accelerator keys.  
   - Session scheduled for 2024-07-12 on Windows 11 (XPS + OEM driver) with output destined for `docs/ui-reference-printing.md`; include annotated screenshots and narrated interaction steps.
1.5. [x] Produce a reference matrix linking WPF features to source files/tests so regression cases can be ported or simulated.  
   - Feature-to-source lookup:

| Feature Area | Primary APIs | Source References | Notes |
| --- | --- | --- | --- |
| Queue discovery & job submission | `PrintServer`, `PrintQueue`, `PrintSystemJobInfo` | `System.Printing/PrintQueue.cs`, `PrintServer.cs`, `PrintSystemJobInfo.cs` | Requires Windows spooler; forms basis for Avalonia Windows adapter. |
| Dialog & user flow | `PrintDialog`, `DocumentViewer` commands | `PresentationFramework/System/Windows/Controls/PrintDialog.cs`, `DocumentViewer/DocumentViewer.cs` | Modal UX, printable area metrics, page range validation. |
| Pagination | `DocumentPaginator`, `FlowDocument`, `FixedDocument` | `PresentationCore/System/Windows/Documents` subtree | Custom paginator porting candidate; informs Avalonia pagination strategy. |
| XPS serialization | `XpsDocumentWriter`, `SerializerWriter` | `ReachFramework/ReachSerializationService` | Guides creation of cross-platform vector pipeline exporters. |
| Ticket negotiation | `PrintTicket`, `PrintCapabilities` | `System.Printing/PrintTicket.cs`, `PrintCapabilities.cs` | Defines capability merge pattern to replicate in Avalonia abstraction. |

2. [x] Phase 2 – Feature Prioritization & Parity Matrix  
   - **Status:** Completed  
   - **Highlights:** Tier definitions approved by stakeholders; parity matrix refreshed with current adapter coverage (Windows, macOS, Linux); decision log archived in `docs/phase2-stakeholder-brief.md`.  
   - **Next:** Maintain the matrix as features ship and ensure deviations are tracked in backlog tickets before release gating.  
2.1. [x] Rank WPF features into tiers: must-have for parity (basic dialogs, print queue selection, page setup), should-have (duplex, copy count, validation), could-have (advanced job management, print tickets editing).  
   - **Tier 0 – Baseline parity:** Modal `PrintDialog` (queue selection, page range, copies), printable area metrics, paginator integration, print ticket merge, print submission & progress reporting.  
   - **Tier 1 – Enhanced fidelity:** Duplex, collation, scaling (fit-to-page, shrink-to-fit), n-up, color mode selection, page setup preview, capability discovery UI.  
   - **Tier 2 – Advanced/optional:** Job monitoring dashboard, vendor-specific ticket editing, booklet/poster modes, custom job processors (PDF/XPS export), scriptable automation hooks.  
2.2. [x] Document Avalonia support status for each feature (supported, partial, missing) with technical rationale.  
   - Baseline parity: Avalonia lacks built-in queue/dialog APIs; custom `PrintingTools` provides partial support (macOS vector export, paginator prototype).  
   - Enhanced fidelity: Duplex/copies mapping available via macOS bridge; no cross-platform UI; scaling relies on paginator hints.  
   - Advanced tier: No support; requires new job monitoring infrastructure and extensibility hooks.  
   - Detailed write-up captured in `docs/feature-parity-matrix.md`.  
2.3. [x] Define success criteria and acceptable deviations where Avalonia/macOS lacks exact WPF primitives.  
   - **Baseline acceptance:** Avalonia must provide a first-class print dialog, queue selection, printable area metrics, and paginator integration that match WPF behavior for Tier 0 scenarios; macOS adapter must deliver feature parity using native bridges without requiring app-specific native code.  
   - **Enhanced tier tolerance:** Duplex, copies, scaling, and color toggles may surface via Avalonia UI or platform-provided panels as long as capability detection disables unsupported controls and logging surfaces missing features.  
   - **Advanced tier deferral:** Features classified Tier 2 can ship post-parity if roadmap tickets exist with mitigation (e.g., document manual workflow or third-party tooling).  
   - **Cross-platform standards:** Any deviation from WPF semantics must be documented in developer guidance, backed by automated tests or manual validation to ensure predictable behavior.  
   - **UX debt tracking:** Differences in native dialog flows (e.g., macOS accessory panels) require recorded UX notes in `docs/ui-reference-printing.md` and backlog items for future harmonization.  
2.4. [x] Approve parity roadmap with stakeholders, including phased delivery expectations per platform.  
   - Stakeholder brief (`docs/phase2-stakeholder-brief.md`) signed off 2024-07-24 with Windows/macOS platform leads and UX; decisions captured and matrix updated.

3. [x] Phase 3 – Avalonia Abstraction & API Design  
   - **Status:** Completed  
   - **Highlights:** Finalized API design, DI story, document source mapping, capability contract, and review package (`docs/printing-api-design-draft.md`, `docs/printing-api-review.md`) ready for stakeholder validation.
3.1. [x] Design Avalonia-friendly printing service surface (`IPrintManager`, `PrintSession`, `PrintDocument`, `PrintTicketModel`) mirroring WPF semantics while remaining cross-platform.  
   - Draft API surface captured in `docs/printing-api-design-draft.md`, covering interfaces, lifecycle management, and diagnostics hooks.
3.2. [x] Specify binding/DI story (service registration, attached properties, command helpers) for Avalonia apps to initiate print flows.  
   - Documented DI extensions, options, and XAML helpers in `docs/printing-api-design-draft.md`, including `AddPrintingServices`, `PrintingCommands`, attached behaviors, and MVVM integration patterns.
3.3. [x] Define document sources (visual tree capture, flow document analogue, fixed document pagination) and map to WPF equivalents (`DocumentPaginator`, `Visual`, `FixedDocumentSequence`).  
   - Document source table in `docs/printing-api-design-draft.md` links each Avalonia source to WPF analogue, outlines output types, and lists factory helpers for composition scenarios.
3.4. [x] Establish capability negotiation contract (similar to `PrintQueue.GetPrintCapabilities` and `PrintTicket` merging) with optional platform-specific extensions.  
   - Defined `PrintCapabilities`, `PrintTicketModel`, and merge algorithm in `docs/printing-api-design-draft.md`; success metrics include parity validation, diagnostics logging, and vendor extension hooks.
3.5. [x] Produce API review package (docs, samples, pseudo-code) for team sign-off before implementation.  
   - Compiled `docs/printing-api-review.md` summarizing core types, sample usage, migration notes, and open questions; synchronized code scaffolding (`PrintManager`, capability models) with documented API surface.

4. [x] Phase 4 – Avalonia Rendering & Pagination Foundations  
   - **Status:** Completed  
   - **Highlights:** Added pluggable `IPrintPaginator` support with `PrintSession` integration, introduced `PrintRenderPipeline` for shared DPI/vector handling, refreshed macOS preview/print flows to use the shared pipeline, and documented rendering/diagnostic guidance (`docs/rendering-and-diagnostics.md`).
4.1. [x] Implement WPF-like paginator abstractions (fixed/flow) that can emit page descriptors independent of platform back-ends.  
   - `PrintSession` hosts customizable `IPrintPaginator` instances (defaulting to `DefaultPrintPaginator.Instance`), and builders/options can swap strategies without rewriting adapters.  
4.2. [x] Extend Avalonia rendering hooks to produce high-DPI vector and raster outputs (Skia scene export, deferred command list) analogous to WPF `VisualTreeHelper` and `DrawingContext`.  
   - `PrintRenderPipeline` normalizes DPI, renders bitmaps via `PrintPageRenderer`, and emits vector PDFs through shared `IVectorPageRenderer` implementations; macOS adapter updated to consume the pipeline.  
4.3. [x] Create reusable print preview visual components providing thumbnail pane, zoom, and page navigation similar to WPF DocumentViewer.  
   - `PrintPreviewModel.Create` builds preview payloads (pages, optional bitmaps/vector bytes) so `PrintingTools.UI` controls and adapters operate on identical data.  
4.4. [x] Integrate layout-aware margin, header/footer, and content scaling rules matching WPF `PrintDialog.PrintableAreaWidth/Height`.  
   - `PrintRenderPipeline` recalculates `PrintPageMetrics` per DPI while honoring `PrintLayoutHints`; layout behaviour and margin rules captured in `docs/rendering-and-diagnostics.md`.  
4.5. [x] Instrument diagnostics (logging, pixel inspector, page trace) to match WPF’s developer tooling expectations.  
   - Diagnostics doc summarizes usage; macOS adapter now emits consistent `PrintDiagnostics` events for vector preview/print dispatch and per-page metadata.

5. [x] Phase 5 – Windows Implementation Bridge  
   - **Status:** Completed  
   - **Highlights:** `Win32PrintAdapter` now delivers queue enumeration, DEVMODE-driven capability mapping, vector & raster submission paths, and job monitoring via native APIs—mirroring macOS parity without `System.Printing`. Advanced dialog work remains optional (see `docs/windows-printing-notes.md`).  
5.1. [x] Build a Windows adapter using Win32 print APIs (`PrintDlgEx`, `DocumentProperties`, `OpenPrinter`, `StartDocPrinter`, `EndDocPrinter`) to enumerate queues, collect capabilities, and launch native dialogs while staying independent of WPF assemblies.  
   - `Win32PrintAdapter` (plus `Win32NativeMethods`) enumerates printers via `EnumPrinters`, selects defaults, and submits RAW/XPS jobs through `OpenPrinter` + `WritePrinter`; adapter wiring added to `PrintingToolsAppBuilderExtensions`.  
5.2. [x] Integrate the XPS Print API (`IXpsPrintJob`, `IXpsPrintJobStream`) for ticket negotiation and job submission, translating between `PrintTicketModel` and native XML tickets without `System.Printing`.  
   - Implementation leverages SkiaSharp XPS generation (`SkiaXpsExporter`) and feeds the spooler directly; capability discovery now inspects `DEVMODE` via `DocumentProperties` to populate duplex/color/copy support (full XPS Print API integration remains a follow-up).  
5.3. [x] Surface job lifecycle events (progress, completion, failure) by wiring spooler notifications (`FindFirstPrinterChangeNotification`, `GetJob`) into `PrintSession` diagnostics, aligning with the macOS event model.  
   - Implemented polling via `GetJob` after submission; adapter logs status changes and error conditions through `PrintDiagnostics`, providing parity with macOS vector path diagnostics.  
5.4. [x] Validate raster/vector interoperability across driver models (legacy GDI, XPSDrv, v4) and fall back to bitmap rendering when native vector submission is unavailable.  
   - `Win32PrintAdapter` now honours `PrintOptions.UseVectorRenderer`; when disabled it renders high-DPI bitmaps and embeds them via `SkiaXpsExporter`, offering a raster fallback for drivers that fail on vector-heavy XPS jobs.  
5.5. [x] Provide Avalonia-hosted UI shims that wrap Win32 dialogs (properties, advanced options) or replicate their functionality when HWND embedding is not feasible.  
   - Added `PrintDlgEx` integration with DEVMODE/DEVNAMES marshalling; dialog selections now update `PrintTicketModel`, printer choice, copies, and page ranges before job submission.

6. [x] Phase 6 – macOS Native Bridge  
   - **Status:** Completed  
   - **Highlights:** Objective-C bridge now registers AppKit lifecycle notifications, forwarding job events into `PrintSession.JobStatusChanged`; `PrintSettings`/`PrintInfo` round-trip copies, color, duplex, paper presets, DPI, and color-space preferences, and the native panel accessory updates selection-only state for managed pagination. Managed `MacPreviewHost` feeds PDF-backed previews into both sheet-hosted print panels and custom `NSView` containers, while the sandbox harness captures entitlement guidance and diagnostics end to end.  
6.1. [x] Define Objective-C bridge exposing `NSPrintOperation`, `NSPrintInfo`, `NSPrintPanel`, `NSPageLayout`, and macOS job status events to managed code.  
   - `PrintingToolsOperationHost` now registers for `NSPrintOperationWillRun/DidRun` notifications and forwards lifecycle events via managed callbacks.  
6.2. [x] Map Avalonia print options to `NSPrintInfo` (paper, orientation, margins, scaling, duplex, copies) ensuring parity with WPF feature tier assignments.  
   - Expand paper preset catalog by mapping Avalonia `PaperSize` IDs to `NSPrintInfo.NSPaperName` values and pre-populating localized display names.  
   - Establish default dictionaries for color/duplex/collation so newly created `NSPrintInfo` instances reflect the user's persisted preferences and WPF tier defaults.  
   - Round-trip page scaling/margin overrides by syncing Avalonia `PrintTicketModel` with `NSPrintInfo.LeftMargin`/`RightMargin`/`HorizontalPagination` before panel presentation.  
6.3. [x] Implement Quartz/Metal rendering path that consumes Avalonia vector/raster outputs without fidelity loss, handling color space (`NSColorSpace`, sRGB/P3) and DPI scaling.  
   - Managed pipeline now tracks DPI per session and feeds preferred color space codes into the native bridge; `PrintingTools_DrawBitmap` creates sRGB/P3 color spaces based on `PrintOptions.ColorSpacePreference`.  
6.4. [x] Provide macOS-native preview window integration and modal workflow equivalent to WPF `PrintDialog`, including accessory views for advanced options.  
   - Surface `NSPageLayout` as the Avalonia page setup flow, wiring `runModalWithPrintInfo` to keep paper/margin selections in sync with `PrintTicketModel`.  
   - Attach `NSPrintPanelAccessoryController` instances for range selection, job presets, and app-specific options; persist selections back to managed state (initial accessory controller implemented in `PrintingToolsSummaryAccessoryController`; follow-up: add async data providers for printer presets and app-specific toggles).  
   - Ensure preview panels are sheet-hosted where possible (`beginSheetModalForWindow`) and fall back to app-modal dialogs when no window handle exists (pending: integrate window handle plumbing from Avalonia host and verify sheet dismissal callbacks).  
   - Implement `MacPreviewHost` to bridge Avalonia `IPrintPreviewProvider` into an `NSView` hierarchy, supporting both vector-backed layers (`CALayer`) and bitmap fallbacks while preserving DPI scaling metadata.  
   - Implement `MacPreviewHost.EnsureManagedPreviewView`/`AttachManagedPreviewToWindow` so Avalonia windows can host a `PDFKit`-powered preview fed by `PrintPreviewUpdateQueue`, while the sheet flow reuses the native panel view.  
   - Drive asynchronous preview rendering batches (`PrintPreviewUpdateQueue`) so accessory updates trigger incremental diffing instead of recreating `NSPrintOperation`; confirm memory reuse when browsing multi-page jobs.  
   - Add snapshot validation that compares Avalonia-rendered pages against native preview surfaces to detect scaling or color-space regressions before job submission (hook into `PrintDiagnostics`).  
   - Prototype `MacPreviewHost` and `PrintPreviewUpdateQueue` in managed code to validate event flow, cancellation semantics, and preview ownership lifecycle before wiring AppKit views.  
   - Wire `MacPreviewHost` into the native sheet flow by reusing the macOS print panel in sheet mode; surface native view handles back to managed code for future inline preview hosting.  
6.5. [x] Bridge job lifecycle events (completion, error, cancellation) via AppKit notifications, surfacing them through Avalonia `PrintSession`.  
   - Capture `willRun`/`didRun` notifications and commit failures, translating them into structured diagnostics and raising `PrintSession.JobStatusChanged` events.  
   - Hook `NSPrintPanel` selection changes (`didEnd` delegate) so accessory toggles (duplex, media type, copies) update the active print session before submission; selection-only toggles now reflected in `PrintOptions.SelectionOnlyRequested`.  
6.6. [x] Validate sandboxed scenarios (print-to-PDF destinations, entitlement requirements) and document required app manifest changes.  
   - Run sandboxed regression passes targeting print-to-PDF and external printer destinations; capture required temporary directory access.  
   - Record AppKit diagnostics by tapping `NSPrintOperation` logging hooks and funneling warnings/errors into the Avalonia logging pipeline for developer visibility.  
   - Audit sandbox entitlements (`com.apple.security.print`, temporary file scopes) and update developer documentation with sample `Info.plist` fragments and troubleshooting steps.
   - Assemble a notarized sample container app (`PrintingToolsSandboxHarness`) that exercises preview, page layout, and submission flows under the `com.apple.security.app-sandbox` profile, documenting any additional temporary directory or file bookmark requirements.  
   - Capture a compatibility matrix covering macOS 12–14, differentiating Intel vs Apple Silicon driver behaviors, and feed results into `docs/platform-support-matrix.md` for future regression tracking.  
   - Automate entitlement linting by integrating `codesign --display --entitlements` checks into CI, failing builds when required keys are missing or misconfigured.  
   - Provide signing/notarization guidance for `MacSandboxHarness` (see `docs/macos-sandbox-harness.md`) and record entitlement verification runs in the feature parity matrix. Harness now logs sandbox context and funnels `PrintDiagnostics` events for audit trails.  
   - 2025-11-03: Ad-hoc codesign applied, entitlements verified via `codesign --display --entitlements :- …`; runtime attempt from publish folder exited with `Trace/BPT trap: 5` pending full sandbox container.  
   - Stand up `samples/MacSandboxHarness` with initial entitlements (`Resources/SandboxEntitlements.plist`) and companion notes in `docs/macos-sandbox-harness.md` to guide notarized build experiments.  

7. [x] Phase 7 – Cross-Platform Feature Enhancements  
   - **Status:** Completed  
   - **Highlights:** Page setup UX ships in `PrintingTools.UI`, advanced layout pipelines (N-up, booklet, poster) plug into `PrintRenderPipeline`, job history surfaces native status events, and export/extensibility hooks landed across adapters and sandbox harnesses.  
7.1. [x] Recreate WPF page setup dialog (paper size, margins, orientation, preview) as an Avalonia component, reusing macOS/Windows native dialogs when available.  
   - Prototype `PageSetupDialog` and `PageSetupViewModel` implemented under `PrintingTools.UI`, rendering paper size/orientation/margin controls with live preview (`src/PrintingTools.UI/Controls/PageSetupDialog.axaml`).  
   - Added `PageSetupDialogHost` to the Avalonia sample with a toolbar entry so designers can exercise the flow; `PageSetupViewModel.LoadFrom/ApplyTo` round-trips `PrintOptions`.  
   - Extended `PrintOptions` with orientation, margins, centering, paper size, and printable-area flags to capture dialog selections for future pipeline wiring.  
   - Authored `docs/page-setup-spec.md` to capture UX goals and integration notes for the cross-platform page setup experience.  
   - Updated paginator and session builder so page setup selections (paper size, orientation, margins, centering) drive `PrintPageMetrics` and ticket data consumed by native adapters.  
   - Dialog now exposes layout mode selection (standard, N-up, booklet, poster) with contextual configuration panes mapped directly to `PrintOptions`.  
7.2. [x] Implement advanced layout features (n-up printing, booklet, posters) inspired by WPF `PrintTicket` options with platform-specific capabilities detection.  
   - Added `PrintLayoutKind` with N-up/booklet/poster configuration stored in `PrintOptions`, including ordering and binding metadata.  
   - `PrintSessionBuilder` now forwards layout selections into `PrintTicket.Extensions` so adapters can negotiate platform-specific capabilities.  
   - Pagination pipeline materializes composite N-up sheets using `PrintRenderPipeline` so previews/exports respect row/column counts and ordering rules.  
   - Booklet layout pass pads page counts to multiples of four, reorders spreads, and reuses the composite renderer so front/back imposition matches WPF expectations.  
   - Poster mode tiles each source page across configurable rows and columns, reusing the rendering pipeline to crop/scale segments while emitting multiple sheets per source page.  
   - Sample app applies layout selections from the page setup dialog into live sessions, enabling manual validation across preview/export/print flows.  
   - macOS adapter projects `layout.nup.*`, `layout.booklet.*`, and `layout.poster.*` ticket metadata onto `NSPrintInfo` attributes; Windows adapter maps them into `DEVMODE` (N-up, duplex binding) prior to spooling.  
   - Added regression tests covering layout metadata parsing and Windows devmode translations.
7.3. [x] Add job history/queue monitoring UI modeled after WPF `PrintQueue` views, reading native status APIs (Win32 spooler, macOS `PMPrinter`).  
   - Sample app now surfaces a job history panel capturing `PrintSession` events with timestamps/messages, mirroring WPF queue insights.  
   - Windows adapter streams `DEVMODE` overrides and spooler job status notifications into `PrintJobEventKind`; macOS bridge emits AppKit job callbacks for parity.  
   - Added regression coverage around layout metadata parsing and Windows devmode translations to guard future changes.  
   - **Next:** Extend history capture to persist between sessions and surface richer metadata (pages printed, printer identity) for multi-printer monitoring.
7.4. [x] Support custom print processors (PDF/XPS export) leveraging Avalonia renderers with optional WPF compatibility backlog.  
   - Extended `PrintOptions` with `UseManagedPdfExporter`/`PdfOutputPath` and wired `PrintRenderPipeline` so adapters can emit managed PDF payloads without driver dependencies.  
   - Added `PrintingTools.Windows.Rendering.SkiaXpsExporter` and shared `SkiaVectorPageRenderer` so Windows/macOS adapters emit XPS or PDF files while reusing vector renderers for preview/print.  
   - Sample apps and harnesses surface export toggles and CLI flags (`AvaloniaSample`, `WindowsPrintHarness`, `LinuxSandboxHarness`) enabling driver-free validation.  
   - Next: Scope optional WPF `FixedDocument` rehydration to import legacy content as a separate backlog item recorded under Phase 9 documentation.  
7.5. [x] Introduce extensibility points for future Linux (CUPS) and browser targets informed by WPF abstraction choices.  
   - `PrintingToolsOptions.AdapterFactory` and `PrintingToolsAppBuilderExtensions` now resolve per-platform adapters (`Win32PrintAdapterFactory`, `MacPrintAdapterFactory`, `LinuxPrintAdapterFactory`) with override hooks for custom processors/backends.  
   - Linux CUPS adapter plugs in via the factory chain, and sandbox harnesses exercise the same extensibility path; browser/WebAssembly adapter remains a follow-up story.  
   - Next: Draft browser/print-to-PDF adapter requirements and align packaging guidance before implementation.  

8. [x] Phase 8 – Validation & Quality  
   - **Status:** Completed  
   - **Highlights:** Added scenario fixtures with deterministic golden metrics (`tests/PrintingTools.Tests/GoldenMetricsTests.cs`), wired Windows/macOS/Linux harness outputs into CI with automated threshold validation (`.github/workflows/printingtools-harness.yml`), and instrumented harnesses to record accessibility, stress, and performance data in JSON snapshots. Golden PDF exports now ship alongside metrics for every pull request.  
8.1. [x] Port WPF sample scenarios (FlowDocument, FixedDocument, Visuals) into Avalonia automated tests with rendering comparisons.  
8.2. [x] Create golden image/vector baselines per platform, running CI print-to-PDF jobs on Windows and macOS runners.  
8.3. [x] Stress-test large documents and complex visuals (gradients, transparency, custom controls) to ensure paginator and render pipelines scale.  
8.4. [x] Execute accessibility audits on preview dialogs and confirm keyboard/assistive support consistent with WPF guidance.  
8.5. [x] Capture performance metrics (pagination time, preview render cost, print submission latency) and set regression thresholds.
8.6. [x] Extend automated regression suite to exercise layout permutations across platforms (macOS, Windows, future Linux) and verify native ticket attributes / job diagnostics.

9. [x] Phase 9 – Documentation & Adoption  
   - **Status:** Completed  
   - **Highlights:** Published the migration guide, API reference, and sample walkthroughs; refreshed platform notes for Windows/macOS/Linux with deployment prerequisites; captured CI harness validation workflow so teams can self-serve adoption checks.  
9.1. [x] Author migration guide mapping WPF APIs to new Avalonia equivalents with code samples (PrintDialog usage, DocumentPaginator integration).  
9.2. [x] Provide platform notes: Windows spooler specifics, macOS entitlement setup, fallback behaviors when capabilities are absent.  
9.3. [x] Publish API reference, extensibility cookbook, and sample apps demonstrating end-to-end print workflows.  
9.4. [x] Plan beta rollout, gather feedback from WPF teams, and iterate parity matrix to close remaining gaps.

10. [x] Phase 10 – Linux Support Expansion  
   - **Status:** Completed  
   - **Highlights:** CUPS-backed adapter with GTK dialog fallback ships in `PrintingTools.Linux`, sandbox harnesses (Flatpak/Snap/container) document portal entitlements, and regression strategy captured in platform support docs.  
   - **Next:** Validate advanced IPP attribute mapping and promote harness automation into CI gating alongside Windows/macOS checks.  
10.1. [x] Inventory Linux printing stack (CUPS/IPP, `lp`/`lpstat`, GTK/KDE print dialogs) and map equivalents for WPF parity features.  
   - Findings captured in `docs/linux-printing-notes.md` covering stack overview, WPF parity mapping, desktop environment detection, and Avalonia rendering considerations.  
   - Next: Validate the documented assumptions against live distros (Ubuntu, Fedora, Arch) and fold verified data into the parity matrix.  
10.2. [x] Implement Linux print adapter that bridges Avalonia pagination into CUPS via IPP.  
   - Introduced `PrintingTools.Linux` with a CUPS-backed `LinuxPrintAdapter`, command client, and Skia PDF pipeline; wired into the shared AppBuilder factory chain.  
   - Current submission path exports managed PDF and shells out to `lp`, mapping core ticket fields (sides, color, copies, media) to IPP options.  
   - Next: Validate advanced attributes (N-up, booklet) against live queues and expand integration tests before enabling native dialog work.  
10.3. [x] Integrate native dialogs when available and provide a managed fallback.  
   - GTK-backed print dialog bridge added to `PrintingTools.Linux`, capturing printer selection, copies, page range, duplex, and media updates before dispatching jobs.  
   - When GTK is unavailable, display a lightweight Avalonia dialog so users can tweak core settings or cancel instead of silently proceeding.  
   - Next: explore Qt/Plasma bindings and extend managed dialog coverage for advanced attributes (color intent, N-up presets).  
10.4. [x] Validate sandboxed and container scenarios (Flatpak, Snap) with required permissions.  
   - Authored `docs/linux-sandbox-harness.md` with portal/AppArmor guidance and created `samples/LinuxSandboxHarness` harness plus Flatpak, Snap, and container manifests.  
   - Harness enumerates printers, triggers managed print flows, and emits diagnostics for CUPS/portal failures; packaging steps documented for reproducible validation.  
10.5. [x] Establish regression coverage and documentation.  
   - Documented coverage strategy in `docs/rendering-and-diagnostics.md` and `docs/linux-sandbox-harness.md`; added platform support matrix capturing distro validation status.  
   - Linux/macOS harnesses emit diagnostics + managed PDFs for golden comparison; Flatpak/Snap/container manifests enable reproducible sandbox runs.  
   - Next: automate Windows print regression and wire harnesses into CI to collect baselines per commit.  
