# Avalonia Printing API Design Draft

## Goals
- Deliver a cross-platform printing surface that mirrors key WPF concepts (`PrintDialog`, `PrintQueue`, `PrintTicket`, `DocumentPaginator`) without exposing platform-specific details to app developers.
- Enable progressive enhancement: baseline parity (Tier 0) flows must operate with no platform conditionals; enhanced features surface when supported by the installed printer/OS.
- Allow native bridges (Windows/macOS/Linux) to plug in via adapters without leaking internal Avalonia rendering types outside controlled surfaces.

## Core Abstractions

| Concept | Responsibility | WPF Analogue | Notes |
| --- | --- | --- | --- |
| `IPrintManager` | Entry point registered in DI; issues `PrintSession` instances, enumerates printers, exposes capability queries. | `PrintQueue`, `PrintServer`, `PrintDialog` coordination | Exposes async APIs (`GetPrintersAsync`, `RequestPrintSessionAsync`). |
| `PrintSession` | Represents a single user-initiated print operation (dialog + job). Tracks selected printer, ticket, document source, and lifecycle events. | `PrintDialog.PrintQueue`, `PrintTicket`, `XpsDocumentWriter` interaction | Disposable; raises `JobSubmitted`, `JobProgress`, `JobFailed`, `JobCompleted`. |
| `PrintDocument` | Abstract base for printable content (visual tree, flow layout, fixed pages). | `DocumentPaginator`, `Visual`, `FixedDocument` | Provides `GetPaginator()` returning `IPaginator`. |
| `IPaginator` | Produces `PrintPage` descriptors with layout metrics, vector/raster payloads. | `DocumentPaginator` | Supports async page generation for large documents. |
| `PrintTicketModel` | Cross-platform capability + option bag derived from selected printer; serializable and mergeable. | `PrintTicket`, `PrintCapabilities` | Includes standard settings (paper, orientation, duplex, color) + vendor extension dictionary. |
| `IPrintAdapter` | Platform-specific bridge (Windows/macOS/etc.) responsible for showing native dialogs, submitting jobs, and converting tickets. | `PrintQueue` + native interop | Registered via platform bootstrap; resolved through `IServiceProvider`. |
| `PrintPreviewModel` | View model powering Avalonia preview UI; observes `PrintSession` and paginator outputs. | `DocumentViewer` preview logic | Provides zoom, page navigation, thumbnail data. |
| `PrintRenderPipeline` | Shared helpers for collecting pages, rendering bitmaps, and producing vector documents. | WPF print visual infrastructure | Centralizes DPI normalization and page range filtering. |

## Service Registration
- Provide `PrintingToolsServices.AddPrintingServices(IServiceCollection services, Action<PrintingToolsOptions>? configure = null)` extension to register:
  - Default `IPrintManager` implementation (`PrintManager`).
  - Platform adapter resolver (`IPrintAdapterResolver`), defaulting to `DefaultPrintAdapterResolver`.
  - `IPrintDiagnosticsSink` implementations (console/file) driven by `PrintingToolsOptions`.
  - UI helpers (`PrintPreviewHost`, `PrintDialogViewModel`, command factory).
- `PrintingToolsOptions.DefaultPaginator` allows apps to swap in custom paginator strategies (e.g., flow document adaptor) without rebuilding sessions.
- Optional `AddPrintingServices` overload for service locator scenarios (`IServiceCollection?` unavailable) that returns a `PrintingServiceRegistry`.
- Consumers opt-in via:
  ```csharp
  AppBuilder
      .Configure<App>()
      .UsePlatformDetect()
      .ConfigureServices(services =>
      {
          services.AddPrintingServices(options =>
          {
              options.EnablePreview = true;
              options.AdapterFactory = () => new MacPrintAdapter();
          });
      });
  ```
- Introduce attached properties/behaviors:
  - `PrintingCommands.PrintCommand` (bindable routed command).
  - `PrintingTools.Print.IsEnabled="True"` toggles command wiring for `Button`, `MenuItem`.
  - `PrintingTools.Print.DocumentSource="{Binding SelectedDocument}"` for declarative configuration.
- Provide MVVM-friendly helpers:
  ```xaml
  <MenuItem Header="_Print"
            Command="{Binding PrintCommand}"
            printing:PrintingTools.Print.IsEnabled="True"
            printing:PrintingTools.Print.Description="Sales Report" />
  ```
- Expose `IPrintManager` via DI for manual invocation (`IPrintManager printManager` in view models).

## Public API Sketch (C#)
```csharp
public interface IPrintManager
{
    Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken ct = default);
    Task<PrintSession> RequestPrintSessionAsync(PrintRequest request, CancellationToken ct = default);
    PrintCapabilities GetCapabilities(PrinterId printerId, PrintTicketModel? baseTicket = null);
}

public sealed class PrintSession : IAsyncDisposable
{
    public PrinterInfo Printer { get; }
    public PrintTicketModel Ticket { get; }
    public PrintDocument Document { get; }
    public event EventHandler<JobProgressEventArgs>? JobProgress;
    public event EventHandler<JobCompletedEventArgs>? JobCompleted;
    public event EventHandler<JobFailedEventArgs>? JobFailed;

    public Task SubmitAsync(PrintSubmissionOptions options, CancellationToken ct = default);
    public Task ShowPreviewAsync(CancellationToken ct = default);
}

public abstract class PrintDocument
{
    public abstract IPaginator CreatePaginator(PrintTicketModel ticket);
}
```

## Document Sources

| Source | Description | WPF Analogue | Output |
| --- | --- | --- | --- |
| `VisualDocument` | Wraps an Avalonia `Visual` subtree. Captures via vector renderer, honoring `PrintLayoutHints`. | `Visual` printing | Produces single `PrintPage` per visual (with pagination hints). |
| `FlowDocumentAdapter` | Generates pages from rich text/section models (markdown, XAML). Uses layout engine inspired by WPF `FlowDocument`. | `FlowDocument` / `IDocumentPaginatorSource` | Emits multi-page sequence with dynamic pagination. |
| `FixedDocumentSource` | Replays pre-generated pages (PDF/XPS). Uses vector data or raster fallback. | `FixedDocument`, `FixedDocumentSequence` | Provides deterministic page set. |
| `CompositeDocument` | Aggregates multiple sources (visual + flow) into a single print job. | `FixedDocumentSequence` composition | Maintains order with shared ticket context. |

- Provide factory helpers: `PrintDocument.FromVisual(Visual visual)`, `PrintDocument.FromPaginator(IPaginator paginator)`, `PrintDocument.Combine(params PrintDocument[] documents)`.
- `FlowDocumentAdapter` consumes Avalonia text layout pipeline (`TextLayout`, `RichTextBlock`) to ensure consistent typography.
- `FixedDocumentSource` accepts `Stream` or file path; optional converters turn PDF/XPS into `PrintPage` instances (vector-first when supported).
- All sources implement `IPaginator` to supply `PrintPage` descriptors; interoperable with existing `PrintDocument` enumeration logic.
- Support metadata (title, author, tags) via `PrintDocumentProperties` used by native dialogs.

## Rendering & Preview Pipeline
- `PrintRenderPipeline.CollectPages(PrintSession, Vector)` captures paginated content, normalizes DPI, and applies page range filters based on `PrintOptions`.
- `PrintRenderPipeline.RenderBitmaps` turns collected pages into `RenderTargetBitmap` previews for thumbnails or zoom panes.
- `PrintRenderPipeline.TryCreateVectorDocument` produces PDF bytes through an `IVectorPageRenderer`, enabling vector-first workflows on macOS and future adapters.
- `PrintPreviewModel.Create` convenience method wraps the pipeline to build preview payloads (pages, optional bitmaps, optional vector document) in one call.
- Platform adapters (macOS, upcoming Windows) now reuse the shared pipeline for both preview and print paths, guaranteeing consistent pagination and DPI handling.

## Ticket Negotiation Workflow
1. `IPrintManager.GetCapabilities` fetches platform capabilities via adapter (`IPrintAdapter.GetCapabilitiesAsync`).
2. `PrintTicketModel.Merge(PrintTicketModel requested, PrintCapabilities caps)` mirrors WPF merge semantics; invalid settings downgraded with recorded warnings.
3. `PrintSession` stores resolved ticket; UI binds to `PrintTicketModel` for user edits.
4. Prior to submission, adapter converts `PrintTicketModel` to native structures (`DEVMODE` on Windows, `NSPrintInfo` on macOS).
5. Vendor extensions carried through via `PrintTicketModel.Extensions` dictionary (string key + JSON payload).

### Contract Details
- `PrintCapabilities` exposes:
  ```csharp
  public sealed class PrintCapabilities
  {
      public IReadOnlyList<PageMediaSizeInfo> PageMediaSizes { get; }
      public IReadOnlyList<PageOrientation> Orientations { get; }
      public DuplexingSupport Duplexing { get; }
      public IReadOnlyList<ColorMode> ColorModes { get; }
      public IReadOnlyList<int> SupportedCopyCounts { get; }
      public IReadOnlyDictionary<string, string> Extensions { get; }
  }
  ```
- `PrintTicketModel` structure:
  ```csharp
  public sealed class PrintTicketModel
  {
      public PageMediaSize PageMediaSize { get; set; }
      public PageOrientation Orientation { get; set; }
      public DuplexingMode Duplex { get; set; }
      public ColorMode ColorMode { get; set; }
      public int Copies { get; set; }
      public IDictionary<string, string> Extensions { get; }
      public IReadOnlyList<CapabilityWarning> Warnings { get; }
  }
  ```
- Capability negotiation algorithm:
  1. Start with requested ticket (defaults from `PrintingToolsOptions.DefaultTicket`).
  2. Validate each property against `PrintCapabilities`; fallback to device default when unsupported.
  3. Record downgrade warnings (`CapabilityWarning` with code + message).
  4. Merge extension payloads using adapter-specific logic (future provider hook tracked separately).
- Adapters translate native structures (Win32 `PRINTCAPABILITIES`, macOS `NSPrintInfo`) into managed `PrintCapabilities`.

### Success Metrics
- **Parity**: for devices supporting duplex/color/copies, negotiated ticket must match WPF `MergeAndValidatePrintTicket` outcome in integration tests.
- **Diagnostics**: every downgrade is logged via `IPrintDiagnosticsSink` with unique warning codes.
- **Extensibility**: vendor extensions stored via `PrintTicketModel.Extensions`; adapter-specific merge hook to be formalized in a future milestone.
- **Fallback**: when capability retrieval fails, system falls back to safe defaults (letter/A4, portrait, single copy) and surfaces warning to user.

## Lifecycle & Threading
- All UI interactions occur on Avalonia UI thread; heavy pagination can run on background tasks returning marshalled results.
- `PrintSession` ensures adapters invoked on correct thread (e.g., dispatch to macOS main thread for AppKit).
- Provide cancellation tokens for long-running pagination or job submission.

## Diagnostics & Logging
- `PrintDiagnosticsOptions` configures logging level, page render traces, ticket merge warnings.
- Expose `IPrintDiagnosticsSink` to enable per-app logging or telemetry.
- Provide built-in sinks: console, file, structured events consumable by CI tests.

## Open Questions
- Should `PrintSession` expose synchronous fallback APIs for simple usage? (Currently async-only.)
- How to version `PrintTicketModel.Extensions` schema for vendor plugins.
- Need to align with Avalonia internals for renderer access (`ImmediateRenderer`)—requires upstream coordination.
- Evaluate whether Windows adapter can host legacy WPF `PrintDialog`; identify HWND bridge constraints.

## Next Steps
- Review API draft with stakeholders (Phase 3 sign-off).
- Flesh out DI story (3.2) with XAML examples and service configuration details.
- Prototype `VisualDocument` paginator using existing `PrintingTools` pipeline to validate API feasibility.
