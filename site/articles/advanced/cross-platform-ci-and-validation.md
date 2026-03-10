---
title: "Cross-Platform CI and Validation"
---

# Cross-Platform CI and Validation

The repository validates both the code and the shipped artifacts in GitHub Actions.

## Main workflows

| Workflow | Purpose |
| --- | --- |
| `ci.yml` | Restore, build, test, run harnesses, validate docs, and verify package packing. |
| `release.yml` | Build release packages on tags, verify package contents, and publish to NuGet. |
| `docs.yml` | Build and publish the Lunet documentation site to GitHub Pages on the primary branch. |

## CI stages

- cross-platform managed build and test
- macOS native bridge validation
- platform harness execution
- docs site build
- NuGet pack verification

## Why the harness jobs are separate

Each harness needs a native OS environment and platform-specific prerequisites. Splitting them out makes failures easier to isolate and preserves artifacts such as logs, metrics, and generated PDFs.

## Related

- [Harness Baselines and Golden Metrics](harness-baselines-and-golden-metrics.md)
- [Lunet Docs Pipeline](../reference/lunet-docs-pipeline.md)
