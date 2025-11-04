using Avalonia.Controls;
using PrintingTools.Core;
using Xunit;

namespace PrintingTools.Tests;

public class PrintSessionBuilderTests
{
    [Fact]
    public void ApplyOptionsToTicket_PopulatesExtensionsAndDuplex()
    {
        var builder = new PrintSessionBuilder();
        builder.AddVisual(new Border { Width = 100, Height = 100 });
        builder.ConfigureOptions(options =>
        {
            options.LayoutKind = PrintLayoutKind.Booklet;
            options.BookletBindLongEdge = false;
        });

        var session = builder.Build("Booklet Test");

        Assert.Equal(DuplexingMode.TwoSidedShortEdge, session.Ticket.Duplex);
        Assert.Equal("Booklet", session.Ticket.Extensions["layout.kind"]);
        Assert.Equal("0", session.Ticket.Extensions["layout.booklet.bindLongEdge"]);
    }

    [Fact]
    public void ApplyOptionsToTicket_NUpMetadata()
    {
        var builder = new PrintSessionBuilder();
        builder.AddVisual(new Border { Width = 100, Height = 100 });
        builder.ConfigureOptions(options =>
        {
            options.LayoutKind = PrintLayoutKind.NUp;
            options.NUpRows = 2;
            options.NUpColumns = 3;
            options.NUpOrder = NUpPageOrder.TopToBottomLeftToRight;
        });

        var session = builder.Build("NUp Test");

        Assert.Equal("NUp", session.Ticket.Extensions["layout.kind"]);
        Assert.Equal("2", session.Ticket.Extensions["layout.nup.rows"]);
        Assert.Equal("3", session.Ticket.Extensions["layout.nup.columns"]);
        Assert.Equal(NUpPageOrder.TopToBottomLeftToRight.ToString(), session.Ticket.Extensions["layout.nup.order"]);
    }
}
