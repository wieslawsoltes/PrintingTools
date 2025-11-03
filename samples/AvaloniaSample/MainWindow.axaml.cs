using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PrintingTools.Core;
using PrintingTools.MacOS;
using PrintingTools.UI.ViewModels;

namespace AvaloniaSample;

public partial class MainWindow : Window
{
    private readonly MacPrintAdapterFactory _adapterFactory = new();
    private readonly Action<PrintDiagnosticEvent> _diagnosticSink;
    private readonly List<string> _printers = new();
    private PrintPreviewModel? _currentPreview;
    private bool _isRendering;
    private string? _selectedPrinter;

    public MainWindow()
    {
        InitializeComponent();
        _diagnosticSink = OnDiagnostic;
        PrintDiagnostics.RegisterSink(_diagnosticSink);

        if (_adapterFactory.IsSupported)
        {
            StatusText.Text = "Ready to render sample pages.";
            RefreshPrinters();
        }
        else
        {
            StatusText.Text = "macOS printing adapter unavailable on this platform.";
            PreviewButton.IsEnabled = false;
            NativePreviewButton.IsEnabled = false;
            ExportPdfButton.IsEnabled = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        PrintDiagnostics.UnregisterSink(_diagnosticSink);
        DisposePreview();
    }

    private async void OnPreviewClicked(object? sender, RoutedEventArgs e)
    {
        if (_isRendering)
        {
            return;
        }

        if (!_adapterFactory.IsSupported)
        {
            StatusText.Text = "macOS adapter is unavailable.";
            return;
        }

        _isRendering = true;
        PreviewButton.IsEnabled = false;
        ExportPdfButton.IsEnabled = false;
        StatusText.Text = "Generating preview…";

        try
        {
            DisposePreview();

            var adapter = _adapterFactory.CreateAdapter();
            if (adapter is null)
            {
                StatusText.Text = "macOS adapter could not be created.";
                return;
            }

            var session = BuildSampleSession(options =>
            {
                if (!string.IsNullOrWhiteSpace(_selectedPrinter))
                {
                    options.PrinterName = _selectedPrinter;
                }
            });
            var preview = await adapter.CreatePreviewAsync(session);
            _currentPreview = preview;

            var viewModel = new PrintPreviewViewModel(preview.Pages, preview.VectorDocument);
            viewModel.ActionRequested += OnPreviewActionRequested;
            viewModel.LoadPrinters(_printers);
            if (!string.IsNullOrWhiteSpace(_selectedPrinter))
            {
                viewModel.SelectedPrinter = _selectedPrinter;
            }

            var window = new PrintPreviewWindow(viewModel);
            await window.ShowDialog(this);
            viewModel.ActionRequested -= OnPreviewActionRequested;
            _selectedPrinter = viewModel.SelectedPrinter;
            StatusText.Text = "Preview closed.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Preview failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            PreviewButton.IsEnabled = _adapterFactory.IsSupported;
            NativePreviewButton.IsEnabled = _adapterFactory.IsSupported;
            ExportPdfButton.IsEnabled = _adapterFactory.IsSupported;
        }
    }

    private async void OnNativePreviewClicked(object? sender, RoutedEventArgs e)
    {
        if (_isRendering)
        {
            return;
        }

        if (!_adapterFactory.IsSupported)
        {
            StatusText.Text = "macOS adapter is unavailable.";
            return;
        }

        _isRendering = true;
        PreviewButton.IsEnabled = false;
        NativePreviewButton.IsEnabled = false;
        ExportPdfButton.IsEnabled = false;
        StatusText.Text = "Opening macOS print preview…";

        try
        {
            DisposePreview();

            var adapter = _adapterFactory.CreateAdapter();
            if (adapter is null)
            {
                StatusText.Text = "macOS adapter could not be created.";
                return;
            }

            var session = BuildSampleSession();
            session.Options.ShowPrintDialog = true;
            session.Options.UseManagedPdfExporter = true;
            session.Options.PdfOutputPath = null;
            if (!string.IsNullOrWhiteSpace(_selectedPrinter))
            {
                session.Options.PrinterName = _selectedPrinter;
            }

            await adapter.PrintAsync(session);

            StatusText.Text = "macOS preview closed.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"macOS preview failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            PreviewButton.IsEnabled = _adapterFactory.IsSupported;
            NativePreviewButton.IsEnabled = _adapterFactory.IsSupported;
            ExportPdfButton.IsEnabled = _adapterFactory.IsSupported;
        }
    }

    private async void OnExportPdfClicked(object? sender, RoutedEventArgs e) =>
        await ExecutePdfExportAsync(_selectedPrinter);

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
            configureOptions?.Invoke(options);
        });

        return builder.Build("Quarterly Report");
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

    private void RefreshPrinters()
    {
        _printers.Clear();

        try
        {
            var printers = MacPrinterCatalog.GetInstalledPrinters();
            _printers.AddRange(printers);
            if (_printers.Count > 0 && string.IsNullOrWhiteSpace(_selectedPrinter))
            {
                _selectedPrinter = _printers[0];
            }

            StatusText.Text = _printers.Count switch
            {
                0 => "No printers detected.",
                1 => $"Detected {_printers.Count} printer.",
                _ => $"Detected {_printers.Count} printers."
            };
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Printer refresh failed: {ex.Message}";
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
                RefreshPrinters();
                viewModel.LoadPrinters(_printers);
                if (!string.IsNullOrWhiteSpace(_selectedPrinter))
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
        if (!_adapterFactory.IsSupported)
        {
            StatusText.Text = "macOS adapter is unavailable.";
            return;
        }

        var result = MacPrintUtilities.ShowVectorPreview(pdfBytes);
        StatusText.Text = result ? "Vector preview launched." : "Vector preview failed.";
    }

    private async Task ExecutePhysicalPrintAsync(string? printerName)
    {
        if (_isRendering)
        {
            StatusText.Text = "Another print job is already running.";
            return;
        }

        if (!_adapterFactory.IsSupported)
        {
            StatusText.Text = "macOS adapter is unavailable.";
            return;
        }

        _isRendering = true;
        PreviewButton.IsEnabled = false;
        NativePreviewButton.IsEnabled = false;
        ExportPdfButton.IsEnabled = false;
        StatusText.Text = "Sending print job…";

        try
        {
            DisposePreview();
            var adapter = _adapterFactory.CreateAdapter();
            if (adapter is null)
            {
                StatusText.Text = "macOS adapter could not be created.";
                return;
            }

            var session = BuildSampleSession(options =>
            {
                options.ShowPrintDialog = true;
                options.CollectPreviewFirst = false;
                options.UseManagedPdfExporter = false;
                options.PdfOutputPath = null;
                if (!string.IsNullOrWhiteSpace(printerName))
                {
                    options.PrinterName = printerName;
                }
            });

            await adapter.PrintAsync(session);
            StatusText.Text = "Print job submitted.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Print failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            PreviewButton.IsEnabled = _adapterFactory.IsSupported;
            NativePreviewButton.IsEnabled = _adapterFactory.IsSupported;
            ExportPdfButton.IsEnabled = _adapterFactory.IsSupported;
        }
    }

    private async Task ExecutePdfExportAsync(string? printerName)
    {
        if (_isRendering)
        {
            StatusText.Text = "Another operation is already running.";
            return;
        }

        if (!_adapterFactory.IsSupported)
        {
            StatusText.Text = "macOS adapter is unavailable.";
            return;
        }

        _isRendering = true;
        PreviewButton.IsEnabled = false;
        NativePreviewButton.IsEnabled = false;
        ExportPdfButton.IsEnabled = false;
        StatusText.Text = "Exporting PDF…";

        try
        {
            DisposePreview();
            var adapter = _adapterFactory.CreateAdapter();
            if (adapter is null)
            {
                StatusText.Text = "macOS adapter could not be created.";
                return;
            }

            var pdfPath = GetPdfDestination();
            var session = BuildSampleSession(options =>
            {
                options.ShowPrintDialog = false;
                options.CollectPreviewFirst = false;
                options.UseManagedPdfExporter = true;
                options.PdfOutputPath = pdfPath;
                if (!string.IsNullOrWhiteSpace(printerName))
                {
                    options.PrinterName = printerName;
                }
            });

            await adapter.PrintAsync(session);
            StatusText.Text = $"PDF exported to {pdfPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"PDF export failed: {ex.Message}";
        }
        finally
        {
            _isRendering = false;
            PreviewButton.IsEnabled = _adapterFactory.IsSupported;
            NativePreviewButton.IsEnabled = _adapterFactory.IsSupported;
            ExportPdfButton.IsEnabled = _adapterFactory.IsSupported;
        }
    }
}
