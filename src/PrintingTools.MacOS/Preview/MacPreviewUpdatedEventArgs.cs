using System;
using PrintingTools.Core;
using PrintingTools.Core.Preview;

namespace PrintingTools.MacOS.Preview;

/// <summary>
/// Conveys preview updates alongside the native view that should be refreshed.
/// </summary>
public sealed class MacPreviewUpdatedEventArgs : EventArgs
{
    public MacPreviewUpdatedEventArgs(PrintPreviewModel preview, PrintPreviewUpdateKind kind, IntPtr nativeViewHandle)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        Kind = kind;
        NativeViewHandle = nativeViewHandle;
    }

    /// <summary>
    /// Gets the updated preview model. Ownership transfers to the receiver, who must dispose when finished.
    /// </summary>
    public PrintPreviewModel Preview { get; }

    /// <summary>
    /// Indicates what triggered the refresh.
    /// </summary>
    public PrintPreviewUpdateKind Kind { get; }

    /// <summary>
    /// Gets the native <c>NSView*</c> that should be invalidated/redrawn.
    /// </summary>
    public IntPtr NativeViewHandle { get; }
}
