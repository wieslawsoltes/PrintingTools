using System;
using Avalonia;
using Avalonia.Headless;
using System.Threading;
using System.Threading.Tasks;

namespace PrintingTools.SampleHarnesses;

internal static class HarnessAvaloniaBootstrap
{
    public static bool IsHeadless => true;

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
    {
        return HeadlessUnitTestSession.StartNew(typeof(HeadlessHarnessEntryPoint));
    });

    public static void EnsureInitialized()
    {
        _ = Session.Value;
    }

    public static T Invoke<T>(Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureInitialized();
        return Session.Value.Dispatch(callback, CancellationToken.None).GetAwaiter().GetResult();
    }

    public static void Invoke(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureInitialized();
        Session.Value.Dispatch(callback, CancellationToken.None).GetAwaiter().GetResult();
    }

    public static Task InvokeAsync(Func<Task> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureInitialized();
        return Session.Value.Dispatch(callback, cancellationToken);
    }

    public static Task<T> InvokeAsync<T>(Func<Task<T>> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        EnsureInitialized();
        return Session.Value.Dispatch(callback, cancellationToken);
    }

    private sealed class HarnessApplication : Application;

    private static class HeadlessHarnessEntryPoint
    {
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder
                .Configure<HarnessApplication>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true
                });
    }
}
