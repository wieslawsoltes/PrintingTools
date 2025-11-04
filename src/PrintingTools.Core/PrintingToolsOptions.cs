using System;

namespace PrintingTools.Core;

using PrintingTools.Core.Pagination;

public sealed class PrintingToolsOptions
{
    public bool EnablePreview { get; set; } = true;

    public Func<IPrintAdapter>? AdapterFactory { get; set; }

    public Action<PrintDiagnosticEvent>? DiagnosticSink { get; set; }

    public PrintTicketModel DefaultTicket { get; set; } = PrintTicketModel.CreateDefault();

    public IPrintPaginator DefaultPaginator { get; set; } = DefaultPrintPaginator.Instance;

    public PrintingToolsOptions Clone() =>
        new()
        {
            EnablePreview = EnablePreview,
            AdapterFactory = AdapterFactory,
            DiagnosticSink = DiagnosticSink,
            DefaultTicket = DefaultTicket.Clone(),
            DefaultPaginator = DefaultPaginator
        };
}
