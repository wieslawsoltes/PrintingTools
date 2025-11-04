using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Windows.Input;
using Avalonia.Input;
using PrintingTools.Core;
using PrintingTools.Core.Preview;
using PrintingTools.MacOS;
using PrintingTools.MacOS.Preview;
using PrintingTools.UI.Controls;
using PrintingTools.UI.ViewModels;

namespace AvaloniaSample;

public partial class MainWindow : Window
{
    private readonly IPrintManager _printManager;
    private readonly IPrintAdapterResolver _adapterResolver;
    private readonly Action<PrintDiagnosticEvent> _diagnosticSink;
    private readonly List<PrinterInfo> _printers = new();
    private PrintPreviewModel? _currentPreview;
    private bool _isRendering;
    private bool _printingSupported;
    private PrinterInfo? _selectedPrinter;
    private PrintOptions _pageSetupOptions = new();
    private PrintSession? _activeSession;

    public ObservableCollection<JobHistoryEntry> JobHistory { get; } = new();

    public ICommand ClearHistoryCommand { get; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _printManager = PrintServiceRegistry.EnsureManager();
        _adapterResolver = PrintServiceRegistry.EnsureResolver();

        _diagnosticSink = OnDiagnostic;
        PrintDiagnostics.RegisterSink(_diagnosticSink);

        ClearHistoryCommand = new DelegateCommand(() => JobHistory.Clear());

        StatusText.Text = "Initializing printing services…";
        SetCommandButtonsEnabled(false);

        Dispatcher.UIThread.Post(async () => await InitializeAsync());
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_activeSession is not null)
        {
            _activeSession.JobStatusChanged -= OnSessionJobStatusChanged;
            _activeSession = null;
        }

        PrintDiagnostics.UnregisterSink(_diagnosticSink);
        DisposePreview();
    }

    private async Task InitializeAsync()
    {
        _printingSupported = await RefreshPrintersAsync();
        if (!_printingSupported && string.IsNullOrWhiteSpace(StatusText.Text))
        {
            StatusText.Text = "Printing services are unavailable on this platform.";
        }

        if (!_isRendering)
        {
            SetCommandButtonsEnabled(_printingSupported);
        }
    }

    private async void OnPreviewClicked(object? sender, RoutedEventArgs e)
    {
        if (_isRendering)
        {
            return;
        }

        if (!_printingSupported)
        {
            StatusText.Text = "Printing services are unavailable.";
            return;
        }

        _isRendering = true;
        SetCommandButtonsEnabled(false);
        StatusText.Text = "Generating preview…";

        try
        {
            DisposePreview();

            var session = BuildSampleSession(options =>
            {
                if (_selectedPrinter is not null)
                {
                    options.PrinterName = _selectedPrinter.Name;
                }
            });

            var preview = await _printManager.CreatePreviewAsync(session);
            _currentPreview = preview;

            var viewModel = new PrintPreviewViewModel(preview.Pages, preview.VectorDocument);
            viewModel.ActionRequested += OnPreviewActionRequested;
            viewModel.LoadPrinters(_printers, _selectedPrinter?.Id, _selectedPrinter?.Name);
            if (_selectedPrinter is not null)
            {
                viewModel.SelectedPrinter = _selectedPrinter;
            }

            Control? nativePreviewContent = null;
            MacPreviewHost? macPreviewHost = null;

            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    var adapter = _adapterResolver.Resolve(session);
                    if (adapter is IPrintPreviewProvider previewProvider)
                    {
                        macPreviewHost = new MacPreviewHost(previewProvider);
                        macPreviewHost.LoadPreview(preview);
                        macPreviewHost.Initialize(session);
                        nativePreviewContent = new MacPreviewNativeControlHost(macPreviewHost);
                    }
                }

                var window = new PrintPreviewWindow(viewModel)
                {
                    NativePreviewContent = nativePreviewContent
                };

                await window.ShowDialog(this);
            }
            finally
            {
                viewModel.ActionRequested -= OnPreviewActionRequested;
                _selectedPrinter = viewModel.SelectedPrinter;
                macPreviewHost?.Dispose();
            }

            StatusText.Text = "Preview closed.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Preview failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            SetCommandButtonsEnabled(_printingSupported);
        }
    }

    private async void OnNativePreviewClicked(object? sender, RoutedEventArgs e)
    {
        if (_isRendering)
        {
            return;
        }

        if (!_printingSupported)
        {
            StatusText.Text = "Printing services are unavailable.";
            return;
        }

        _isRendering = true;
        SetCommandButtonsEnabled(false);
        StatusText.Text = "Opening native print preview…";

        try
        {
            DisposePreview();

            var session = BuildSampleSession();
            session.Options.ShowPrintDialog = true;
            session.Options.UseManagedPdfExporter = true;
            session.Options.PdfOutputPath = null;
            if (_selectedPrinter is not null)
            {
                session.Options.PrinterName = _selectedPrinter.Name;
            }

            await _printManager.PrintAsync(session);

            StatusText.Text = "Native preview closed.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Native preview failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            SetCommandButtonsEnabled(_printingSupported);
        }
    }

    private async void OnExportPdfClicked(object? sender, RoutedEventArgs e) =>
        await ExecutePdfExportAsync(_selectedPrinter);

    private async void OnPageSetupClicked(object? sender, RoutedEventArgs e)
    {
        var viewModel = new PageSetupViewModel();
        viewModel.LoadFrom(_pageSetupOptions);
        var dialog = new PageSetupWindow(viewModel);
        var applied = await dialog.ShowDialog<bool>(this);
        if (applied)
        {
            _pageSetupOptions = viewModel.ApplyTo(_pageSetupOptions);
            StatusText.Text = "Page setup updated.";
        }
    }

    private PrintSession BuildSampleSession(Action<PrintOptions>? configureOptions = null)
    {
        var builder = new PrintSessionBuilder();

        for (var page = 1; page <= 4; page++)
        {
            var visual = CreateSamplePage(page);
            builder.AddVisual(visual);
        }

        builder.ConfigureOptions(options =>
        {
            options.ShowPrintDialog = false;
            options.CollectPreviewFirst = true;
            options.UseVectorRenderer = true;
            options.Orientation = _pageSetupOptions.Orientation;
            options.Margins = _pageSetupOptions.Margins;
            options.UsePrintableArea = _pageSetupOptions.UsePrintableArea;
            options.CenterHorizontally = _pageSetupOptions.CenterHorizontally;
            options.CenterVertically = _pageSetupOptions.CenterVertically;
            options.PaperSize = _pageSetupOptions.PaperSize;
            options.LayoutKind = _pageSetupOptions.LayoutKind;
            options.NUpRows = _pageSetupOptions.NUpRows;
            options.NUpColumns = _pageSetupOptions.NUpColumns;
            options.NUpOrder = _pageSetupOptions.NUpOrder;
            options.BookletBindLongEdge = _pageSetupOptions.BookletBindLongEdge;
            options.PosterTileCount = _pageSetupOptions.PosterTileCount;
            configureOptions?.Invoke(options);
        });

        var session = builder.Build("Quarterly Report");
        AttachSession(session);
        return session;
    }

    private Control CreateSamplePage(int pageNumber)
    {
        const double PageWidth = 816;
        const double PageHeight = 1056;
        const double PageMargin = 48;
        var contentWidth = PageWidth - (PageMargin * 2);
        var contentHeight = PageHeight - (PageMargin * 2);

        var accent = pageNumber % 2 == 0 ? Colors.SteelBlue : Colors.DarkOrange;
        var accentBrush = new SolidColorBrush(accent);

        var content = new Border
        {
            Width = contentWidth,
            Height = contentHeight,
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            Tag = $"SamplePage-{pageNumber}",
            Child = new StackPanel
            {
                Spacing = 24,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Quarterly Report – Page {pageNumber}",
                        FontSize = 30,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = accentBrush
                    },
                    new TextBlock
                    {
                        Text = "This preview demonstrates the macOS printing pipeline rendering Avalonia visuals " +
                               "into Quartz contexts. The layout mirrors a simple report page with typography, " +
                               "summary metrics, and highlights.",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 16
                    },
                    CreateMetricsGrid(accentBrush),
                    CreateHighlightsSection(pageNumber)
                }
            }
        };

        PrintLayoutHints.SetTargetPageSize(content, new Size(PageWidth, PageHeight));
        PrintLayoutHints.SetMargins(content, new Thickness(PageMargin));
        PrintLayoutHints.SetScale(content, 1d);

        PrepareVisual(content);
        return content;
    }

    private static Control CreateMetricsGrid(SolidColorBrush accentBrush)
    {
        var labels = new[] { "Revenue", "Expenses", "Delta" };
        var values = new[] { "$128K", "$86K", "$42K" };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 6
        };

        for (var i = 0; i < labels.Length; i++)
        {
            var label = new TextBlock
            {
                Text = labels[i],
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = accentBrush
            };

            Grid.SetColumn(label, i);
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var value = new TextBlock
            {
                Text = values[i],
                FontSize = 22,
                FontWeight = FontWeight.Bold
            };

            Grid.SetColumn(value, i);
            Grid.SetRow(value, 1);
            grid.Children.Add(value);
        }

        var accentColor = accentBrush.Color;

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, accentColor.R, accentColor.G, accentColor.B)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16),
            Child = grid
        };
    }

    private static Control CreateHighlightsSection(int pageNumber)
    {
        var highlights = new List<string>
        {
            "Feature parity milestones advanced for the macOS backend.",
            "Quartz rendering validated against Avalonia visuals.",
            $"Preview pipeline now returns raster snapshots (page {pageNumber}).",
            "Next step: translate PrintOptions into NSPrintInfo settings."
        };

        var stack = new StackPanel
        {
            Spacing = 6
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Highlights",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        });

        foreach (var highlight in highlights)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"• {highlight}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15
            });
        }

        return stack;
    }

    private static void PrepareVisual(Control control)
    {
        if (control is not Layoutable layoutable)
        {
            return;
        }

        var width = !double.IsNaN(control.Width) && control.Width > 0 ? control.Width : 816;
        var height = !double.IsNaN(control.Height) && control.Height > 0 ? control.Height : 1056;
        var size = new Size(width, height);

        layoutable.Measure(size);
        layoutable.Arrange(new Rect(size));
    }

    private void DisposePreview()
    {
        if (_currentPreview is null)
        {
            return;
        }

        _currentPreview.Dispose();
        _currentPreview = null;
    }

    private static string GetPdfDestination()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var fallback = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var baseDirectory = string.IsNullOrWhiteSpace(desktop) ? fallback : desktop;
        var fileName = $"AvaloniaSample-Print-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
        return Path.Combine(baseDirectory, fileName);
    }

    private void OnDiagnostic(PrintDiagnosticEvent diagnostic)
    {
        var message = $"[{diagnostic.Timestamp:O}] {diagnostic.Category}: {diagnostic.Message}";
        Console.WriteLine(message);

        if (diagnostic.Exception is { } exception)
        {
            Console.WriteLine(exception);
        }
    }

    private void AttachSession(PrintSession session)
    {
        if (_activeSession == session)
        {
            return;
        }

        if (_activeSession is not null)
        {
            _activeSession.JobStatusChanged -= OnSessionJobStatusChanged;
        }

        _activeSession = session;
        _activeSession.JobStatusChanged += OnSessionJobStatusChanged;
        AppendJobHistory(PrintJobEventKind.Unknown, $"Session '{session.Description ?? "Print Job"}' created.");
    }

    private void OnSessionJobStatusChanged(object? sender, PrintJobEventArgs e)
    {
        var message = string.IsNullOrWhiteSpace(e.Message) ? e.Kind.ToString() : e.Message!;
        Dispatcher.UIThread.Post(() =>
        {
            AppendJobHistory(e.Kind, message);
            if (e.Exception is { } ex)
            {
                AppendJobHistory(e.Kind, ex.Message);
            }
        });
    }

    private void AppendJobHistory(PrintJobEventKind kind, string message)
    {
        JobHistory.Add(new JobHistoryEntry(DateTimeOffset.Now, kind, message));
        while (JobHistory.Count > 200)
        {
            JobHistory.RemoveAt(0);
        }
    }

    private async Task<bool> RefreshPrintersAsync(PrintPreviewViewModel? viewModel = null)
    {
        try
        {
            var printers = await _printManager.GetPrintersAsync();

            _printers.Clear();
            _printers.AddRange(printers);

            if (_selectedPrinter is not null)
            {
                var updated = _printers.FirstOrDefault(p => p.Id == _selectedPrinter.Id);
                if (updated is not null)
                {
                    _selectedPrinter = updated;
                }
            }

            if (_selectedPrinter is null && _printers.Count > 0)
            {
                _selectedPrinter = _printers.FirstOrDefault(p => p.IsDefault) ?? _printers[0];
            }

            viewModel?.LoadPrinters(_printers, _selectedPrinter?.Id, _selectedPrinter?.Name);

            StatusText.Text = _printers.Count switch
            {
                0 => "No printers detected.",
                1 => "Detected 1 printer.",
                _ => $"Detected {_printers.Count} printers."
            };

            _printingSupported = true;
            if (!_isRendering)
            {
                SetCommandButtonsEnabled(true);
            }

            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException)
        {
            _printingSupported = false;
            StatusText.Text = "Printer enumeration is not supported on this platform.";
            SetCommandButtonsEnabled(false);
            return false;
        }
        catch (Exception ex)
        {
            _printingSupported = false;
            StatusText.Text = $"Printer refresh failed: {ex.Message}";
            SetCommandButtonsEnabled(false);
            return false;
        }
    }

    private async void OnPreviewActionRequested(object? sender, PreviewActionEventArgs e)
    {
        if (sender is not PrintPreviewViewModel viewModel)
        {
            return;
        }

        switch (e.Action)
        {
            case PreviewAction.Print:
                _selectedPrinter = viewModel.SelectedPrinter;
                await ExecutePhysicalPrintAsync(_selectedPrinter);
                break;
            case PreviewAction.ExportPdf:
                _selectedPrinter = viewModel.SelectedPrinter;
                await ExecutePdfExportAsync(_selectedPrinter);
                break;
            case PreviewAction.RefreshPrinters:
                await RefreshPrintersAsync(viewModel);
                if (_selectedPrinter is not null)
                {
                    viewModel.SelectedPrinter = _selectedPrinter;
                }
                break;
            case PreviewAction.ViewVectorDocument:
                if (viewModel.VectorDocument is { Length: > 0 } vectorDocument)
                {
                    LaunchVectorPreview(vectorDocument);
                }
                else
                {
                    StatusText.Text = "Vector document unavailable.";
                }
                break;
        }
    }

    private void LaunchVectorPreview(byte[] pdfBytes)
    {
        if (!OperatingSystem.IsMacOS())
        {
            StatusText.Text = "Vector preview is only available on macOS.";
            return;
        }

        var result = MacPrintUtilities.ShowVectorPreview(pdfBytes);
        StatusText.Text = result ? "Vector preview launched." : "Vector preview failed.";
    }

    private async Task ExecutePhysicalPrintAsync(PrinterInfo? printer)
    {
        if (_isRendering)
        {
            StatusText.Text = "Another print job is already running.";
            return;
        }

        if (!_printingSupported)
        {
            StatusText.Text = "Printing services are unavailable.";
            return;
        }

        _isRendering = true;
        SetCommandButtonsEnabled(false);
        StatusText.Text = "Sending print job…";

        try
        {
            DisposePreview();

            var session = BuildSampleSession(options =>
            {
                options.ShowPrintDialog = true;
                options.CollectPreviewFirst = false;
                options.UseManagedPdfExporter = false;
                options.PdfOutputPath = null;
                if (printer is not null)
                {
                    options.PrinterName = printer.Name;
                }
            });

            await _printManager.PrintAsync(session);
            StatusText.Text = "Print job submitted.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Print failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            SetCommandButtonsEnabled(_printingSupported);
        }
    }

    private async Task ExecutePdfExportAsync(PrinterInfo? printer)
    {
        if (_isRendering)
        {
            StatusText.Text = "Another operation is already running.";
            return;
        }

        if (!_printingSupported)
        {
            StatusText.Text = "Printing services are unavailable.";
            return;
        }

        _isRendering = true;
        SetCommandButtonsEnabled(false);
        StatusText.Text = "Exporting PDF…";

        try
        {
            DisposePreview();

            var pdfPath = GetPdfDestination();
            var session = BuildSampleSession(options =>
            {
                options.ShowPrintDialog = false;
                options.CollectPreviewFirst = false;
                options.UseManagedPdfExporter = true;
                options.PdfOutputPath = pdfPath;
                if (printer is not null)
                {
                    options.PrinterName = printer.Name;
                }
            });

            await _printManager.PrintAsync(session);
            StatusText.Text = $"PDF exported to {pdfPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"PDF export failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            SetCommandButtonsEnabled(_printingSupported);
        }
    }

    private void SetCommandButtonsEnabled(bool isEnabled)
    {
        PreviewButton.IsEnabled = isEnabled;
        NativePreviewButton.IsEnabled = isEnabled;
        ExportPdfButton.IsEnabled = isEnabled;
    }
}
