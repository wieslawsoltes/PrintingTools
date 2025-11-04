# Printing API Reference (MVP)

This reference captures the public surface of the `PrintingTools` MVP so you can locate the right abstractions without digging through the source.

## 1. Namespaces & Assemblies

| Assembly | Namespace | Purpose |
| --- | --- | --- |
| `PrintingTools.Core` | `PrintingTools.Core` | Cross-platform contracts, print sessions, pagination helpers, diagnostics. |
| `PrintingTools.Windows` | `PrintingTools.Windows` | Win32/XPS print adapter and rendering helpers. |
| `PrintingTools.MacOS` | `PrintingTools.MacOS` | AppKit bridge, preview host, macOS adapter. |
| `PrintingTools.Linux` | `PrintingTools.Linux` | CUPS-backed adapter with GTK/managed dialogs. |
| `PrintingTools.UI` | `PrintingTools.UI` | Optional Avalonia UI components (page setup dialog, preview window). |

## 2. Core Services

| Type | Description | Key Members |
| --- | --- | --- |
| `PrintServiceRegistry` | Static entry point for configuring shared options and resolving services. | `Configure(PrintingToolsOptions options)`, `EnsureManager()`, `EnsureResolver()` |
| `IPrintManager` | Orchestrates sessions, previews, and print submission. | `RequestSessionAsync`, `CreatePreviewAsync`, `PrintAsync`, `GetPrintersAsync`, `GetCapabilitiesAsync` |
| `IPrintAdapter` | Platform-specific implementation used by `IPrintManager`. | `CreateSessionAsync`, `PrintAsync`, `CreatePreviewAsync`, `GetPrintersAsync`, `GetCapabilitiesAsync` |
| `PrintingToolsOptions` | Global configuration passed to adapters. | `AdapterFactory`, `EnablePreview`, `DefaultTicket`, `DefaultPaginator`, `DiagnosticSink` |
| `PrintRequest` | Wraps a document, ticket, and options when requesting a session. | `Document`, `Options`, `Ticket`, `PreferredPrinterId`, `Description` |
| `PrintSession` | Mutable session produced by adapters. | `Document`, `Options`, `Ticket`, `Printer`, `Capabilities`, `Paginate()`, `JobStatusChanged` |
| `PrintPreviewModel` | Result from `CreatePreviewAsync`. | `Pages`, `VectorDocument`, `HasBitmaps`, `Dispose()` |

## 3. Options & Tickets

| Type | Purpose | Highlights |
| --- | --- | --- |
| `PrintOptions` | User-facing toggles that influence preview/print behaviour. | `ShowPrintDialog`, `UseVectorRenderer`, `PdfOutputPath`, `Orientation`, `Margins`, `PaperSize`, `LayoutKind`, `NUpRows/Columns`, `BookletBindLongEdge`, `PosterTileCount`, `ColorSpacePreference`. |
| `PrintTicketModel` | Device-facing state mirroring WPF `PrintTicket`. | `PageMediaSize`, `Duplex`, `ColorMode`, `Copies`, `Extensions`, `MergeWithCapabilities(capabilities)`. |
| `PrintCapabilities` | Device capabilities returned by adapters. | `PageMediaSizes`, `Orientations`, `Duplexing`, `ColorModes`, `SupportedCopyCounts`, `Warnings`. |
| `PrintPageSettings` | Layout hints for generating pages. | `TargetSize`, `Margins`, `Scale`, `SelectionBounds`. |
| `PrintLayoutKind` | Enumerates layout strategies. | `Standard`, `NUp`, `Booklet`, `Poster`. |
| `PrintLayoutHints` | Attached properties for tagging visuals. | `SetIsPrintable`, `SetIsPageBreakAfter`, `SetPageName`. |

## 4. Pagination & Rendering

| Type | Description |
| --- | --- |
| `IPrintPaginator` | Interface for custom pagination strategies. Default implementation is accessible via `DefaultPrintPaginator.Instance`. |
| `PrintPage` | Immutable page containing the visual, settings, metrics, and `IsPageBreakAfter` flag. |
| `PrintPageMetrics` | Captures page size, content rectangle, DPI, pixel sizes, and offsets. |
| `PrintRenderPipeline` | Static helper for collecting pages, rendering bitmaps, and producing vector payloads. |
| `IVectorPageRenderer` | Plug-in abstraction for exporting PDF/XPS (`SkiaVectorPageRenderer`, `SkiaXpsExporter`). |

## 5. Diagnostics & Events

| Type | Purpose |
| --- | --- |
| `PrintDiagnostics` | Global event hub for logging warnings/errors from adapters. Register sinks via `PrintingToolsOptions.DiagnosticSink` or `PrintDiagnostics.RegisterSink`. |
| `PrintDiagnosticEvent` | Raised for each diagnostic. Includes `Category`, `Message`, optional `Exception`, and `Context`. |
| `PrintJobEventKind` | Enum for job lifecycle events: `Started`, `Completed`, `Failed`, `Cancelled`, `Unknown`. |
| `PrintJobEventArgs` | Payload passed to `PrintSession.JobStatusChanged`. |

## 6. UI Components (Optional)

| Component | Location | Usage |
| --- | --- | --- |
| `PageSetupDialog` | `PrintingTools.UI.Controls` | XAML dialog for configuring paper, margins, layout modes. Bind to `PageSetupViewModel`. |
| `PrintPreviewWindow` | `PrintingTools.UI.Controls` | Avalonia preview shell consuming `PrintPreviewModel`. |
| `MacPreviewHost` | `PrintingTools.MacOS.Preview` | Wraps the native AppKit preview surface; used by macOS adapter and sample preview window. |

## 7. Extensibility Points
- **Custom adapters:** Provide your own `PrintingToolsOptions.AdapterFactory` to override platform selection (e.g., injecting a mock adapter during tests).
- **Ticket extensions:** Store platform/vendor attributes under namespaced keys in `PrintTicketModel.Extensions` (`cups.*`, `windows.*`, `macos.*`, `layout.*`).
- **Preview providers:** Implement `IPrintPreviewProvider` if you need to supply native previews outside the default adapters (macOS harness uses this through `MacPrintAdapter`).
- **Diagnostics:** Attach multiple sinks to `PrintDiagnostics` to route events into Serilog, AppCenter, or console logging.

Refer to [`docs/printing-migration-guide.md`](printing-migration-guide.md) for upgrade steps, and the platform notes for environment-specific requirements.
