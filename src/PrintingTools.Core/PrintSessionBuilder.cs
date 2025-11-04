using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using PrintingTools.Core.Pagination;

namespace PrintingTools.Core;

public sealed class PrintSessionBuilder
{
    private readonly List<Func<IPrintPageEnumerator>> _pageSources = new();
    private readonly PrintOptions _options = new();
    private IPrintPaginator? _paginator;

    public PrintSessionBuilder AddVisual(Visual visual, PrintPageSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (!PrintLayoutHints.GetIsPrintable(visual))
        {
            return this;
        }

        var baseSettings = (settings ?? PrintPageSettings.Default).Clone();
        var pageSettings = PrintLayoutHints.CreateSettings(visual, baseSettings);

        _pageSources.Add(() => new VisualPrintPageEnumerator(visual, pageSettings, PrintLayoutHints.GetIsPageBreakAfter(visual)));
        return this;
    }

    public PrintSessionBuilder AddPageSource(Func<IPrintPageEnumerator> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _pageSources.Add(factory);
        return this;
    }

    public PrintSessionBuilder ConfigureOptions(Action<PrintOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    public PrintSessionBuilder UsePaginator(IPrintPaginator paginator)
    {
        ArgumentNullException.ThrowIfNull(paginator);
        _paginator = paginator;
        return this;
    }

    public PrintSession Build(string? description = null)
    {
        if (_pageSources.Count == 0)
        {
            throw new InvalidOperationException("At least one page source must be added to build a session.");
        }

        var factories = _pageSources.ToArray();
        var document = PrintDocument.FromFactories(factories);
        var session = new PrintSession(document, _options.Clone(), description, paginator: _paginator);
        ApplyOptionsToTicket(session);
        return session;
    }

    private static void ApplyOptionsToTicket(PrintSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var options = session.Options;
        var ticket = session.Ticket.Clone();
        ticket.Orientation = options.Orientation;

        try
        {
            var pageMediaSize = Pagination.PrintPaginationUtilities.CreatePageMediaSize(options);
            ticket.PageMediaSize = pageMediaSize;
        }
        catch (ArgumentException)
        {
            // Ignore invalid sizes and keep the existing page media size.
        }

        ticket.Extensions["layout.kind"] = options.LayoutKind.ToString();

        switch (options.LayoutKind)
        {
            case PrintLayoutKind.NUp:
                ticket.Extensions["layout.nup.rows"] = options.NUpRows.ToString(CultureInfo.InvariantCulture);
                ticket.Extensions["layout.nup.columns"] = options.NUpColumns.ToString(CultureInfo.InvariantCulture);
                ticket.Extensions["layout.nup.order"] = options.NUpOrder.ToString();
                break;

            case PrintLayoutKind.Booklet:
                ticket.Extensions["layout.booklet.bindLongEdge"] = options.BookletBindLongEdge ? "1" : "0";
                ticket.Duplex = options.BookletBindLongEdge
                    ? DuplexingMode.TwoSidedLongEdge
                    : DuplexingMode.TwoSidedShortEdge;
                break;

            case PrintLayoutKind.Poster:
                ticket.Extensions["layout.poster.tileCount"] = options.PosterTileCount.ToString(CultureInfo.InvariantCulture);
                var (rows, columns) = EstimatePosterGrid(options);
                ticket.Extensions["layout.poster.rows"] = rows.ToString(CultureInfo.InvariantCulture);
                ticket.Extensions["layout.poster.columns"] = columns.ToString(CultureInfo.InvariantCulture);
                break;
        }

        session.UpdateTicket(ticket, adoptWarnings: false);
    }

    private static (int rows, int columns) EstimatePosterGrid(PrintOptions options)
    {
        var count = Math.Max(1, options.PosterTileCount);
        var rows = Math.Max(1, (int)Math.Round(Math.Sqrt(count)));
        var columns = (int)Math.Ceiling(count / (double)rows);

        if (options.Orientation == PageOrientation.Landscape && columns < rows)
        {
            (rows, columns) = (columns, rows);
        }
        else if (options.Orientation == PageOrientation.Portrait && rows < columns)
        {
            (rows, columns) = (columns, rows);
        }

        while (rows * columns < count)
        {
            columns++;
        }

        return (rows, columns);
    }
}
