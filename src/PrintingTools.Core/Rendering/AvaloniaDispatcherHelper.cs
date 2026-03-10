using System;
using System.Threading;
using Avalonia.Threading;

namespace PrintingTools.Core.Rendering;

internal static class AvaloniaDispatcherHelper
{
    public static T Invoke<T>(Func<T> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (Dispatcher.UIThread.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            return callback();
        }

        return Dispatcher.UIThread
            .InvokeAsync(callback, DispatcherPriority.Send, cancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    public static void Invoke(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (Dispatcher.UIThread.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return;
        }

        Dispatcher.UIThread
            .InvokeAsync(callback, DispatcherPriority.Send, cancellationToken)
            .GetAwaiter()
            .GetResult();
    }
}
