---
title: "Overview"
---

# Overview

`PrintingTools` is a multi-package printing toolkit for .NET 10 and Avalonia applications that need a consistent print workflow across Windows, macOS, and Linux.

## What the project provides

- A shared orchestration layer built around <xref:PrintingTools.Core.IPrintManager>, <xref:PrintingTools.Core.PrintRequest>, <xref:PrintingTools.Core.PrintSession>, and <xref:PrintingTools.Core.PrintPreviewModel>.
- Platform adapters for Win32/XPS, AppKit/Quartz, and Linux CUPS or GTK-backed environments.
- Optional Avalonia UI for page setup and preview windows through `PrintingTools.UI`.
- CI-ready harnesses that exercise each platform path and validate metrics, accessibility, and golden output expectations.

## Package selection

| Scenario | Recommended packages |
| --- | --- |
| Avalonia app that wants automatic adapter selection | `PrintingTools` |
| Avalonia app that also wants packaged dialogs and preview windows | `PrintingTools` + `PrintingTools.UI` |
| Service or headless pipeline that only needs preview generation or PDF export | `PrintingTools.Core` + one platform package |
| Adapter-specific development or diagnostics | `PrintingTools.Windows`, `PrintingTools.MacOS`, or `PrintingTools.Linux` directly |

## End-to-end workflow

1. Configure the project once at startup through <xref:PrintingTools.PrintingToolsAppBuilderExtensions> or <xref:PrintingTools.Core.PrintServiceRegistry>.
2. Build a <xref:PrintingTools.Core.PrintDocument> from a visual or page enumerator.
3. Wrap the document in a <xref:PrintingTools.Core.PrintRequest>.
4. Ask <xref:PrintingTools.Core.IPrintManager> for a session with `RequestSessionAsync`.
5. Optionally call `CreatePreviewAsync` to obtain a <xref:PrintingTools.Core.PrintPreviewModel>.
6. Call `PrintAsync` to open a dialog or submit output directly, depending on `PrintOptions`.

## What is already covered in the repo

- Cross-platform NuGet packaging for six publishable assemblies.
- GitHub Actions validation for restore, build, tests, harness execution, and package packing.
- Generated API docs through Lunet `api.dotnet`.
- Existing operational notes preserved under `docs/` for deeper design history and parity tracking.

## Read next

- [Installation](installation.md)
- [Quickstart Avalonia](quickstart-avalonia.md)
- [Architecture and Service Model](../concepts/architecture-and-service-model.md)
