---
title: "Page Setup and Preview UI"
---

# Page Setup and Preview UI

`PrintingTools.UI` supplies reusable Avalonia controls for page setup and preview flows.

## Main UI types

| Type | Role |
| --- | --- |
| <xref:PrintingTools.UI.Controls.PageSetupDialog> | Embeddable page setup user control. |
| <xref:PrintingTools.UI.Controls.PageSetupWindow> | Modal shell for the setup dialog. |
| <xref:PrintingTools.UI.Controls.PrintPreviewWindow> | Packaged preview window with navigation, zoom, print, export, and printer refresh actions. |
| <xref:PrintingTools.UI.Controls.PrintPageVectorView> | Vector-page presentation surface used by the preview experience. |
| <xref:PrintingTools.UI.ViewModels.PageSetupViewModel> | Maps `PrintOptions` into editable UI state and applies changes back. |
| <xref:PrintingTools.UI.ViewModels.PrintPreviewViewModel> | Holds preview pages, printers, zoom, and action requests. |

## Page setup flow

```csharp
using PrintingTools.Core;
using PrintingTools.UI.Controls;
using PrintingTools.UI.ViewModels;

var viewModel = new PageSetupViewModel();
viewModel.LoadFrom(currentOptions);

var window = new PageSetupWindow(viewModel);
var applied = await window.ShowDialog<bool>(owner);

if (applied)
{
    currentOptions = viewModel.ApplyTo(currentOptions);
}
```

## Preview flow

```csharp
using PrintingTools.Core;
using PrintingTools.UI.Controls;
using PrintingTools.UI.ViewModels;

var preview = await manager.CreatePreviewAsync(session);
var viewModel = new PrintPreviewViewModel(preview.Pages, preview.VectorDocument);
viewModel.LoadPrinters(await manager.GetPrintersAsync(), session.Printer?.Id, session.Printer?.Name);

var window = new PrintPreviewWindow(viewModel);
await window.ShowDialog(owner);
```

## Handling preview actions

`PrintPreviewViewModel` raises `ActionRequested` so the host application can decide what to do when the user presses Print, Export PDF, refreshes printers, or requests vector preview.

## Native preview integration

On macOS, <xref:PrintingTools.MacOS.Preview.MacPreviewHost> and <xref:PrintingTools.MacOS.Preview.MacPreviewNativeControlHost> can be embedded into `PrintPreviewWindow.NativePreviewContent` when you want a native preview surface alongside the managed page list.

## Related

- [Quickstart Avalonia](../getting-started/quickstart-avalonia.md)
- [Samples and Harnesses](samples-and-harnesses.md)
