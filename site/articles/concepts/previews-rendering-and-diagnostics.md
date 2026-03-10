---
title: "Previews, Rendering, and Diagnostics"
---

# Previews, Rendering, and Diagnostics

The preview and diagnostics pipeline is shared across all adapters.

## Preview model

<xref:PrintingTools.Core.PrintPreviewModel> contains paginated pages plus optional rendered assets. `PrintingTools.UI` consumes this model directly, and platform-specific preview hosts can do the same.

## Rendering pipeline

<xref:PrintingTools.Core.Rendering.PrintRenderPipeline> coordinates page collection, bitmap generation, and vector-document creation. The goal is simple: preview and final print should use the same page list and rendering assumptions.

## Vector vs raster

- Use vector rendering when you want smaller, higher-fidelity output.
- Use raster rendering when legacy drivers or environments cannot reliably consume vector payloads.

`PrintOptions.UseVectorRenderer` is the top-level switch, while <xref:PrintingTools.Core.Rendering.IVectorPageRenderer> allows platform packages to plug in their own vector exporters.

## Diagnostics

<xref:PrintingTools.Core.PrintDiagnostics> is the shared event hub for warnings, trace messages, and failures. Configure it through `PrintingToolsOptions.DiagnosticSink` or register additional sinks explicitly.

Typical diagnostic categories include:

- pagination and page-metric normalization
- vector document generation
- platform adapter capability downgrades
- CUPS, Win32, or AppKit integration failures

## Related

- [Samples and Harnesses](../guides/samples-and-harnesses.md)
- [Cross-Platform CI and Validation](../advanced/cross-platform-ci-and-validation.md)
