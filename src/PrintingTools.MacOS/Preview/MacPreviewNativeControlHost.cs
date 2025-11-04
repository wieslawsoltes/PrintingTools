using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace PrintingTools.MacOS.Preview;

/// <summary>
/// Hosts the macOS managed preview view inside an Avalonia visual tree.
/// </summary>
public sealed class MacPreviewNativeControlHost : NativeControlHost
{
    private readonly MacPreviewHost _previewHost;

    public MacPreviewNativeControlHost(MacPreviewHost previewHost)
    {
        _previewHost = previewHost ?? throw new ArgumentNullException(nameof(previewHost));
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = _previewHost.EnsureManagedPreviewView();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to obtain macOS preview view handle.");
        }

        return new PlatformHandle(handle, "NSView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        // The preview host owns the underlying view handle, so we intentionally
        // avoid destroying it here. The base implementation would attempt to
        // release the handle, which would double-free the native resources.
    }
}
