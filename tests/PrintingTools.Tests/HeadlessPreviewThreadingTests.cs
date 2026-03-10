using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using PrintingTools.Core;
using Xunit;

namespace PrintingTools.Tests;

[Collection("AvaloniaHeadless")]
public sealed class HeadlessPreviewThreadingTests
{
    [Fact]
    public async Task PreviewCreationCanRenderBitmapsFromBackgroundThread()
    {
        using var headlessSession = HeadlessUnitTestSession.StartNew(typeof(HeadlessPreviewEntryPoint));

        await headlessSession.Dispatch(async () =>
        {
            var visual = new PreviewTestVisual
            {
                Width = 612,
                Height = 792
            };

            visual.Measure(new Size(visual.Width, visual.Height));
            visual.Arrange(new Rect(0, 0, visual.Width, visual.Height));

            var document = PrintDocument.FromVisual(visual, new PrintPageSettings
            {
                TargetSize = new Size(612, 792),
                Margins = new Thickness(24)
            });

            var printSession = new PrintSession(document, new PrintOptions
            {
                UseVectorRenderer = false
            });

            var manager = new PrintManager(
                new TestPrintAdapterResolver(new AsyncPreviewAdapter()),
                new PrintingToolsOptions
                {
                    EnablePreview = true
                });

            using var preview = await manager.CreatePreviewAsync(printSession);

            Assert.NotEmpty(preview.Pages);
            Assert.NotEmpty(preview.Images);
        }, CancellationToken.None);
    }

    private sealed class HeadlessPreviewApplication : Application;

    private sealed class PreviewTestVisual : Control
    {
        public override void Render(DrawingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
            context.FillRectangle(Brushes.DarkSlateBlue, new Rect(32, 32, Bounds.Width - 64, Bounds.Height - 64));
        }
    }

    private sealed class AsyncPreviewAdapter : IPrintAdapter
    {
        public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            return Array.Empty<PrinterInfo>();
        }

        public Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(PrintCapabilities.CreateDefault());

        public Task<PrintSession> CreateSessionAsync(PrintRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new NotSupportedException();
        }

        public async Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            return PrintPreviewModel.Create(
                session,
                new Vector(144, 144),
                includeBitmaps: true,
                includeVectorDocument: false,
                cancellationToken: cancellationToken);
        }
    }

    private sealed class TestPrintAdapterResolver : IPrintAdapterResolver
    {
        private readonly IPrintAdapter _adapter;

        public TestPrintAdapterResolver(IPrintAdapter adapter)
        {
            _adapter = adapter;
        }

        public IPrintAdapter Resolve() => _adapter;

        public IPrintAdapter Resolve(PrintSession session) => _adapter;
    }

    private static class HeadlessPreviewEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder
                .Configure<HeadlessPreviewApplication>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true
                });
    }
}
