using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using PrintingTools.Core;
using PrintingTools.Core.Preview;
using PrintingTools.Core.Rendering;
using PrintingTools.MacOS;
using PrintingTools.MacOS.Native;
using PrintingTools.MacOS.Rendering;

namespace PrintingTools.MacOS.Preview;

/// <summary>
/// Coordinates native preview hosting for macOS by bridging Avalonia preview providers to AppKit views.
/// </summary>
public sealed class MacPreviewHost : IDisposable
{
    private static readonly Vector SheetPreviewDpi = new(300, 300);
    private readonly PrintPreviewUpdateQueue _queue;
    private readonly object _gate = new();
    private PrintSession? _session;
    private PrintPreviewModel? _currentPreview;
    private bool _disposed;
    private IntPtr _nativeViewHandle;
    private IntPtr _managedHostHandle;
    private IntPtr _managedViewHandle;

    private static readonly IVectorPageRenderer PreviewVectorRenderer = new SkiaVectorPageRenderer();
    private const double ValidationTolerance = 0.25;
    private const string DiagnosticsCategory = "MacPreviewHost";

    public static IntPtr CreateHostWindow(double width, double height) =>
        PrintingToolsInterop.CreateHostWindow(width, height);

    public static void ShowHostWindow(IntPtr windowHandle) =>
        PrintingToolsInterop.ShowWindow(windowHandle);

    public static void DestroyHostWindow(IntPtr windowHandle) =>
        PrintingToolsInterop.DestroyHostWindow(windowHandle);

    public MacPreviewHost(IPrintPreviewProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _queue = new PrintPreviewUpdateQueue(provider);
        _queue.PreviewAvailable += OnPreviewAvailable;
        _queue.PreviewFailed += OnPreviewFailed;
    }

    public event EventHandler<MacPreviewUpdatedEventArgs>? PreviewUpdated;

    public event EventHandler<Exception>? PreviewFailed;

    /// <summary>
    /// Gets the underlying native view handle once a view is attached.
    /// </summary>
    public IntPtr NativeViewHandle
    {
        get
        {
            lock (_gate)
            {
                return _nativeViewHandle;
            }
        }
    }

    /// <summary>
    /// Attaches an AppKit view handle that the host should drive. The caller is responsible for creating the view.
    /// </summary>
    /// <param name="viewHandle">The native view pointer, typically an <c>NSView*</c>.</param>
    public void AttachNativeView(IntPtr viewHandle)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _nativeViewHandle = viewHandle;
        }
    }

    /// <summary>
    /// Ensures a managed preview view is created and returns its native handle.
    /// </summary>
    public IntPtr EnsureManagedPreviewView()
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            if (_managedHostHandle == IntPtr.Zero)
            {
                _managedHostHandle = PrintingToolsInterop.CreateManagedPreviewHost();
                if (_managedHostHandle != IntPtr.Zero)
                {
                    _managedViewHandle = PrintingToolsInterop.GetManagedPreviewView(_managedHostHandle);
                }
            }

            return _managedViewHandle;
        }
    }

    public void AttachManagedPreviewToWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Window handle must not be zero.", nameof(windowHandle));
        }

        var viewHandle = EnsureManagedPreviewView();
        if (viewHandle != IntPtr.Zero)
        {
            PrintingToolsInterop.SetWindowContent(windowHandle, viewHandle);
        }
    }

    public void LoadPreview(PrintPreviewModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        EnsureManagedPreviewView();

        IntPtr hostHandle;
        lock (_gate)
        {
            ThrowIfDisposed();
            hostHandle = _managedHostHandle;
        }

        if (hostHandle != IntPtr.Zero)
        {
            UpdateManagedPreview(hostHandle, preview);
            ValidatePreviewSnapshot(preview);
        }
    }

    /// <summary>
    /// Sets the session whose content should be previewed. Triggers the initial preview render.
    /// </summary>
    public void Initialize(PrintSession session, PrintPreviewUpdateKind kind = PrintPreviewUpdateKind.Initial)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            ThrowIfDisposed();
            _session = session;
        }

        _queue.RequestUpdate(session, kind);
    }

    /// <summary>
    /// Requests a refresh using the last assigned session.
    /// </summary>
    public void RequestRefresh(PrintPreviewUpdateKind kind)
    {
        PrintSession? session;
        lock (_gate)
        {
            ThrowIfDisposed();
            session = _session;
        }

        if (session is null)
        {
            throw new InvalidOperationException("Preview host must be initialised before requesting refresh operations.");
        }

        _queue.RequestUpdate(session, kind);
    }

    public bool PresentPrintPanelSheet(PrintSession session, IntPtr windowHandle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Window handle must not be zero.", nameof(windowHandle));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MacPreviewHost));
            }
        }

        IReadOnlyList<PrintPage> pages = PrintRenderPipeline.CollectPages(session, SheetPreviewDpi, cancellationToken);

        var handled = global::PrintingTools.MacOS.MacPrintAdapter.ShowNativePrintPanel(
            session,
            pages,
            NativePrintPanelMode.Sheet,
            windowHandle,
            out var previewViewHandle,
            mutateSession: false);

        if (previewViewHandle != IntPtr.Zero)
        {
            AttachNativeView(previewViewHandle);
        }

        _queue.RequestUpdate(session, PrintPreviewUpdateKind.RefreshRequested);
        return handled;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        PrintPreviewModel? toDispose = null;
        IntPtr managedHost = IntPtr.Zero;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.PreviewAvailable -= OnPreviewAvailable;
            _queue.PreviewFailed -= OnPreviewFailed;
            toDispose = _currentPreview;
            _currentPreview = null;
            _session = null;
            _nativeViewHandle = IntPtr.Zero;
            managedHost = _managedHostHandle;
            _managedHostHandle = IntPtr.Zero;
            _managedViewHandle = IntPtr.Zero;
        }

        _queue.Dispose();
        toDispose?.Dispose();

        if (managedHost != IntPtr.Zero)
        {
            PrintingToolsInterop.DestroyManagedPreviewHost(managedHost);
        }
    }

    private void OnPreviewAvailable(object? sender, PrintPreviewUpdatedEventArgs e)
    {
        MacPreviewUpdatedEventArgs? args;
        PrintPreviewModel? previous;
        IntPtr managedHost;

        lock (_gate)
        {
            if (_disposed)
            {
                e.Preview.Dispose();
                return;
            }

            previous = _currentPreview;
            _currentPreview = e.Preview;
            args = new MacPreviewUpdatedEventArgs(e.Preview, e.Kind, _nativeViewHandle);
            managedHost = _managedHostHandle;
        }

        previous?.Dispose();

        if (managedHost != IntPtr.Zero)
        {
            UpdateManagedPreview(managedHost, e.Preview);
        }

        ValidatePreviewSnapshot(e.Preview);
        PreviewUpdated?.Invoke(this, args);
    }

    private void OnPreviewFailed(object? sender, Exception exception)
    {
        if (_disposed)
        {
            return;
        }

        PreviewFailed?.Invoke(this, exception);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MacPreviewHost));
        }
    }

    private static byte[]? EnsureVectorDocument(PrintPreviewModel preview)
    {
        if (preview.VectorDocument is { Length: > 0 } vector)
        {
            return vector;
        }

        var regenerated = PrintRenderPipeline.TryCreateVectorDocument(preview.Pages, PreviewVectorRenderer);
        return regenerated is { Length: > 0 } bytes ? bytes : null;
    }

    private static void UpdateManagedPreview(IntPtr hostHandle, PrintPreviewModel preview)
    {
        var pdf = EnsureVectorDocument(preview);
        if (pdf is null)
        {
            return;
        }

        var metrics = TryGetPrimaryMetrics(preview);
        var dpi = metrics?.Dpi.X ?? SheetPreviewDpi.X;

        PrintingToolsInterop.UpdateManagedPreviewWithPdf(hostHandle, pdf, pdf.Length, dpi);
    }

    private static void ValidatePreviewSnapshot(PrintPreviewModel preview)
    {
        var metrics = TryGetPrimaryMetrics(preview);
        if (metrics is null)
        {
            return;
        }

        var deltaX = Math.Abs(metrics.Dpi.X - SheetPreviewDpi.X);
        var deltaY = Math.Abs(metrics.Dpi.Y - SheetPreviewDpi.Y);

        if (deltaX > ValidationTolerance || deltaY > ValidationTolerance)
        {
            PrintDiagnostics.Report(
                DiagnosticsCategory,
                $"Preview DPI mismatch (expected {SheetPreviewDpi.X:F2}x{SheetPreviewDpi.Y:F2}, actual {metrics.Dpi.X:F2}x{metrics.Dpi.Y:F2}).",
                context: new
                {
                    PageIndex = 0,
                    metrics.Dpi,
                    metrics.PageSize
                });
        }
    }

    private static PrintPageMetrics? TryGetPrimaryMetrics(PrintPreviewModel preview)
    {
        if (preview.Pages.Count == 0)
        {
            return null;
        }

        var page = preview.Pages[0];
        return page.Metrics ?? PrintPageMetrics.Create(page.Visual, page.Settings, SheetPreviewDpi);
    }
}
