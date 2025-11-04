using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Gtk;
using GtkPageOrientation = Gtk.PageOrientation;
using GtkPrintDuplex = Gtk.PrintDuplex;
using GtkPrintPages = Gtk.PrintPages;
using PrintingTools.Core;

namespace PrintingTools.Linux.Dialogs;

/// <summary>
/// Wraps the GTK print dialog so the Linux adapter can present native UI when GTK is available.
/// </summary>
internal sealed class GtkPrintDialogBridge
{
    private const string DiagnosticsCategory = "GtkPrintDialog";

    private static readonly object GtkInitLock = new();
    private static bool _gtkInitialized;
    private static bool _gtkAvailable;

    public bool IsSupported
    {
        get
        {
            EnsureInitialized();
            return _gtkAvailable;
        }
    }

    private static void EnsureInitialized()
    {
        lock (GtkInitLock)
        {
            if (_gtkInitialized)
            {
                return;
            }

            try
            {
                var args = Array.Empty<string>();
                if (!Gtk.Application.InitCheck("PrintingTools", ref args))
                {
                    PrintDiagnostics.Report(DiagnosticsCategory, "GTK initialization failed (InitCheck returned false).");
                    _gtkAvailable = false;
                }
                else
                {
                    _gtkAvailable = true;
                    PrintDiagnostics.Report(DiagnosticsCategory, "GTK runtime detected. Using native print dialog.");
                }
            }
            catch (DllNotFoundException ex)
            {
                PrintDiagnostics.Report(DiagnosticsCategory, "GTK libraries not found on the current system.", ex);
                _gtkAvailable = false;
            }
            catch (Exception ex)
            {
                PrintDiagnostics.Report(DiagnosticsCategory, "Unexpected failure while initializing GTK.", ex);
                _gtkAvailable = false;
            }
            finally
            {
                _gtkInitialized = true;
            }
        }
    }

    public GtkPrintDialogResult ShowDialog(PrintSession session)
    {
        if (!IsSupported)
        {
            return GtkPrintDialogResult.Unavailable;
        }

        try
        {
            using var dialog = new PrintUnixDialog(session.Description ?? "Print", null)
            {
                SupportSelection = true,
                HasSelection = session.Options.SelectionOnlyRequested
            };

            ApplyDefaults(dialog, session);

            var response = (ResponseType)dialog.Run();

            if (response == ResponseType.Ok)
            {
                ApplySettings(dialog, session);
                PumpPendingEvents();
                return GtkPrintDialogResult.Accepted;
            }

            PumpPendingEvents();
            return GtkPrintDialogResult.Cancelled;
        }
        catch (Exception ex)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "GTK print dialog failed.", ex);
            return GtkPrintDialogResult.Error(ex);
        }
    }

    private static void ApplyDefaults(PrintUnixDialog dialog, PrintSession session)
    {
        var settings = dialog.Settings ?? new PrintSettings();

        if (session.Printer is { } printer)
        {
            settings.Printer = printer.Name;
        }

        if (session.Options.PageRange is { } range)
        {
            var gtkRange = new PageRange
            {
                Start = Math.Max(range.StartPage - 1, 0),
                End = Math.Max(range.EndPage - 1, range.StartPage - 1)
            };
            settings.PrintPages = GtkPrintPages.Ranges;
            settings.SetPageRanges(gtkRange, 1);
        }

        dialog.Settings = settings;
    }

    private static void ApplySettings(PrintUnixDialog dialog, PrintSession session)
    {
        var printer = dialog.SelectedPrinter;
        if (printer is not null)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(printer.Location))
            {
                attributes["Location"] = printer.Location;
            }

            var printerInfo = new PrinterInfo(
                new PrinterId(printer.Name),
                printer.Name,
                isDefault: printer.IsDefault,
                isOnline: printer.IsActive,
                isLocal: !printer.IsVirtual,
                attributes);

            session.AssignPrinter(printerInfo);
            session.Options.PrinterName = printerInfo.Name;
        }

        var settings = dialog.Settings ?? new PrintSettings();
        var ticket = session.Ticket.Clone();

        var copies = Math.Clamp(settings.NCopies, 1, 999);
        ticket.Copies = copies;

        ticket.Duplex = settings.Duplex switch
        {
            GtkPrintDuplex.Simplex => DuplexingMode.OneSided,
            GtkPrintDuplex.Horizontal => DuplexingMode.TwoSidedLongEdge,
            GtkPrintDuplex.Vertical => DuplexingMode.TwoSidedShortEdge,
            _ => ticket.Duplex
        };

        ticket.ColorMode = settings.UseColor
            ? ColorMode.Color
            : ColorMode.Monochrome;

        var pageSetup = dialog.PageSetup ?? new PageSetup();
        if (pageSetup.PaperSize is { } paperSize)
        {
            var widthPoints = paperSize.GetWidth(Unit.Points);
            var heightPoints = paperSize.GetHeight(Unit.Points);
            var pageName = !string.IsNullOrWhiteSpace(paperSize.DisplayName)
                ? paperSize.DisplayName
                : paperSize.Name;

            ticket.PageMediaSize = new PageMediaSize(pageName, widthPoints, heightPoints);
            ticket.Extensions["cups.media"] = paperSize.Name;
        }

        ticket.Orientation = pageSetup.Orientation switch
        {
            GtkPageOrientation.Landscape => Core.PageOrientation.Landscape,
            _ => Core.PageOrientation.Portrait
        };

        session.UpdateTicket(ticket, adoptWarnings: false);

        session.Options.SelectionOnlyRequested = settings.PrintPages == GtkPrintPages.Selection;

        if (settings.PrintPages == GtkPrintPages.Ranges)
        {
            var gtkRanges = settings.GetPageRanges(out var rangeCount);
            if (rangeCount > 0)
            {
                var firstRange = gtkRanges;
                session.Options.PageRange = new PrintPageRange(firstRange.Start + 1, firstRange.End + 1);
            }
        }
        else
        {
            session.Options.PageRange = null;
        }

        session.Options.Orientation = ticket.Orientation;

        var left = pageSetup.GetLeftMargin(Unit.Inch);
        var top = pageSetup.GetTopMargin(Unit.Inch);
        var right = pageSetup.GetRightMargin(Unit.Inch);
        var bottom = pageSetup.GetBottomMargin(Unit.Inch);
        session.Options.Margins = new Thickness(left, top, right, bottom);
    }

    private static void PumpPendingEvents()
    {
        while (Gtk.Application.EventsPending())
        {
            Gtk.Application.RunIteration();
        }
    }
}

internal readonly record struct GtkPrintDialogResult(GtkPrintDialogStatus Status, Exception? Exception = null)
{
    public static readonly GtkPrintDialogResult Accepted = new(GtkPrintDialogStatus.Accepted);
    public static readonly GtkPrintDialogResult Cancelled = new(GtkPrintDialogStatus.Cancelled);
    public static readonly GtkPrintDialogResult Unavailable = new(GtkPrintDialogStatus.Unavailable);

    public static GtkPrintDialogResult Error(Exception exception) => new(GtkPrintDialogStatus.Error, exception);
}

internal enum GtkPrintDialogStatus
{
    Accepted,
    Cancelled,
    Unavailable,
    Error
}
