using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PrintingTools.Core;

namespace PrintingTools.UI.ViewModels;

public sealed class PrintPreviewViewModel : INotifyPropertyChanged
{
    public PrintPreviewViewModel()
    {
        Pages = new ObservableCollection<PreviewPageViewModel>();
        Pages.CollectionChanged += (_, __) => OnPropertyChanged(nameof(PageCount));
        Printers = new ObservableCollection<string>();
        Printers.CollectionChanged += OnPrintersChanged;
    }

    public PrintPreviewViewModel(IReadOnlyList<PrintPage> pages, byte[]? vectorDocument = null) : this()
    {
        for (var i = 0; i < pages.Count; i++)
        {
            Pages.Add(new PreviewPageViewModel(i + 1, pages[i]));
        }

        if (Pages.Count > 0)
        {
            SelectedPage = Pages[0];
            SelectedPageNumber = 1;
        }

        VectorDocument = vectorDocument;
        OnPropertyChanged(nameof(HasVectorDocument));

        OnPropertyChanged(nameof(PageCount));
    }

    public ObservableCollection<PreviewPageViewModel> Pages { get; }

    public ObservableCollection<string> Printers { get; }

    private string? _selectedPrinter;
    public string? SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    public bool HasPrinters => Printers.Count > 0;

    public byte[]? VectorDocument { get; }

    public bool HasVectorDocument => VectorDocument is { Length: > 0 };

    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, Math.Clamp(value, 0.25, 4.0));
    }

    private PreviewPageViewModel? _selectedPage;
    public PreviewPageViewModel? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetProperty(ref _selectedPage, value) && value is not null)
            {
                var index = Pages.IndexOf(value);
                if (index >= 0)
                {
                    SelectedPageNumber = index + 1;
                }
            }
        }
    }

    private int _selectedPageNumber = 1;
    public int SelectedPageNumber
    {
        get => _selectedPageNumber;
        set
        {
            if (SetProperty(ref _selectedPageNumber, value))
            {
                var index = value - 1;
                if (index >= 0 && index < Pages.Count)
                {
                    SelectedPage = Pages[index];
                }
            }
        }
    }

    public int PageCount => Pages.Count;

    public event EventHandler<PreviewActionEventArgs>? ActionRequested;

    public void GoToNextPage()
    {
        if (SelectedPageNumber < Pages.Count)
        {
            SelectedPageNumber += 1;
        }
    }

    public void GoToPreviousPage()
    {
        if (SelectedPageNumber > 1)
        {
            SelectedPageNumber -= 1;
        }
    }

    public void LoadPrinters(IEnumerable<string> printers)
    {
        Printers.CollectionChanged -= OnPrintersChanged;
        Printers.Clear();
        foreach (var printer in printers)
        {
            Printers.Add(printer);
        }
        Printers.CollectionChanged += OnPrintersChanged;

        if (string.IsNullOrWhiteSpace(SelectedPrinter) && Printers.Count > 0)
        {
            SelectedPrinter = Printers[0];
        }

        OnPropertyChanged(nameof(HasPrinters));
    }

    public void RequestAction(PreviewAction action) =>
        ActionRequested?.Invoke(this, new PreviewActionEventArgs(action));

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void OnPrintersChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasPrinters));
}
