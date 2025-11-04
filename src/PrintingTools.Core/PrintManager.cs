using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PrintingTools.Core;

public interface IPrintManager
{
    Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default);

    Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default);

    Task<PrintSession> RequestSessionAsync(PrintRequest request, CancellationToken cancellationToken = default);

    Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default);

    Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default);
}

public interface IPrintAdapter
{
    Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default);

    Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default);

    Task<PrintSession> CreateSessionAsync(PrintRequest request, CancellationToken cancellationToken = default);

    Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default);

    Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default);
}

public interface IPrintAdapterResolver
{
    IPrintAdapter Resolve();

    IPrintAdapter Resolve(PrintSession session);
}

public sealed class PrintManager : IPrintManager
{
    private readonly IPrintAdapterResolver _resolver;
    private readonly PrintingToolsOptions _options;

    public PrintManager(IPrintAdapterResolver resolver, PrintingToolsOptions options)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        var adapter = _resolver.Resolve();
        return adapter.GetPrintersAsync(cancellationToken);
    }

    public Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default)
    {
        var adapter = _resolver.Resolve();
        return adapter.GetCapabilitiesAsync(printerId, baseTicket, cancellationToken);
    }

    public async Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var adapter = _resolver.Resolve(session);
        session.EnsurePaginator(_options.DefaultPaginator);
        await EnsureSessionPreparedAsync(session, adapter, cancellationToken).ConfigureAwait(false);
        await adapter.PrintAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_options.EnablePreview)
        {
            throw new NotSupportedException("Print preview is disabled by configuration.");
        }

        var adapter = _resolver.Resolve(session);
        session.EnsurePaginator(_options.DefaultPaginator);
        await EnsureSessionPreparedAsync(session, adapter, cancellationToken).ConfigureAwait(false);
        return await adapter.CreatePreviewAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PrintSession> RequestSessionAsync(PrintRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedTicket = (request.Ticket ?? _options.DefaultTicket).Clone();
        var normalizedOptions = request.Options?.Clone() ?? new PrintOptions();

        var normalizedRequest = new PrintRequest(request.Document)
        {
            Description = request.Description,
            Options = normalizedOptions,
            PreferredPrinterId = request.PreferredPrinterId,
            Ticket = normalizedTicket
        };

        var adapter = _resolver.Resolve();
        var session = await adapter.CreateSessionAsync(normalizedRequest, cancellationToken).ConfigureAwait(false);
        var sessionAdapter = _resolver.Resolve(session);

        // Align ticket/options with normalized request in case adapter ignored defaults.
        session.UpdateTicket(normalizedTicket);
        session.Options.PrinterName ??= normalizedOptions.PrinterName;
        session.EnsurePaginator(_options.DefaultPaginator);

        await EnsureSessionPreparedAsync(session, sessionAdapter, cancellationToken, normalizedRequest.PreferredPrinterId).ConfigureAwait(false);

        return session;
    }

    private static PrinterInfo? SelectPrinter(
        IReadOnlyList<PrinterInfo> printers,
        PrinterId? preferredPrinterId,
        string? preferredPrinterName)
    {
        if (printers.Count == 0)
        {
            return null;
        }

        if (preferredPrinterId is { } printerId)
        {
            var match = printers.FirstOrDefault(p => p.Id == printerId);
            if (match is not null)
            {
                return match;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredPrinterName))
        {
            var match = printers.FirstOrDefault(p =>
                string.Equals(p.Name, preferredPrinterName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return printers.FirstOrDefault(p => p.IsDefault) ?? printers[0];
    }

    private async Task EnsureSessionPreparedAsync(
        PrintSession session,
        IPrintAdapter adapter,
        CancellationToken cancellationToken,
        PrinterId? preferredPrinterId = null)
    {
        if (session.Printer is null)
        {
            var printers = await adapter.GetPrintersAsync(cancellationToken).ConfigureAwait(false);
            var printer = SelectPrinter(printers, preferredPrinterId, session.Options.PrinterName);
            if (printer is not null)
            {
                session.AssignPrinter(printer);
            }
        }

        if (session.Printer is not null && session.Capabilities is null)
        {
            var capabilities = await adapter
                .GetCapabilitiesAsync(session.Printer.Id, session.Ticket, cancellationToken)
                .ConfigureAwait(false);
            session.UpdateCapabilities(capabilities);

            var mergedTicket = session.Ticket.MergeWithCapabilities(capabilities);
            session.UpdateTicket(mergedTicket);
        }
    }
}

public sealed class DefaultPrintAdapterResolver : IPrintAdapterResolver
{
    private readonly PrintingToolsOptions _options;
    private readonly IPrintAdapter _fallback = new UnsupportedPrintAdapter();

    public DefaultPrintAdapterResolver(PrintingToolsOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IPrintAdapter Resolve() => ResolveInternal();

    public IPrintAdapter Resolve(PrintSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ResolveInternal();
    }

    private IPrintAdapter ResolveInternal()
    {
        if (_options.AdapterFactory is { } factory)
        {
            var adapter = factory();
            if (adapter is not null)
            {
                return adapter;
            }
        }

        return _fallback;
    }
}

internal sealed class UnsupportedPrintAdapter : IPrintAdapter
{
    private static readonly Task<IReadOnlyList<PrinterInfo>> UnsupportedPrintersTask =
        Task.FromException<IReadOnlyList<PrinterInfo>>(new NotSupportedException("Printer enumeration is not available on this platform."));

    private static readonly Task<PrintCapabilities> UnsupportedCapabilitiesTask =
        Task.FromException<PrintCapabilities>(new NotSupportedException("Print capabilities are not available on this platform."));

    private static readonly Task<PrintSession> UnsupportedSessionTask =
        Task.FromException<PrintSession>(new NotSupportedException("Printing is not available on this platform."));

    private static readonly Task<PrintPreviewModel> UnsupportedPreviewTask =
        Task.FromException<PrintPreviewModel>(new NotSupportedException("Print preview is not available on this platform."));

    public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default) =>
        UnsupportedPrintersTask;

    public Task<PrintCapabilities> GetCapabilitiesAsync(PrinterId printerId, PrintTicketModel? baseTicket = null, CancellationToken cancellationToken = default) =>
        UnsupportedCapabilitiesTask;

    public Task<PrintSession> CreateSessionAsync(PrintRequest request, CancellationToken cancellationToken = default) =>
        UnsupportedSessionTask;

    public Task PrintAsync(PrintSession session, CancellationToken cancellationToken = default) =>
        Task.FromException(new NotSupportedException("Printing is not available on this platform."));

    public Task<PrintPreviewModel> CreatePreviewAsync(PrintSession session, CancellationToken cancellationToken = default) =>
        UnsupportedPreviewTask;
}
