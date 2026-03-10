---
title: "Harness Baselines and Golden Metrics"
---

# Harness Baselines and Golden Metrics

PrintingTools uses tests and platform harnesses to keep pagination, rendering, and performance changes visible.

## Baseline files

| File | Purpose |
| --- | --- |
| `tests/PrintingTools.Tests/Baselines/golden-metrics.json` | Stable hashes for pagination and metrics scenarios. |
| `tests/PrintingTools.Tests/Baselines/harness-thresholds.json` | Time, memory, and accessibility thresholds for harness validation. |

## Update workflow

1. Run the affected harness and generate new metrics JSON.
2. Run the test project to capture the new golden hash or threshold deltas.
3. Update the baseline files deliberately.
4. Re-run tests to confirm the new baseline is stable.

## Why this matters

The project has platform-specific rendering paths. Golden hashes and threshold tests catch regressions that ordinary unit tests miss, especially around layout drift, platform defaults, and performance regressions.

## Related

- [Samples and Harnesses](../guides/samples-and-harnesses.md)
- [Cross-Platform CI and Validation](cross-platform-ci-and-validation.md)
