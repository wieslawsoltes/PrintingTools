---
title: "Quickstart Avalonia"
---

# Quickstart Avalonia

This is the standard setup for a desktop Avalonia app that wants automatic adapter selection and reusable preview UI.

## 1. Add packages

```bash
dotnet add package PrintingTools
dotnet add package PrintingTools.UI
```

## 2. Register PrintingTools during app startup

```csharp
using Avalonia;
using PrintingTools;
using PrintingTools.Core;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UsePrintingTools(options =>
    {
        options.EnablePreview = true;
        options.DiagnosticSink = evt =>
            Console.WriteLine($"[{evt.Category}] {evt.Message}");
    })
    .StartWithClassicDesktopLifetime(args);
```

`UsePrintingTools(...)` configures <xref:PrintingTools.Core.PrintServiceRegistry> and resolves the right adapter for the active OS.

## 3. Create a print request

```csharp
using PrintingTools.Core;

var manager = PrintServiceRegistry.EnsureManager();

var document = PrintDocument.FromVisual(myPrintableControl);
var request = new PrintRequest(document)
{
    Description = "Quarterly summary",
    Ticket = PrintTicketModel.CreateDefault(),
    Options = new PrintOptions
    {
        ShowPrintDialog = true,
        UseVectorRenderer = true
    }
};

var session = await manager.RequestSessionAsync(request);
var preview = await manager.CreatePreviewAsync(session);
```

## 4. Show preview UI

```csharp
using PrintingTools.UI.Controls;
using PrintingTools.UI.ViewModels;

var viewModel = new PrintPreviewViewModel(preview.Pages, preview.VectorDocument);
viewModel.LoadPrinters(await manager.GetPrintersAsync(), session.Printer?.Id, session.Printer?.Name);
var window = new PrintPreviewWindow
{
    DataContext = viewModel
};

await window.ShowDialog(owner);
```

## 5. Submit the job

```csharp
await manager.PrintAsync(session);
```

## Common follow-ups

- Use `PageSetupDialog` to let users adjust margins, orientation, and layout modes before printing.
- Subscribe to diagnostics through `PrintingToolsOptions.DiagnosticSink` or `PrintDiagnostics.RegisterSink`.
- If you need full control over pagination, replace the default paginator with your own <xref:PrintingTools.Core.Pagination.IPrintPaginator>.

## Read next

- [Page Setup and Preview UI](../guides/page-setup-and-preview-ui.md)
- [Print Session Lifecycle](../concepts/print-session-lifecycle.md)
