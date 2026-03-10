---
title: "Namespace: Platform Packages"
---

# Namespace: Platform Packages

These namespaces provide the concrete adapter implementations and their platform-specific helpers.

## Entry points by package

### `PrintingTools`

- <xref:PrintingTools.PrintingToolsAppBuilderExtensions>

### `PrintingTools.Windows`

- <xref:PrintingTools.Windows.Win32PrintAdapterFactory>
- internal rendering helpers live under `PrintingTools.Windows.Rendering`

### `PrintingTools.MacOS`

- <xref:PrintingTools.MacOS.MacPrintAdapter>
- <xref:PrintingTools.MacOS.MacPrintAdapterFactory>
- <xref:PrintingTools.MacOS.MacPrinterCatalog>
- <xref:PrintingTools.MacOS.MacPrintUtilities>
- <xref:PrintingTools.MacOS.Preview.MacPreviewHost>
- <xref:PrintingTools.MacOS.Preview.MacPreviewNativeControlHost>

### `PrintingTools.Linux`

- <xref:PrintingTools.Linux.LinuxPrintAdapter>
- <xref:PrintingTools.Linux.LinuxPrintAdapterFactory>
- internal command and dialog helpers live under `PrintingTools.Linux` and `PrintingTools.Linux.Dialogs`

## Related

- [Windows](../platforms/windows.md)
- [macOS](../platforms/macos.md)
- [Linux](../platforms/linux.md)
