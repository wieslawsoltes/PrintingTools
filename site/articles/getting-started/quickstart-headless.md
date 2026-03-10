---
title: "Quickstart Headless"
---

# Quickstart Headless

Use this flow when you want PDF generation, preview creation, or adapter smoke tests without presenting interactive UI.

## 1. Add the packages you need

```bash
dotnet add package PrintingTools.Core
dotnet add package PrintingTools.Linux
```

The same pattern applies for `PrintingTools.Windows` or `PrintingTools.MacOS`.

## 2. Configure the registry directly

```csharp
using PrintingTools.Core;
using PrintingTools.Linux;

PrintServiceRegistry.Configure(new PrintingToolsOptions
{
    AdapterFactory = () => new LinuxPrintAdapter(),
    EnablePreview = true,
    DiagnosticSink = evt =>
        Console.WriteLine($"[{evt.Category}] {evt.Message}")
});
```

## 3. Request a non-interactive session

```csharp
var manager = PrintServiceRegistry.EnsureManager();

var request = new PrintRequest(PrintDocument.FromVisual(printRoot))
{
    Description = "CI validation run",
    Options = new PrintOptions
    {
        ShowPrintDialog = false,
        PdfOutputPath = "artifacts/output.pdf",
        UseVectorRenderer = true
    },
    Ticket = PrintTicketModel.CreateDefault()
};

var session = await manager.RequestSessionAsync(request);
var preview = await manager.CreatePreviewAsync(session);
await manager.PrintAsync(session);
```

## 4. Validate the output

- Inspect `PdfOutputPath` output.
- Serialize preview or harness metrics when you need regression checks.
- Route diagnostics to STDOUT or a structured log sink so CI failures carry enough context.

## Existing harness examples

| Harness | Command |
| --- | --- |
| Windows | `dotnet run -c Release --project samples/WindowsPrintHarness/WindowsPrintHarness.csproj -- --output=artifacts/windows/output.pdf --metrics=artifacts/windows/metrics.json --stress=3` |
| macOS | `dotnet run -c Release --project samples/MacSandboxHarness/MacSandboxHarness.csproj -- --headless --output=artifacts/macos/output.pdf --metrics=artifacts/macos/metrics.json --stress=3` |
| Linux | `GTK_USE_PORTAL=1 GIO_USE_PORTALS=1 dotnet run -c Release --project samples/LinuxSandboxHarness/LinuxSandboxHarness.csproj -- --print --output=artifacts/linux/output.pdf --metrics=artifacts/linux/metrics.json --stress=3` |

## Read next

- [Samples and Harnesses](../guides/samples-and-harnesses.md)
- [Harness Baselines and Golden Metrics](../advanced/harness-baselines-and-golden-metrics.md)
