using PrintingTools.Core;
using Xunit;

namespace PrintingTools.Tests;

public class LayoutMetadataTests
{
    [Fact]
    public void FromTicket_DefaultsToStandard()
    {
        var ticket = new PrintTicketModel();

        var metadata = LayoutMetadata.FromTicket(ticket);

        Assert.Equal(PrintLayoutKind.Standard, metadata.Kind);
        Assert.Equal(1, metadata.NUpRows);
        Assert.Equal(1, metadata.NUpColumns);
        Assert.True(metadata.BookletBindLongEdge);
        Assert.Equal(1, metadata.PosterRows);
        Assert.Equal(1, metadata.PosterColumns);
    }

    [Fact]
    public void FromTicket_ParsesNUp()
    {
        var ticket = new PrintTicketModel();
        ticket.Extensions["layout.kind"] = PrintLayoutKind.NUp.ToString();
        ticket.Extensions["layout.nup.rows"] = "2";
        ticket.Extensions["layout.nup.columns"] = "3";
        ticket.Extensions["layout.nup.order"] = NUpPageOrder.TopToBottomLeftToRight.ToString();

        var metadata = LayoutMetadata.FromTicket(ticket);

        Assert.Equal(PrintLayoutKind.NUp, metadata.Kind);
        Assert.Equal(2, metadata.NUpRows);
        Assert.Equal(3, metadata.NUpColumns);
        Assert.Equal(NUpPageOrder.TopToBottomLeftToRight, metadata.NUpOrder);
        Assert.Equal(6, metadata.NUpTileCount);
    }

    [Fact]
    public void FromTicket_ParsesBooklet()
    {
        var ticket = new PrintTicketModel();
        ticket.Extensions["layout.kind"] = PrintLayoutKind.Booklet.ToString();
        ticket.Extensions["layout.booklet.bindLongEdge"] = "0";

        var metadata = LayoutMetadata.FromTicket(ticket);

        Assert.Equal(PrintLayoutKind.Booklet, metadata.Kind);
        Assert.False(metadata.BookletBindLongEdge);
    }

    [Fact]
    public void FromTicket_ParsesPoster()
    {
        var ticket = new PrintTicketModel();
        ticket.Extensions["layout.kind"] = PrintLayoutKind.Poster.ToString();
        ticket.Extensions["layout.poster.rows"] = "3";
        ticket.Extensions["layout.poster.columns"] = "2";

        var metadata = LayoutMetadata.FromTicket(ticket);

        Assert.Equal(PrintLayoutKind.Poster, metadata.Kind);
        Assert.Equal(3, metadata.PosterRows);
        Assert.Equal(2, metadata.PosterColumns);
        Assert.Equal(6, metadata.PosterTileCount);
    }
}
