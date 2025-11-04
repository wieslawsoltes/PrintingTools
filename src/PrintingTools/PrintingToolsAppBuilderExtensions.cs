using System;
using Avalonia;
using PrintingTools.Core;
using PrintingTools.MacOS;
using PrintingTools.Linux;
using PrintingTools.Windows;

namespace PrintingTools;

/// <summary>
/// Extends the Avalonia <see cref="AppBuilder"/> with PrintingTools integration hooks.
/// </summary>
public static class PrintingToolsAppBuilderExtensions
{
    public static AppBuilder UsePrintingTools(this AppBuilder builder, Action<PrintingToolsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var baseOptions = new PrintingToolsOptions();
        configure?.Invoke(baseOptions);

        var linuxFactory = new LinuxPrintAdapterFactory();
        var macFactory = new MacPrintAdapterFactory();
        var windowsFactory = new Win32PrintAdapterFactory();

        return builder.AfterSetup(_ =>
        {
            var options = baseOptions.Clone();

            if (options.AdapterFactory is null)
            {
                if (windowsFactory.IsSupported)
                {
                    options.AdapterFactory = () => windowsFactory.CreateAdapter() ?? throw new PlatformNotSupportedException("Windows printing is unavailable.");
                }
                else if (linuxFactory.IsSupported)
                {
                    options.AdapterFactory = () => linuxFactory.CreateAdapter() ?? throw new PlatformNotSupportedException("Linux printing is unavailable.");
                }
                else if (macFactory.IsSupported)
                {
                    options.AdapterFactory = () => macFactory.CreateAdapter() ?? throw new PlatformNotSupportedException("macOS printing is unavailable.");
                }
            }

            PrintServiceRegistry.Configure(options);
        });
    }

    public static IPrintManager GetPrintManager() => PrintServiceRegistry.EnsureManager();

    public static PrintingToolsOptions GetPrintingOptions() => PrintServiceRegistry.Options;
}
