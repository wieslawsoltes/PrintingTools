---
title: "Architecture and Service Model"
---

# Architecture and Service Model

PrintingTools is split into a shared orchestration assembly plus optional UI and platform-specific adapters.

## Assembly responsibilities

| Assembly | Responsibility |
| --- | --- |
| `PrintingTools.Core` | Sessions, requests, tickets, capabilities, pagination, preview models, rendering helpers, and diagnostics. |
| `PrintingTools` | Avalonia startup extensions that register the correct platform adapter automatically. |
| `PrintingTools.UI` | Reusable preview and page setup controls. |
| `PrintingTools.Windows` | Win32 spooler integration, dialog handling, and XPS/PDF output. |
| `PrintingTools.MacOS` | AppKit bridge, native preview hosting, and Quartz-driven print flows. |
| `PrintingTools.Linux` | CUPS command integration, GTK or portal-aware dialogs, and managed PDF submission. |

## Core service graph

- <xref:PrintingTools.Core.PrintServiceRegistry> owns process-wide configuration.
- <xref:PrintingTools.Core.PrintingToolsOptions> carries adapter factories, preview defaults, and diagnostic sinks.
- <xref:PrintingTools.Core.IPrintManager> is the single orchestration surface applications use.
- <xref:PrintingTools.Core.IPrintAdapter> hides platform-specific queue discovery, capability lookup, preview generation, and job submission.

## Runtime adapter resolution

If you use <xref:PrintingTools.PrintingToolsAppBuilderExtensions>, the library selects the adapter appropriate for the current OS at startup. If you need explicit control, set `PrintingToolsOptions.AdapterFactory` yourself.

## Why the split matters

- It keeps the core API stable even when native backends differ.
- It allows headless or service-style flows to reference only the necessary platform package.
- It keeps optional UI separate from the print pipeline itself.

## Related

- [Print Session Lifecycle](print-session-lifecycle.md)
- [Package Selection and Assemblies](../reference/package-selection-and-assemblies.md)
