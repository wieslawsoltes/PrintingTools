using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

namespace PrintingTools.Tests;

public class HarnessMetricsThresholdTests
{
    [Fact]
    public void HarnessMetricsRespectThresholdsWhenProvided()
    {
        var metricsPath = Environment.GetEnvironmentVariable("HARNESS_METRICS_PATH");
        var platform = Environment.GetEnvironmentVariable("HARNESS_PLATFORM");

        if (string.IsNullOrWhiteSpace(metricsPath) || string.IsNullOrWhiteSpace(platform))
        {
            // No metrics supplied – nothing to validate in this execution context.
            return;
        }

        if (!File.Exists(metricsPath))
        {
            throw new XunitException($"Harness metrics file not found: {metricsPath}");
        }

        var thresholds = LoadThresholds();
        if (!thresholds.TryGetValue(platform, out var threshold))
        {
            throw new XunitException($"No threshold configuration found for platform '{platform}'.");
        }

        var metrics = JsonSerializer.Deserialize<HarnessMetrics>(File.ReadAllText(metricsPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize harness metrics at '{metricsPath}'.");

        Assert.True(metrics.PageCount >= threshold.MinPageCount, $"Expected at least {threshold.MinPageCount} pages, got {metrics.PageCount}.");
        Assert.InRange(metrics.SessionCreationMilliseconds, 0, threshold.MaxSessionMilliseconds);
        Assert.InRange(metrics.MaxPreviewMilliseconds, 0, threshold.MaxPreviewMilliseconds);
        Assert.InRange(metrics.PrintMilliseconds, 0, threshold.MaxPrintMilliseconds);
        Assert.True(metrics.AccessibilityIssueCount <= threshold.MaxAccessibilityIssues, $"Accessibility issues ({metrics.AccessibilityIssueCount}) exceed threshold ({threshold.MaxAccessibilityIssues}).");
        Assert.InRange(metrics.PeakMemoryBytes, 0, threshold.MaxPeakMemoryBytes);
        Assert.True(metrics.StressIterations >= threshold.MinStressIterations, $"Stress iterations ({metrics.StressIterations}) below minimum ({threshold.MinStressIterations}).");
        Assert.False(string.IsNullOrWhiteSpace(metrics.MetricsHash), "Metrics hash was not populated.");
    }

    private static Dictionary<string, HarnessThreshold> LoadThresholds()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Baselines", "harness-thresholds.json");
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("Harness threshold manifest not found.", file);
        }

        var json = File.ReadAllText(file);
        var manifest = JsonSerializer.Deserialize<Dictionary<string, HarnessThreshold>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return manifest ?? new Dictionary<string, HarnessThreshold>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class HarnessMetrics
    {
        public string Platform { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;

        public double SessionCreationMilliseconds { get; set; }

        public double MaxPreviewMilliseconds { get; set; }

        public double AveragePreviewMilliseconds { get; set; }

        public double PrintMilliseconds { get; set; }

        public double TotalMilliseconds { get; set; }

        public int PageCount { get; set; }

        public int StressIterations { get; set; }

        public int AccessibilityIssueCount { get; set; }

        public int TotalControlCount { get; set; }

        public long PeakMemoryBytes { get; set; }

        public string MetricsHash { get; set; } = string.Empty;
    }

    private sealed class HarnessThreshold
    {
        public int MinPageCount { get; set; } = 1;

        public double MaxSessionMilliseconds { get; set; } = 5000;

        public double MaxPreviewMilliseconds { get; set; } = 8000;

        public double MaxPrintMilliseconds { get; set; } = 20000;

        public int MaxAccessibilityIssues { get; set; } = 0;

        public long MaxPeakMemoryBytes { get; set; } = 250_000_000;

        public int MinStressIterations { get; set; } = 1;
    }
}
