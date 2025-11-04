using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using PrintingTools.Core;
using PrintingTools.Core.Rendering;
using PrintingTools.Tests.Validation;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace PrintingTools.Tests;

public class GoldenMetricsTests
{
    private readonly ITestOutputHelper _output;

    public GoldenMetricsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ScenarioMetricsMatchBaseline()
    {
        var manifest = LoadBaseline();
        var context = new ValidationScenarioContext();
        var dpi = context.TargetDpi;

        foreach (var scenario in ValidationScenarios.All)
        {
            _output.WriteLine($"Executing scenario: {scenario.Name}");
            var result = scenario.Execute(context);

            var pages = PrintRenderPipeline.CollectPages(result.Session, dpi);
            var metricsHash = ComputeMetricsHash(pages);
            _output.WriteLine($"Computed hash: {metricsHash}");

            if (!manifest.TryGetValue(scenario.Name, out var baseline))
            {
                _output.WriteLine($"Scenario '{scenario.Name}' not found in baseline manifest.");
                throw new XunitException($"Missing baseline entry for scenario '{scenario.Name}'. Actual hash: {metricsHash}");
            }

            Assert.Equal(baseline.PageCount, pages.Count);
            Assert.Equal(baseline.MetricsHash, metricsHash);
        }
    }

    private static Dictionary<string, MetricsBaselineEntry> LoadBaseline()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Baselines", "golden-metrics.json");
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("Golden metrics baseline manifest not found.", file);
        }

        var json = File.ReadAllText(file);
        var manifest = JsonSerializer.Deserialize<MetricsBaselineManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize golden metrics manifest.");

        return manifest.Scenarios?.ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, MetricsBaselineEntry>(StringComparer.OrdinalIgnoreCase);
    }

    private static string ComputeMetricsHash(IReadOnlyList<PrintPage> pages)
    {
        var builder = new StringBuilder();
        builder.AppendLine(pages.Count.ToString());

        for (var i = 0; i < pages.Count; i++)
        {
            var metrics = pages[i].Metrics ?? PrintPageMetrics.Create(pages[i].Visual, pages[i].Settings, new Vector(144, 144));
            builder.Append(i);
            builder.Append('|');
            AppendMetrics(builder, metrics);
            builder.AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static void AppendMetrics(StringBuilder builder, PrintPageMetrics metrics)
    {
        builder
            .Append(metrics.PageSize.Width.ToString("F3"))
            .Append(',')
            .Append(metrics.PageSize.Height.ToString("F3"))
            .Append(',')
            .Append(metrics.Margins.Left.ToString("F3"))
            .Append(',')
            .Append(metrics.Margins.Top.ToString("F3"))
            .Append(',')
            .Append(metrics.Margins.Right.ToString("F3"))
            .Append(',')
            .Append(metrics.Margins.Bottom.ToString("F3"))
            .Append(',')
            .Append(metrics.ContentRect.Width.ToString("F3"))
            .Append(',')
            .Append(metrics.ContentRect.Height.ToString("F3"))
            .Append(',')
            .Append(metrics.ContentScale.ToString("F6"))
            .Append(',')
            .Append(metrics.Dpi.X.ToString("F3"))
            .Append(',')
            .Append(metrics.Dpi.Y.ToString("F3"))
            .Append(',')
            .Append(metrics.PagePixelSize.Width)
            .Append(',')
            .Append(metrics.PagePixelSize.Height)
            .Append(',')
            .Append(metrics.ContentPixelRect.X)
            .Append(',')
            .Append(metrics.ContentPixelRect.Y)
            .Append(',')
            .Append(metrics.ContentPixelRect.Width)
            .Append(',')
            .Append(metrics.ContentPixelRect.Height)
            .Append(',')
            .Append(metrics.VisualBounds.Width.ToString("F3"))
            .Append(',')
            .Append(metrics.VisualBounds.Height.ToString("F3"))
            .Append(',')
            .Append(metrics.ContentOffset.X.ToString("F3"))
            .Append(',')
            .Append(metrics.ContentOffset.Y.ToString("F3"));
    }

    private sealed class MetricsBaselineManifest
    {
        public MetricsBaselineEntry[]? Scenarios { get; set; }
    }

    private sealed class MetricsBaselineEntry
    {
        public string Name { get; set; } = string.Empty;

        public int PageCount { get; set; }

        public string MetricsHash { get; set; } = string.Empty;
    }
}
