using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using PrintingTools.Core.Rendering;

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

    public static PrintPreviewModel Create(
        PrintSession session,
        Vector targetDpi,
        bool includeBitmaps,
        bool includeVectorDocument,
        IVectorPageRenderer? vectorRenderer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var pages = PrintRenderPipeline.CollectPages(session, targetDpi, cancellationToken);
        IReadOnlyList<RenderTargetBitmap>? images = null;
        if (includeBitmaps)
        {
            images = PrintRenderPipeline.RenderBitmaps(pages, targetDpi, cancellationToken);
        }

        byte[]? vectorDocument = null;
        if (includeVectorDocument)
        {
            var renderer = vectorRenderer ?? throw new ArgumentNullException(nameof(vectorRenderer));
            vectorDocument = PrintRenderPipeline.TryCreateVectorDocument(pages, renderer);
        }

        return new PrintPreviewModel(pages, images, vectorDocument);
    }

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
