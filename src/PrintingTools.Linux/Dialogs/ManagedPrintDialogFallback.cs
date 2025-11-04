using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PrintingTools.Core;

namespace PrintingTools.Linux.Dialogs;

/// <summary>
/// Presents a lightweight managed dialog when native print UI is unavailable.
/// </summary>
internal static class ManagedPrintDialogFallback
{
    private const string DiagnosticsCategory = "ManagedPrintDialog";

    public static async Task<ManagedPrintDialogOutcome> TryShowAsync(
        PrintSession session,
        IReadOnlyList<PrinterInfo> printers,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (Application.Current is null)
        {
            PrintDiagnostics.Report(DiagnosticsCategory, "Avalonia application context unavailable; unable to show managed dialog.");
            return ManagedPrintDialogOutcome.Unavailable;
        }

        var operation = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = BuildDialog(session, printers, out var builtState);
            var state = builtState;

            using var registration = cancellationToken.Register(() =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (dialog.IsVisible)
                    {
                        state.DialogResult = false;
                        dialog.Close(false);
                    }
                }));

            bool? result;

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is { } owner)
            {
                result = await dialog.ShowDialog<bool?>(owner);
            }
            else
            {
                var tcs = new TaskCompletionSource<bool?>();
                void ClosedHandler(object? sender, EventArgs args)
                {
                    dialog.Closed -= ClosedHandler;
                    tcs.TrySetResult(state.DialogResult);
                }

                dialog.Closed += ClosedHandler;
                dialog.Show();
                result = await tcs.Task.ConfigureAwait(true);
            }

            return (state.DialogResult ?? result) ?? false;
        });

        var accepted = await operation.ConfigureAwait(false);
        return accepted ? ManagedPrintDialogOutcome.Accepted : ManagedPrintDialogOutcome.Cancelled;
    }

    private static Window BuildDialog(PrintSession session, IReadOnlyList<PrinterInfo> printers, out ManagedDialogState state)
    {
        var window = new Window
        {
            Title = "Print",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        window.Padding = new Thickness(18);

        var root = new StackPanel
        {
            Spacing = 12,
            Orientation = Orientation.Vertical
        };

        var header = new TextBlock
        {
            Text = "Native print dialogs were not found on this system. Use the managed dialog to confirm settings.",
            TextWrapping = TextWrapping.Wrap,
            Width = 360
        };
        root.Children.Add(header);

        ComboBox? printerCombo = null;
        if (printers.Count > 0 || session.Printer is not null)
        {
            printerCombo = new ComboBox
            {
                ItemsSource = printers,
                SelectedItem = SelectPrinter(printers, session),
                DisplayMemberBinding = new Binding("Name"),
                MinWidth = 240
            };

            var printerRow = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
            printerRow.Children.Add(new TextBlock { Text = "Printer", FontWeight = Avalonia.Media.FontWeight.SemiBold });
            printerRow.Children.Add(printerCombo);
            root.Children.Add(printerRow);
        }

        var copiesRow = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        copiesRow.Children.Add(new TextBlock { Text = "Copies", FontWeight = Avalonia.Media.FontWeight.SemiBold });
        var copiesBox = new TextBox
        {
            Text = session.Ticket.Copies.ToString(CultureInfo.InvariantCulture),
            Width = 80
        };
        copiesRow.Children.Add(copiesBox);
        root.Children.Add(copiesRow);

        var pageRangeRow = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        pageRangeRow.Children.Add(new TextBlock { Text = "Page Range (e.g., 1-3)", FontWeight = Avalonia.Media.FontWeight.SemiBold });
        var pageRangeBox = new TextBox
        {
            Text = session.Options.PageRange is { } range
                ? FormattableString.Invariant($"{range.StartPage}-{range.EndPage}")
                : string.Empty,
            Width = 160
        };
        pageRangeRow.Children.Add(pageRangeBox);
        root.Children.Add(pageRangeRow);

        var selectionCheck = new CheckBox
        {
            Content = "Print selection only",
            IsChecked = session.Options.SelectionOnlyRequested
        };
        root.Children.Add(selectionCheck);

        var errorText = new TextBlock
        {
            Foreground = Avalonia.Media.Brushes.Red,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };
        root.Children.Add(errorText);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var cancelButton = new Button { Content = "Cancel" };
        var acceptButton = new Button { Content = "Print", Classes = { "accent" } };

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(acceptButton);
        root.Children.Add(buttonPanel);

        window.Content = root;

        var localState = new ManagedDialogState(session, printerCombo, copiesBox, pageRangeBox, selectionCheck, errorText);
        state = localState;

        cancelButton.Click += (_, _) =>
        {
            localState.DialogResult = false;
            window.Close(false);
        };
        acceptButton.Click += (_, _) =>
        {
            if (!TryCommit(localState))
            {
                return;
            }

            localState.DialogResult = true;
            window.Close(true);
        };

        return window;
    }

    private static PrinterInfo? SelectPrinter(IReadOnlyList<PrinterInfo> printers, PrintSession session)
    {
        PrinterInfo? preferred = null;
        if (session.Options.PrinterName is { } printerName)
        {
            preferred = FindByName(printers, printerName);
        }

        preferred ??= session.Printer is { } printer ? FindByName(printers, printer.Name) : null;
        preferred ??= printers.Count > 0 ? printers[0] : null;
        return preferred;
    }

    private static PrinterInfo? FindByName(IReadOnlyList<PrinterInfo> printers, string name)
    {
        foreach (var printer in printers)
        {
            if (string.Equals(printer.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return printer;
            }
        }

        return null;
    }

    private static bool TryCommit(ManagedDialogState state)
    {
        state.ErrorMessage.IsVisible = false;
        state.ErrorMessage.Text = string.Empty;

        if (!int.TryParse(state.CopiesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var copies) || copies <= 0)
        {
            ShowError(state, "Enter a valid copy count (>= 1).");
            return false;
        }

        PrintPageRange? parsedRange = null;
        var rangeText = state.PageRangeBox.Text?.Trim();
        if (!string.IsNullOrEmpty(rangeText))
        {
            if (!TryParsePageRange(rangeText, out parsedRange))
            {
                ShowError(state, "Enter a valid page range (e.g., 2-4).");
                return false;
            }
        }

        var selectedPrinter = state.PrinterCombo?.SelectedItem as PrinterInfo;

        var ticket = state.Session.Ticket.Clone();
        ticket.Copies = copies;
        state.Session.UpdateTicket(ticket, adoptWarnings: false);

        if (selectedPrinter is not null)
        {
            state.Session.AssignPrinter(selectedPrinter);
            state.Session.Options.PrinterName = selectedPrinter.Name;
        }

        state.Session.Options.SelectionOnlyRequested = state.SelectionOnlyCheck.IsChecked == true;
        state.Session.Options.PageRange = parsedRange;

        return true;
    }

    private static bool TryParsePageRange(string input, out PrintPageRange? range)
    {
        range = null;
        var parts = input.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var single) && single > 0)
            {
                range = new PrintPageRange(single, single);
                return true;
            }

            return false;
        }

        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end) &&
            start > 0 && end >= start)
        {
            range = new PrintPageRange(start, end);
            return true;
        }

        return false;
    }

    private static void ShowError(ManagedDialogState state, string message)
    {
        state.ErrorMessage.Text = message;
        state.ErrorMessage.IsVisible = true;
    }

    private sealed class ManagedDialogState
    {
        public ManagedDialogState(
            PrintSession session,
            ComboBox? printerCombo,
            TextBox copiesBox,
            TextBox pageRangeBox,
            CheckBox selectionOnlyCheck,
            TextBlock errorMessage)
        {
            Session = session;
            PrinterCombo = printerCombo;
            CopiesBox = copiesBox;
            PageRangeBox = pageRangeBox;
            SelectionOnlyCheck = selectionOnlyCheck;
            ErrorMessage = errorMessage;
        }

        public PrintSession Session { get; }
        public ComboBox? PrinterCombo { get; }
        public TextBox CopiesBox { get; }
        public TextBox PageRangeBox { get; }
        public CheckBox SelectionOnlyCheck { get; }
        public TextBlock ErrorMessage { get; }
        public bool? DialogResult { get; set; }
    }
}

internal enum ManagedPrintDialogOutcome
{
    Accepted,
    Cancelled,
    Unavailable
}
