using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using PrintingTools.Core.Pagination;

namespace PrintingTools.Core;

/// <summary>
/// Represents a printing job created from Avalonia visuals or custom page sources.
/// </summary>
public sealed class PrintSession
{
    private IPrintPaginator _paginator;
    private bool _paginatorExplicit;

    public PrintSession(
        PrintDocument document,
        PrintOptions? options = null,
        string? description = null,
        PrinterInfo? printer = null,
        PrintTicketModel? ticket = null,
        PrintCapabilities? capabilities = null,
        IPrintPaginator? paginator = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Options = (options ?? new PrintOptions()).Clone();
        Description = description;
        Printer = printer;
        Ticket = (ticket ?? PrintTicketModel.CreateDefault()).Clone();
        Capabilities = capabilities;
        if (paginator is not null)
        {
            _paginator = paginator;
            _paginatorExplicit = true;
        }
        else
        {
            _paginator = Pagination.DefaultPrintPaginator.Instance;
            _paginatorExplicit = false;
        }
    }

    /// <summary>
    /// Gets the document payload to be printed or previewed.
    /// </summary>
    public PrintDocument Document { get; }

    /// <summary>
    /// Gets the print options associated with this session.
    /// </summary>
    public PrintOptions Options { get; }

    /// <summary>
    /// Gets an optional job description passed through to the native print UI.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the printer selected for this session, if any.
    /// </summary>
    public PrinterInfo? Printer { get; private set; }

    /// <summary>
    /// Gets the ticket negotiated for this session.
    /// </summary>
    public PrintTicketModel Ticket { get; private set; }

    /// <summary>
    /// Gets the capabilities associated with the selected printer, if known.
    /// </summary>
    public PrintCapabilities? Capabilities { get; private set; }

    public IPrintPaginator Paginator => _paginator;

    public bool HasExplicitPaginator => _paginatorExplicit;

    public event EventHandler<PrintJobEventArgs>? JobStatusChanged;

    internal void AssignPrinter(PrinterInfo printer)
    {
        Printer = printer ?? throw new ArgumentNullException(nameof(printer));
    }

    internal void UpdateCapabilities(PrintCapabilities capabilities)
    {
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    internal void UpdateTicket(PrintTicketModel ticket, bool adoptWarnings = true)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        Ticket = ticket.Clone();
        if (adoptWarnings)
        {
            Ticket.AdoptWarningsFrom(ticket);
        }
    }

    internal void SetPaginator(IPrintPaginator paginator, bool markExplicit = true)
    {
        _paginator = paginator ?? throw new ArgumentNullException(nameof(paginator));
        if (markExplicit)
        {
            _paginatorExplicit = true;
        }
    }

    internal void EnsurePaginator(IPrintPaginator fallback)
    {
        if (!_paginatorExplicit && fallback is not null)
        {
            _paginator = fallback;
        }
    }

    internal void NotifyJobEvent(PrintJobEventKind kind, string? message = null, Exception? exception = null)
    {
        JobStatusChanged?.Invoke(this, new PrintJobEventArgs(kind, message, exception));
    }

    public IReadOnlyList<PrintPage> Paginate(CancellationToken cancellationToken = default)
    {
        var pages = _paginator.Paginate(Document, Options, cancellationToken);
        return Pagination.PrintPaginationUtilities.ApplyPageRange(pages, Options.PageRange);
    }
}

/// <summary>
/// Holds information required to generate printable pages from Avalonia content.
/// </summary>
public sealed class PrintDocument
{
    private readonly IReadOnlyList<Func<IPrintPageEnumerator>> _enumeratorFactories;

    private PrintDocument(IReadOnlyList<Func<IPrintPageEnumerator>> enumeratorFactories)
    {
        if (enumeratorFactories.Count == 0)
        {
            throw new ArgumentException("At least one page source must be supplied.", nameof(enumeratorFactories));
        }

        _enumeratorFactories = new List<Func<IPrintPageEnumerator>>(enumeratorFactories);
    }

    internal static PrintDocument FromFactories(IReadOnlyList<Func<IPrintPageEnumerator>> factories) =>
        new(factories);

    public static PrintDocument FromVisual(Visual visual, PrintPageSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (!PrintLayoutHints.GetIsPrintable(visual))
        {
            throw new InvalidOperationException("The visual is marked as non-printable via PrintLayoutHints.");
        }

        var baseSettings = (settings ?? PrintPageSettings.Default).Clone();
        var effectiveSettings = PrintLayoutHints.CreateSettings(visual, baseSettings);
        var list = new List<Func<IPrintPageEnumerator>>
        {
            () => new VisualPrintPageEnumerator(visual, effectiveSettings, PrintLayoutHints.GetIsPageBreakAfter(visual))
        };

        return new PrintDocument(list);
    }

    public IPrintPageEnumerator CreateEnumerator()
    {
        var enumerators = new List<IPrintPageEnumerator>(_enumeratorFactories.Count);
        foreach (var factory in _enumeratorFactories)
        {
            enumerators.Add(factory());
        }

        return PagedCompositeEnumerator.Create(enumerators);
    }

    private sealed class PagedCompositeEnumerator : IPrintPageEnumerator
    {
        private readonly Queue<IPrintPageEnumerator> _enumerators;
        private readonly Queue<PrintPage> _pendingPages = new();
        private IPrintPageEnumerator? _current;
        private PrintPage? _currentPage;

        private PagedCompositeEnumerator(IEnumerable<IPrintPageEnumerator> enumerators)
        {
            _enumerators = new Queue<IPrintPageEnumerator>(enumerators);
        }

        public static IPrintPageEnumerator Create(IEnumerable<IPrintPageEnumerator> enumerators) =>
            new PagedCompositeEnumerator(enumerators);

        public PrintPage Current =>
            _currentPage ?? throw new InvalidOperationException("Enumeration has not started yet.");

        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_pendingPages.Count > 0)
            {
                _currentPage = _pendingPages.Dequeue();
                return true;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_current is null && !TryAdvanceEnumerator())
                {
                    _currentPage = null;
                    return false;
                }

                if (_current is null)
                {
                    continue;
                }

                if (!_current.MoveNext(cancellationToken))
                {
                    _current.Dispose();
                    _current = null;
                    continue;
                }

                var page = _current.Current;
                _currentPage = page;

                if (page.IsPageBreakAfter)
                {
                    break;
                }

                _pendingPages.Enqueue(page);

                if (_pendingPages.Count > 0)
                {
                    _currentPage = _pendingPages.Dequeue();
                    return true;
                }
            }

            return _currentPage is not null;
        }

        public void Dispose()
        {
            _current?.Dispose();

            while (_enumerators.Count > 0)
            {
                _enumerators.Dequeue().Dispose();
            }

            _pendingPages.Clear();
            _currentPage = null;
        }

        private bool TryAdvanceEnumerator()
        {
            if (_enumerators.Count == 0)
            {
                return false;
            }

            _current = _enumerators.Dequeue();
            return true;
        }
    }
}

public interface IPrintPageEnumerator : IDisposable
{
    PrintPage Current { get; }

    bool MoveNext(CancellationToken cancellationToken = default);
}

public sealed class PrintPage
{
    public PrintPage(Visual visual, PrintPageSettings settings, bool isPageBreakAfter, PrintPageMetrics? metrics = null)
    {
        Visual = visual ?? throw new ArgumentNullException(nameof(visual));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        IsPageBreakAfter = isPageBreakAfter;
        Metrics = metrics;
    }

    public Visual Visual { get; }

    public PrintPageSettings Settings { get; }

    public bool IsPageBreakAfter { get; }

    public PrintPageMetrics? Metrics { get; }
}

public sealed class PrintPageSettings
{
    public static PrintPageSettings Default => new();

    public Size? TargetSize { get; init; }

    public Thickness? Margins { get; init; }

    public double Scale { get; init; } = 1d;

    public Rect? SelectionBounds { get; init; }

    public PrintPageSettings Clone() =>
        new()
        {
            TargetSize = TargetSize,
            Margins = Margins,
            Scale = Scale,
            SelectionBounds = SelectionBounds
        };
}

internal sealed class VisualPrintPageEnumerator : IPrintPageEnumerator
{
    private readonly Visual _visual;
    private readonly PrintPageSettings _settings;
    private readonly bool _pageBreakAfter;
    private bool _hasReturnedPage;
    private PrintPage? _currentPage;

    public VisualPrintPageEnumerator(Visual visual, PrintPageSettings settings, bool pageBreakAfter)
    {
        _visual = visual;
        _settings = settings;
        _pageBreakAfter = pageBreakAfter;
    }

    public PrintPage Current =>
        _currentPage ?? throw new InvalidOperationException("Enumeration has not started yet.");

    public void Dispose()
    {
        // No resources to release.
        _currentPage = null;
    }

    public bool MoveNext(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_hasReturnedPage)
        {
            _currentPage = null;
            return false;
        }

        _hasReturnedPage = true;
        _currentPage = CreatePage();
        return true;
    }

    private PrintPage CreatePage()
    {
        var metrics = PrintPageMetrics.Create(_visual, _settings);
        return new PrintPage(_visual, _settings, _pageBreakAfter, metrics);
    }
}

public enum PrintJobEventKind
{
    Unknown = 0,
    Started = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public sealed class PrintJobEventArgs : EventArgs
{
    public PrintJobEventArgs(PrintJobEventKind kind, string? message, Exception? exception)
    {
        Kind = kind;
        Message = message;
        Exception = exception;
    }

    public PrintJobEventKind Kind { get; }

    public string? Message { get; }

    public Exception? Exception { get; }
}
