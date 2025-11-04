using System;

namespace PrintingTools.Core.Preview;

/// <summary>
/// Carries a newly generated preview model to interested listeners.
/// </summary>
public sealed class PrintPreviewUpdatedEventArgs : EventArgs
{
    public PrintPreviewUpdatedEventArgs(PrintPreviewModel preview, PrintPreviewUpdateKind kind)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        Kind = kind;
    }

    /// <summary>
    /// Gets the refreshed preview content. Callers take ownership and must dispose when no longer needed.
    /// </summary>
    public PrintPreviewModel Preview { get; }

    /// <summary>
    /// Gets the reason the preview was refreshed.
    /// </summary>
    public PrintPreviewUpdateKind Kind { get; }
}
