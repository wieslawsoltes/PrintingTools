using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PrintingTools.UI.ViewModels;

namespace PrintingTools.UI.Controls;

/// <summary>
/// Provides a reusable Avalonia window that hosts the managed print preview UI.
/// </summary>
public partial class PrintPreviewWindow : Window
{
    private ListBox? _pagesList;
    private PrintPreviewViewModel? _viewModel;
    private ContentControl? _nativePreviewHostContainer;
    private StackPanel? _zoomPanel;
    private ToggleSwitch? _nativePreviewToggle;
    private Control? _nativePreviewContent;

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
        _nativePreviewHostContainer = this.FindControl<ContentControl>("NativePreviewHostContainer");
        _zoomPanel = this.FindControl<StackPanel>("ZoomPanel");
        _nativePreviewToggle = this.FindControl<ToggleSwitch>("NativePreviewToggle");
        if (_nativePreviewToggle is not null)
        {
            _nativePreviewToggle.IsCheckedChanged += NativePreviewToggleOnIsCheckedChanged;
        }
    }

    public Control? NativePreviewContent
    {
        get => _nativePreviewContent;
        set
        {
            _nativePreviewContent = value;
            if (_nativePreviewHostContainer is not null)
            {
                _nativePreviewHostContainer.Content = value;
                _nativePreviewHostContainer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                _nativePreviewHostContainer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                _nativePreviewHostContainer.Width = double.NaN;
                _nativePreviewHostContainer.Height = double.NaN;
            }

            if (value is Control control)
            {
                control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                control.Width = double.NaN;
                control.Height = double.NaN;
            }

            if (_nativePreviewToggle is not null)
            {
                _nativePreviewToggle.IsVisible = value is not null;
                if (value is null)
                {
                    _nativePreviewToggle.IsChecked = false;
                }
            }

            ApplyNativePreviewVisibility(_nativePreviewToggle?.IsChecked ?? false);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyNativePreviewVisibility(_nativePreviewToggle?.IsChecked ?? false);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            _viewModel = null;
        }

        if (_nativePreviewToggle is not null)
        {
            _nativePreviewToggle.IsCheckedChanged -= NativePreviewToggleOnIsCheckedChanged;
        }

        if (_nativePreviewHostContainer is not null)
        {
            _nativePreviewHostContainer.Content = null;
            _nativePreviewHostContainer.IsVisible = false;
        }

        _nativePreviewContent = null;
        _pagesList?.SetValue(IsVisibleProperty, true);
        _zoomPanel?.SetValue(IsVisibleProperty, true);
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

    private void OnVectorPreviewClicked(object? sender, RoutedEventArgs e) =>
        _viewModel?.RequestAction(PreviewAction.ViewVectorDocument);

    private void NativePreviewToggleOnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            ApplyNativePreviewVisibility(toggle.IsChecked ?? false);
        }
    }

    private void ApplyNativePreviewVisibility(bool useNativePreview)
    {
        var hasNativePreview = _nativePreviewContent is not null;
        if (!hasNativePreview || _nativePreviewHostContainer is null)
        {
            _nativePreviewHostContainer?.SetValue(IsVisibleProperty, false);
            _pagesList?.SetValue(IsVisibleProperty, true);
            _zoomPanel?.SetValue(IsVisibleProperty, true);
            return;
        }

        if (useNativePreview)
        {
            _nativePreviewHostContainer.IsVisible = true;
            _pagesList?.SetValue(IsVisibleProperty, false);
            _zoomPanel?.SetValue(IsVisibleProperty, false);
            if (_nativePreviewContent is NativeControlHost nativeHost)
            {
                nativeHost.TryUpdateNativeControlPosition();
            }
        }
        else
        {
            _nativePreviewHostContainer.IsVisible = false;
            _pagesList?.SetValue(IsVisibleProperty, true);
            _zoomPanel?.SetValue(IsVisibleProperty, true);
        }
    }
}
