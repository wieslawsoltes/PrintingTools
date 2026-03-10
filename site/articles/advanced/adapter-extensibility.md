---
title: "Adapter Extensibility"
---

# Adapter Extensibility

PrintingTools is intentionally layered so you can replace or extend parts of the pipeline without forking the whole stack.

## Main extension points

| Type | Purpose |
| --- | --- |
| <xref:PrintingTools.Core.IPrintAdapter> | Implement a new backend for a different platform or environment. |
| <xref:PrintingTools.Core.IPrintAdapterResolver> | Control how adapters are chosen for a request or session. |
| <xref:PrintingTools.Core.Pagination.IPrintPaginator> | Replace the default page splitting strategy. |
| <xref:PrintingTools.Core.Rendering.IVectorPageRenderer> | Plug in a custom vector exporter. |
| <xref:PrintingTools.Core.Preview.IPrintPreviewProvider> | Provide native or specialized preview surfaces. |

## Common customization pattern

The safest way to inject custom behavior is through `PrintingToolsOptions`:

```csharp
PrintServiceRegistry.Configure(new PrintingToolsOptions
{
    AdapterFactory = () => new MyCustomAdapter(),
    DefaultPaginator = new MyPaginator(),
    DiagnosticSink = evt => Logger.LogInformation("[Printing] {Category}: {Message}", evt.Category, evt.Message)
});
```

## Related

- [Architecture and Service Model](../concepts/architecture-and-service-model.md)
- [API Coverage Index](../reference/api-coverage-index.md)
