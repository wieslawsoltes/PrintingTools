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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;
using PrintingTools.Windows;

namespace WindowsPrintHarness;

internal static class Program
{
    private static readonly CancellationTokenSource ShutdownToken = new();

    public static async Task<int> Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            ShutdownToken.Cancel();
            e.Cancel = true;
        };

        var arguments = args ?? Array.Empty<string>();
        var requestDialog = arguments.Contains("--dialog", StringComparer.OrdinalIgnoreCase);
        var outputOverride = arguments.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
        var metricsOverride = arguments.FirstOrDefault(a => a.StartsWith("--metrics=", StringComparison.OrdinalIgnoreCase));
        var stressOverride = arguments.FirstOrDefault(a => a.StartsWith("--stress=", StringComparison.OrdinalIgnoreCase));

        var metricsPath = metricsOverride is null ? null : metricsOverride.Split('=', 2)[1];
        var stressIterations = ParseStressIterations(stressOverride);

        var pdfPath = Environment.GetEnvironmentVariable("PRINTINGTOOLS_WINDOWS_PDF");
        if (!string.IsNullOrWhiteSpace(outputOverride))
        {
            pdfPath = outputOverride.Split('=', 2)[1];
        }

        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            pdfPath = Path.Combine(Environment.CurrentDirectory, "artifacts", "windows", "printingtools-windows.pdf");
        }

        Console.WriteLine("== Windows Print Harness ==");
        Console.WriteLine($"PRINTINGTOOLS_WINDOWS_PDF={pdfPath}");
        Console.WriteLine($"Metrics destination={metricsPath ?? "<none>"}");
        Console.WriteLine($"Stress iterations={stressIterations}");
        Console.WriteLine();

        var factory = new Win32PrintAdapterFactory();
        if (!factory.IsSupported)
        {
            Console.WriteLine("Win32 print adapter is not supported on this platform.");
            return 1;
        }

        var options = new PrintingToolsOptions
        {
            EnablePreview = true,
            AdapterFactory = () => factory.CreateAdapter(),
            DiagnosticSink = evt => Console.WriteLine($"[{evt.Timestamp:O}] {evt.Category}: {evt.Message}")
        };

        PrintServiceRegistry.Configure(options);
        var manager = PrintServiceRegistry.EnsureManager();

        try
        {
            var printers = await manager.GetPrintersAsync(ShutdownToken.Token).ConfigureAwait(false);
            LogPrinters(printers);
            var metrics = await ExecuteHarnessAsync(manager, printers, pdfPath, requestDialog, stressIterations, ShutdownToken.Token).ConfigureAwait(false);

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

    private static void LogPrinters(IReadOnlyList<PrinterInfo> printers)
    {
        if (printers.Count == 0)
        {
            Console.WriteLine("No printers detected. Managed PDF export will still run.");
            Console.WriteLine();
            return;
        }

        for (var i = 0; i < printers.Count; i++)
        {
            var printer = printers[i];
            Console.WriteLine($"[{i}] {printer.Name} (Default={printer.IsDefault}, Local={printer.IsLocal})");
            foreach (var attribute in printer.Attributes)
            {
                Console.WriteLine($"    {attribute.Key} = {attribute.Value}");
            }
        }

        Console.WriteLine();
    }

    private static async Task<HarnessMetrics> ExecuteHarnessAsync(
        IPrintManager manager,
        IReadOnlyList<PrinterInfo> printers,
        string pdfPath,
        bool requestDialog,
        int stressIterations,
        CancellationToken token)
    {
        var metrics = new HarnessMetrics
        {
            Platform = "Windows",
            Scenario = "PrintingTools Windows Harness",
            StressIterations = stressIterations
        };

        var showDialog = requestDialog;
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) && showDialog)
        {
            Console.WriteLine("CI environment detected; disabling native dialog presentation.");
            showDialog = false;
        }

        var directory = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
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
        ticket.Extensions["windows.harness"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var options = new PrintOptions
        {
            ShowPrintDialog = showDialog,
            UseVectorRenderer = true,
            PdfOutputPath = pdfPath,
            JobName = "PrintingTools Windows Harness",
            PaperSize = new Size(8.5, 11),
            Margins = new Thickness(0.5)
        };

        if (printers.Count > 0)
        {
            options.PrinterName = printers[0].Name;
        }

        var request = new PrintRequest(document)
        {
            Description = "PrintingTools Windows Harness",
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

        var printWatch = Stopwatch.StartNew();
        await manager.PrintAsync(session, token).ConfigureAwait(false);
        printWatch.Stop();
        metrics.PrintMilliseconds = printWatch.Elapsed.TotalMilliseconds;

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
            BorderBrush = Brushes.SteelBlue,
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Margin = new Thickness(48),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "PrintingTools Windows Harness",
                        FontSize = 24,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = $"Timestamp: {DateTimeOffset.Now:O}\nMachine: {Environment.MachineName}\nUser: {Environment.UserName}",
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        AutomationProperties.SetName(border, "Windows Harness Root");
        foreach (var child in border.GetVisualDescendants())
        {
            if (child is Control control && control is TextBlock textBlock)
            {
                AutomationProperties.SetName(control, textBlock.Text ?? "Text");
            }
        }

        border.Measure(new Size(border.Width, border.Height));
        border.Arrange(new Rect(0, 0, border.Width, border.Height));
        return border;
    }

    private static IEnumerable<Visual> GetVisualDescendants(this Visual visual)
    {
        if (visual is IVisual parent)
        {
            foreach (var child in parent.VisualChildren)
            {
                yield return child;
                foreach (var grandChild in GetVisualDescendants(child))
                {
                    yield return grandChild;
                }
            }
        }
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
        public string Platform { get; set; } = "Windows";

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
