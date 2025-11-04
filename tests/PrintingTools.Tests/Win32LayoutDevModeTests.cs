using PrintingTools.Core;
using PrintingTools.Windows;
using PrintingTools.Windows.Interop;
using Xunit;

namespace PrintingTools.Tests;

public class Win32LayoutDevModeTests
{
    [Fact]
    public void ApplyLayoutToDevMode_SetsNUpFlags()
    {
        var ticket = new PrintTicketModel();
        ticket.Extensions["layout.kind"] = PrintLayoutKind.NUp.ToString();
        ticket.Extensions["layout.nup.rows"] = "2";
        ticket.Extensions["layout.nup.columns"] = "2";
        var layout = LayoutMetadata.FromTicket(ticket);

        var devMode = default(Win32NativeMethods.DEVMODE);

        var modified = Win32PrintAdapter.ApplyLayoutToDevMode(layout, ref devMode);

        Assert.True(modified);
        Assert.Equal((uint)Win32NativeMethods.DM_NUP, devMode.dmFields & (uint)Win32NativeMethods.DM_NUP);
        Assert.Equal((uint)4, devMode.dmDisplayFlags);
    }

    [Fact]
    public void ApplyLayoutToDevMode_ClearsNUpForStandard()
    {
        var ticket = new PrintTicketModel();
        var layout = LayoutMetadata.FromTicket(ticket);

        var devMode = default(Win32NativeMethods.DEVMODE);
        devMode.dmFields = (uint)Win32NativeMethods.DM_NUP;
        devMode.dmDisplayFlags = 8;

        var modified = Win32PrintAdapter.ApplyLayoutToDevMode(layout, ref devMode);

        Assert.True(modified);
        Assert.Equal(0u, devMode.dmFields & (uint)Win32NativeMethods.DM_NUP);
        Assert.Equal((uint)0, devMode.dmDisplayFlags);
    }

    [Fact]
    public void ApplyLayoutToDevMode_PosterUsesTileCount()
    {
        var ticket = new PrintTicketModel();
        ticket.Extensions["layout.kind"] = PrintLayoutKind.Poster.ToString();
        ticket.Extensions["layout.poster.rows"] = "3";
        ticket.Extensions["layout.poster.columns"] = "2";
        var layout = LayoutMetadata.FromTicket(ticket);

        var devMode = default(Win32NativeMethods.DEVMODE);

        var modified = Win32PrintAdapter.ApplyLayoutToDevMode(layout, ref devMode);

        Assert.True(modified);
        Assert.Equal((uint)Win32NativeMethods.DM_NUP, devMode.dmFields & (uint)Win32NativeMethods.DM_NUP);
        Assert.Equal((uint)6, devMode.dmDisplayFlags);
    }

    [Fact]
    public void ApplyLayoutToDevMode_BookletAdjustsDuplex()
    {
        var ticket = new PrintTicketModel();
        ticket.Extensions["layout.kind"] = PrintLayoutKind.Booklet.ToString();
        ticket.Extensions["layout.booklet.bindLongEdge"] = "0";
        var layout = LayoutMetadata.FromTicket(ticket);

        var devMode = default(Win32NativeMethods.DEVMODE);

        var modified = Win32PrintAdapter.ApplyLayoutToDevMode(layout, ref devMode);

        Assert.True(modified);
        Assert.Equal((uint)Win32NativeMethods.DM_DUPLEX, devMode.dmFields & (uint)Win32NativeMethods.DM_DUPLEX);
        Assert.Equal(Win32NativeMethods.DMDUP_VERTICAL, devMode.dmDuplex);
    }
}
