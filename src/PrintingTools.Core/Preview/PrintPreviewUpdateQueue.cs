using System;
using System.Threading;
using System.Threading.Tasks;
using PrintingTools.Core;

namespace PrintingTools.Core.Preview;

/// <summary>
/// Coordinates background preview generation requests, cancelling obsolete work while preserving order.
/// </summary>
public sealed class PrintPreviewUpdateQueue : IDisposable
{
    private readonly IPrintPreviewProvider _provider;
    private readonly object _gate = new();
    private CancellationTokenSource? _currentCts;
    private bool _disposed;

    public PrintPreviewUpdateQueue(IPrintPreviewProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public event EventHandler<PrintPreviewUpdatedEventArgs>? PreviewAvailable;

    public event EventHandler<Exception>? PreviewFailed;

    public void RequestUpdate(PrintSession session, PrintPreviewUpdateKind kind)
    {
        ArgumentNullException.ThrowIfNull(session);

        CancellationTokenSource? ctsToCancel = null;
        CancellationTokenSource? newCts;

        lock (_gate)
        {
            ThrowIfDisposed();

            if (_currentCts is { IsCancellationRequested: false })
            {
                ctsToCancel = _currentCts;
            }

            newCts = new CancellationTokenSource();
            _currentCts = newCts;
        }

        ctsToCancel?.Cancel();
        ctsToCancel?.Dispose();

        _ = Task.Run(async () =>
        {
            try
            {
                var preview = await _provider.CreatePreviewAsync(session, newCts!.Token).ConfigureAwait(false);
                PreviewAvailable?.Invoke(this, new PrintPreviewUpdatedEventArgs(preview, kind));
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation; a newer request is already pending.
            }
            catch (Exception ex)
            {
                PreviewFailed?.Invoke(this, ex);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_currentCts, newCts))
                    {
                        _currentCts = null;
                    }
                }

                newCts!.Dispose();
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource? toDispose;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toDispose = _currentCts;
            _currentCts = null;
        }

        toDispose?.Cancel();
        toDispose?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PrintPreviewUpdateQueue));
        }
    }
}
