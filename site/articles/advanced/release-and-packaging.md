---
title: "Release and Packaging"
---

# Release and Packaging

PrintingTools is intended to ship as six NuGet packages.

## Published package set

- `PrintingTools`
- `PrintingTools.Core`
- `PrintingTools.UI`
- `PrintingTools.Windows`
- `PrintingTools.MacOS`
- `PrintingTools.Linux`

## Packaging expectations

- package metadata comes from `Directory.Build.props`
- symbol packages are generated as `.snupkg`
- the macOS package must contain `runtimes/osx/native/PrintingToolsMacBridge.dylib`
- release publishing requires `NUGET_API_KEY`

## Release workflow behavior

`release.yml` runs on tags matching `v*`, extracts the version from the tag name, packs all publishable projects, verifies the expected package set, and publishes to NuGet when credentials are present.

## Related

- [Package Selection and Assemblies](../reference/package-selection-and-assemblies.md)
- [Cross-Platform CI and Validation](cross-platform-ci-and-validation.md)
