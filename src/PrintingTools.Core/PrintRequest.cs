using System;

namespace PrintingTools.Core;

public sealed class PrintRequest
{
    public PrintRequest(PrintDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public PrintDocument Document { get; }

    public PrintOptions? Options { get; set; }

    public PrintTicketModel? Ticket { get; set; }

    public PrinterId? PreferredPrinterId { get; set; }

    public string? Description { get; set; }
}
