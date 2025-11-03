using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PrintingTools.UI.ViewModels;

namespace AvaloniaSample;

public partial class PrintPreviewWindow : Window
{
    private ListBox? _pagesList;
    private PrintPreviewViewModel? _viewModel;

    public PrintPreviewWindow()
    {
        InitializeComponent();
    }

    public PrintPreviewWindow(PrintPreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _pagesList = this.FindControl<ListBox>("PagesList");
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        }

        _viewModel = DataContext as PrintPreviewViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
            ScrollToSelectedPage();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            _viewModel = null;
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrintPreviewViewModel.SelectedPage))
        {
            ScrollToSelectedPage();
        }
    }

    private void ScrollToSelectedPage()
    {
        if (_viewModel?.SelectedPage is { } page && _pagesList is { })
        {
            _pagesList.ScrollIntoView(page);
        }
    }

    private void OnPreviousPageClicked(object? sender, RoutedEventArgs e) =>
        _viewModel?.GoToPreviousPage();

    private void OnNextPageClicked(object? sender, RoutedEventArgs e) =>
        _viewModel?.GoToNextPage();

    private void OnPrintClicked(object? sender, RoutedEventArgs e)
    {
        _viewModel?.RequestAction(PreviewAction.Print);
        Close();
    }

    private void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        _viewModel?.RequestAction(PreviewAction.ExportPdf);
        Close();
    }

    private void OnRefreshPrintersClicked(object? sender, RoutedEventArgs e) =>
        _viewModel?.RequestAction(PreviewAction.RefreshPrinters);

    private void OnVectorPreviewClicked(object? sender, RoutedEventArgs e)
    {
        _viewModel?.RequestAction(PreviewAction.ViewVectorDocument);
    }
}
