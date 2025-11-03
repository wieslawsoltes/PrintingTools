using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace PrintingTools.Core;

public sealed class PrintPreviewModel : IDisposable
{
    private bool _disposed;

    public PrintPreviewModel(IReadOnlyList<PrintPage> pages, IReadOnlyList<RenderTargetBitmap>? images = null, byte[]? vectorDocument = null)
    {
        Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        Images = images ?? Array.Empty<RenderTargetBitmap>();
        VectorDocument = vectorDocument;
    }

    public IReadOnlyList<PrintPage> Pages { get; }

    public IReadOnlyList<RenderTargetBitmap> Images { get; }

    public byte[]? VectorDocument { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var image in Images)
        {
            image?.Dispose();
        }

        _disposed = true;
    }
}
