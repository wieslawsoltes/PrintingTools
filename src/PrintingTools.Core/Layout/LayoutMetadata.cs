using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrintingTools.Core;

/// <summary>
/// Represents normalized layout metadata parsed from <see cref="PrintTicketModel"/> extensions.
/// </summary>
public readonly struct LayoutMetadata
{
    private LayoutMetadata(
        PrintLayoutKind kind,
        int nUpRows,
        int nUpColumns,
        NUpPageOrder nUpOrder,
        bool bookletBindLongEdge,
        int posterRows,
        int posterColumns)
    {
        Kind = kind;
        NUpRows = Math.Max(1, nUpRows);
        NUpColumns = Math.Max(1, nUpColumns);
        NUpOrder = nUpOrder;
        BookletBindLongEdge = bookletBindLongEdge;
        PosterRows = Math.Max(1, posterRows);
        PosterColumns = Math.Max(1, posterColumns);
    }

    public PrintLayoutKind Kind { get; }

    public int NUpRows { get; }

    public int NUpColumns { get; }

    public NUpPageOrder NUpOrder { get; }

    public bool BookletBindLongEdge { get; }

    public int PosterRows { get; }

    public int PosterColumns { get; }

    public int NUpTileCount => Math.Max(1, NUpRows * NUpColumns);

    public int PosterTileCount => Math.Max(1, PosterRows * PosterColumns);

    public bool HasExplicitLayout => Kind != PrintLayoutKind.Standard;

    public static LayoutMetadata FromTicket(PrintTicketModel ticket)
    {
        if (ticket is null)
        {
            throw new ArgumentNullException(nameof(ticket));
        }

        var extensions = ticket.Extensions;

        var kind = PrintLayoutKind.Standard;
        if (extensions.TryGetValue("layout.kind", out var kindValue) &&
            Enum.TryParse(kindValue, ignoreCase: true, out PrintLayoutKind parsedKind))
        {
            kind = parsedKind;
        }

        var nUpRows = ParseInt(extensions, "layout.nup.rows", defaultValue: 1);
        var nUpColumns = ParseInt(extensions, "layout.nup.columns", defaultValue: 1);
        var nUpOrder = NUpPageOrder.LeftToRightTopToBottom;
        if (extensions.TryGetValue("layout.nup.order", out var orderValue) &&
            Enum.TryParse(orderValue, ignoreCase: true, out NUpPageOrder parsedOrder))
        {
            nUpOrder = parsedOrder;
        }

        var bookletBindLongEdge = ParseBool(extensions, "layout.booklet.bindLongEdge", defaultValue: true);

        var posterRows = ParseInt(extensions, "layout.poster.rows", defaultValue: 1);
        var posterColumns = ParseInt(extensions, "layout.poster.columns", defaultValue: 1);

        return new LayoutMetadata(
            kind,
            nUpRows,
            nUpColumns,
            nUpOrder,
            bookletBindLongEdge,
            posterRows,
            posterColumns);
    }

    private static int ParseInt(IDictionary<string, string> extensions, string key, int defaultValue)
    {
        if (extensions.TryGetValue(key, out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static bool ParseBool(IDictionary<string, string> extensions, string key, bool defaultValue)
    {
        if (!extensions.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}
