using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;
using PrintingTools.Linux;

namespace LinuxSandboxHarness;

internal static class Program
{
    private static readonly CancellationTokenSource Cancellation = new();

    public static async Task<int> Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            Cancellation.Cancel();
            e.Cancel = true;
        };

        var arguments = args ?? Array.Empty<string>();
        var shouldPrint = arguments.Contains("--print", StringComparer.OrdinalIgnoreCase);
        var requestDialog = arguments.Contains("--dialog", StringComparer.OrdinalIgnoreCase);
        var outputOverride = arguments.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
        var metricsOverride = arguments.FirstOrDefault(a => a.StartsWith("--metrics=", StringComparison.OrdinalIgnoreCase));
        var stressOverride = arguments.FirstOrDefault(a => a.StartsWith("--stress=", StringComparison.OrdinalIgnoreCase));

        var metricsPath = metricsOverride is null ? null : metricsOverride.Split('=', 2)[1];
        var stressIterations = ParseStressIterations(stressOverride);

        var managedPdf = Environment.GetEnvironmentVariable("PRINTINGTOOLS_SANDBOX_PDF");
        if (!string.IsNullOrWhiteSpace(outputOverride))
        {
            managedPdf = outputOverride.Split('=', 2)[1];
        }

        if (string.IsNullOrWhiteSpace(managedPdf))
        {
            managedPdf = Path.Combine(Environment.CurrentDirectory, "artifacts", "linux", "printingtools-linux.pdf");
        }

        Console.WriteLine("== Linux Sandbox Harness ==");
        Console.WriteLine($"PRINTINGTOOLS_SANDBOX_PDF={managedPdf}");
        Console.WriteLine($"GTK_USE_PORTAL={Environment.GetEnvironmentVariable("GTK_USE_PORTAL") ?? "<unset>"}");
        Console.WriteLine($"GIO_USE_PORTALS={Environment.GetEnvironmentVariable("GIO_USE_PORTALS") ?? "<unset>"}");
        Console.WriteLine($"XDG_RUNTIME_DIR={Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "<unset>"}");
        Console.WriteLine($"Metrics destination={metricsPath ?? "<none>"}");
        Console.WriteLine($"Stress iterations={stressIterations}");
        Console.WriteLine();

        var options = new PrintingToolsOptions
        {
            EnablePreview = true,
            AdapterFactory = () => new LinuxPrintAdapter(),
            DiagnosticSink = evt => Console.WriteLine($"[{evt.Timestamp:O}] {evt.Category}: {evt.Message}")
        };

        PrintServiceRegistry.Configure(options);
        var manager = PrintServiceRegistry.EnsureManager();

        try
        {
            var printers = await EnumeratePrintersAsync(manager, Cancellation.Token).ConfigureAwait(false);
            var metrics = await ExecuteHarnessAsync(
                manager,
                printers,
                managedPdf,
                requestDialog,
                shouldPrint,
                stressIterations,
                Cancellation.Token).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(metricsPath))
            {
                PersistMetrics(metrics, metricsPath);
            }

            LogMetrics(metrics);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Harness cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception: {ex}");
            return 1;
        }

    Console.WriteLine("Harness completed.");
    return 0;
    }

    private static int ParseStressIterations(string? stressOverride)
    {
        if (stressOverride is not null && int.TryParse(stressOverride.Split('=', 2)[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) ? 3 : 1;
    }

    private static async Task<IReadOnlyList<PrinterInfo>> EnumeratePrintersAsync(IPrintManager manager, CancellationToken token)
    {
        Console.WriteLine("Discovering printers via PrintingTools.Linux...");
        var printers = await manager.GetPrintersAsync(token).ConfigureAwait(false);

        if (printers.Count == 0)
        {
            Console.WriteLine("No printers detected. Ensure CUPS is reachable inside the sandbox.");
            Console.WriteLine();
            return printers;
        }

        for (var i = 0; i < printers.Count; i++)
        {
            var printer = printers[i];
            Console.WriteLine($"[{i}] {printer.Name} (Default={printer.IsDefault}, Local={printer.IsLocal})");
            foreach (var attribute in printer.Attributes)
            {
                Console.WriteLine($"    {attribute.Key} = {attribute.Value}");
            }

            try
            {
                var capabilities = await manager.GetCapabilitiesAsync(printer.Id, cancellationToken: token).ConfigureAwait(false);
                Console.WriteLine($"    Media sizes: {string.Join(", ", capabilities.PageMediaSizes.Select(m => m.Size.Name))}");
                Console.WriteLine($"    Duplex: {capabilities.Duplexing}");
                Console.WriteLine($"    Color modes: {string.Join(", ", capabilities.ColorModes)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Failed to query capabilities: {ex.Message}");
            }
        }

        Console.WriteLine();
        return printers;
    }

    private static async Task<HarnessMetrics> ExecuteHarnessAsync(
        IPrintManager manager,
        IReadOnlyList<PrinterInfo> printers,
        string pdfPath,
        bool requestDialog,
        bool shouldPrint,
        int stressIterations,
        CancellationToken token)
    {
        var metrics = new HarnessMetrics
        {
            Platform = "Linux",
            Scenario = "PrintingTools Linux Sandbox Harness",
            StressIterations = stressIterations
        };

        var showDialog = requestDialog;
        if (printers.Count == 0 && showDialog)
        {
            Console.WriteLine("No printers available; forcing managed headless flow.");
            showDialog = false;
        }

        if (showDialog && string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("CI environment detected; disabling native dialog presentation.");
            showDialog = false;
        }

        if (shouldPrint)
        {
            var directory = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        var visual = CreateHarnessVisual();
        var accessibility = AnalyzeAccessibility(visual);
        metrics.TotalControlCount = accessibility.TotalControls;
        metrics.AccessibilityIssueCount = accessibility.MissingNames;

        var document = PrintDocument.FromVisual(visual, new PrintPageSettings
        {
            TargetSize = new Size(612, 792),
            Margins = new Thickness(48)
        });

        var ticket = PrintTicketModel.CreateDefault();
        ticket.Extensions["linux.harness"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var options = new PrintOptions
        {
            ShowPrintDialog = showDialog,
            UseVectorRenderer = true,
            PdfOutputPath = shouldPrint ? pdfPath : null,
            JobName = "PrintingTools Linux Sandbox Harness",
            PaperSize = new Size(8.5, 11),
            Margins = new Thickness(0.5)
        };

        var request = new PrintRequest(document)
        {
            Description = "PrintingTools Linux Sandbox Harness",
            Options = options,
            Ticket = ticket
        };

        var sessionWatch = Stopwatch.StartNew();
        var session = await manager.RequestSessionAsync(request, token).ConfigureAwait(false);
        sessionWatch.Stop();
        metrics.SessionCreationMilliseconds = sessionWatch.Elapsed.TotalMilliseconds;

        var previewTimings = new List<double>(Math.Max(stressIterations, 1));
        PrintPreviewModel? lastPreview = null;
        for (var i = 0; i < Math.Max(1, stressIterations); i++)
        {
            var previewWatch = Stopwatch.StartNew();
            lastPreview = await manager.CreatePreviewAsync(session, token).ConfigureAwait(false);
            previewWatch.Stop();
            previewTimings.Add(previewWatch.Elapsed.TotalMilliseconds);
        }

        if (lastPreview is not null)
        {
            metrics.PageCount = lastPreview.Pages.Count;
        }

        metrics.MaxPreviewMilliseconds = previewTimings.Count > 0 ? Math.Max(previewTimings[0], previewTimings.Max()) : 0;
        metrics.AveragePreviewMilliseconds = previewTimings.Count > 0 ? previewTimings.Average() : 0;

        double printDuration = 0;
        if (shouldPrint)
        {
            var printWatch = Stopwatch.StartNew();
            await manager.PrintAsync(session, token).ConfigureAwait(false);
            printWatch.Stop();
            printDuration = printWatch.Elapsed.TotalMilliseconds;
        }

        metrics.PrintMilliseconds = printDuration;
        metrics.PeakMemoryBytes = GC.GetTotalMemory(forceFullCollection: true);
        metrics.TotalMilliseconds = metrics.SessionCreationMilliseconds + metrics.MaxPreviewMilliseconds + metrics.PrintMilliseconds;
        metrics.MetricsHash = ComputeMetricsHash(metrics);

        return metrics;
    }

    private static Control CreateHarnessVisual()
    {
        var border = new Border
        {
            Width = 612,
            Height = 792,
            Background = Brushes.White,
            BorderBrush = Brushes.DarkSlateBlue,
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Margin = new Thickness(48),
                Spacing = 16,
                Children =
                {
                    CreateHeader(),
                    CreateSummaryCard(),
                    CreateStatisticsRow(),
                    CreateFooter()
                }
            }
        };

        AutomationProperties.SetName(border, "Linux Harness Root");
        return border;
    }

    private static Control CreateHeader()
    {
        var header = new TextBlock
        {
            Text = "PrintingTools Linux Sandbox Harness",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        AutomationProperties.SetName(header, header.Text ?? "Header");
        return header;
    }

    private static Control CreateSummaryCard()
    {
        var panel = new Border
        {
            Background = Brushes.LightSteelBlue,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Summary", FontSize = 18, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = "Validates pagination, preview, and PDF export inside the CUPS sandbox." }
                }
            }
        };
        AutomationProperties.SetName(panel, "Summary Card");
        return panel;
    }

    private static Control CreateStatisticsRow()
    {
        var grid = new UniformGrid
        {
            Rows = 1,
            Columns = 3,
            Spacing = 12,
            Children =
            {
                CreateStatisticTile("Documents", "Flow/Fixed/Visual"),
                CreateStatisticTile("Layout Modes", "Standard/Booklet/Poster"),
                CreateStatisticTile("Outputs", "Vector PDF")
            }
        };
        AutomationProperties.SetName(grid, "Statistics Row");
        return grid;
    }

    private static Control CreateStatisticTile(string title, string subtitle)
    {
        var border = new Border
        {
            Background = Brushes.WhiteSmoke,
            BorderBrush = Brushes.SlateGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = subtitle, FontSize = 12, Foreground = Brushes.DimGray }
                }
            }
        };
        AutomationProperties.SetName(border, $"{title} Tile");
        return border;
    }

    private static Control CreateFooter()
    {
        var footer = new TextBlock
        {
            Text = $"Timestamp: {DateTimeOffset.Now:O}",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        AutomationProperties.SetName(footer, "Timestamp Text");
        return footer;
    }

    private static AccessibilityReport AnalyzeAccessibility(Control root)
    {
        var report = new AccessibilityReport();

        void Traverse(Visual visual)
        {
            if (visual is Control control)
            {
                report.TotalControls++;
                if (RequiresAutomationName(control) && string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
                {
                    report.MissingNames++;
                }
            }

            if (visual is IVisual visualNode)
            {
                foreach (var child in visualNode.VisualChildren)
                {
                    Traverse(child);
                }
            }
        }

        Traverse(root);
        return report;
    }

    private static bool RequiresAutomationName(Control control) =>
        control is TextBlock or Button or CheckBox;

    private static string ComputeMetricsHash(HarnessMetrics metrics)
    {
        var builder = new StringBuilder();
        builder.AppendLine(metrics.Platform);
        builder.AppendLine(metrics.Scenario);
        builder.AppendLine(metrics.PageCount.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.StressIterations.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.SessionCreationMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.MaxPreviewMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.AveragePreviewMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.PrintMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.PeakMemoryBytes.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.AccessibilityIssueCount.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine(metrics.TotalControlCount.ToString(CultureInfo.InvariantCulture));

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void PersistMetrics(HarnessMetrics metrics, string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            metrics.MetricsHash = ComputeMetricsHash(metrics);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(metrics, options));
            Console.WriteLine($"Metrics written to {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write metrics file '{path}': {ex.Message}");
        }
    }

    private static void LogMetrics(HarnessMetrics metrics)
    {
        Console.WriteLine("== Metrics ==");
        Console.WriteLine($"Platform: {metrics.Platform}");
        Console.WriteLine($"Scenario: {metrics.Scenario}");
        Console.WriteLine($"Stress Iterations: {metrics.StressIterations}");
        Console.WriteLine($"Pages: {metrics.PageCount}");
        Console.WriteLine($"Session Creation (ms): {metrics.SessionCreationMilliseconds:F2}");
        Console.WriteLine($"Preview Avg/Max (ms): {metrics.AveragePreviewMilliseconds:F2}/{metrics.MaxPreviewMilliseconds:F2}");
        Console.WriteLine($"Print (ms): {metrics.PrintMilliseconds:F2}");
        Console.WriteLine($"Peak Memory (bytes): {metrics.PeakMemoryBytes}");
        Console.WriteLine($"Accessibility Issues: {metrics.AccessibilityIssueCount}");
        Console.WriteLine($"Metrics Hash: {metrics.MetricsHash}");
        Console.WriteLine();
    }

    private sealed class HarnessMetrics
    {
        public string Platform { get; set; } = "Linux";

        public string Scenario { get; set; } = string.Empty;

        public int PageCount { get; set; }

        public int StressIterations { get; set; }

        public double SessionCreationMilliseconds { get; set; }

        public double MaxPreviewMilliseconds { get; set; }

        public double AveragePreviewMilliseconds { get; set; }

        public double PrintMilliseconds { get; set; }

        public double TotalMilliseconds { get; set; }

        public long PeakMemoryBytes { get; set; }

        public int AccessibilityIssueCount { get; set; }

        public int TotalControlCount { get; set; }

        public string MetricsHash { get; set; } = string.Empty;
    }

    private readonly record struct AccessibilityReport(int TotalControls, int MissingNames);
}
