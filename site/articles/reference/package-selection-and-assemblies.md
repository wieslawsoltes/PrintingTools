---
title: "Package Selection and Assemblies"
---

# Package Selection and Assemblies

## Package map

| Package | Assembly | Primary concern |
| --- | --- | --- |
| `PrintingTools` | `PrintingTools` | Avalonia startup integration |
| `PrintingTools.Core` | `PrintingTools.Core` | Sessions, options, tickets, pagination, preview, diagnostics |
| `PrintingTools.UI` | `PrintingTools.UI` | Avalonia page setup and preview controls |
| `PrintingTools.Windows` | `PrintingTools.Windows` | Win32 adapter and rendering helpers |
| `PrintingTools.MacOS` | `PrintingTools.MacOS` | AppKit adapter, preview host, native bridge interop |
| `PrintingTools.Linux` | `PrintingTools.Linux` | CUPS and GTK-backed Linux adapter |

## Recommended combinations

| Scenario | Package set |
| --- | --- |
| Standard Avalonia desktop app | `PrintingTools` |
| Desktop app with packaged preview UI | `PrintingTools` + `PrintingTools.UI` |
| Headless export or integration tests | `PrintingTools.Core` + one platform package |
| Platform-specific troubleshooting | The platform package alone, plus `PrintingTools.Core` transitively |

## Related

- [Overview](../getting-started/overview.md)
- [Release and Packaging](../advanced/release-and-packaging.md)
