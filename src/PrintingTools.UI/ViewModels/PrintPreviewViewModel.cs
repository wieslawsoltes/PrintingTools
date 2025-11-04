using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using PrintingTools.Core;

namespace PrintingTools.UI.ViewModels;

public sealed class PrintPreviewViewModel : INotifyPropertyChanged
{
    public PrintPreviewViewModel()
    {
        Pages = new ObservableCollection<PreviewPageViewModel>();
        Pages.CollectionChanged += (_, __) => OnPropertyChanged(nameof(PageCount));
        Printers = new ObservableCollection<PrinterInfo>();
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

    public ObservableCollection<PrinterInfo> Printers { get; }

    private PrinterInfo? _selectedPrinter;
    public PrinterInfo? SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    public string? SelectedPrinterDisplayName => SelectedPrinter?.Name;

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

    public void LoadPrinters(
        IEnumerable<PrinterInfo> printers,
        PrinterId? preferredPrinterId = null,
        string? preferredPrinterName = null)
    {
        if (printers is null)
        {
            throw new ArgumentNullException(nameof(printers));
        }

        Printers.CollectionChanged -= OnPrintersChanged;
        try
        {
            var previousSelection = SelectedPrinter?.Id;

            Printers.Clear();
            foreach (var printer in printers)
            {
                Printers.Add(printer);
            }

            PrinterInfo? selection = null;

            if (preferredPrinterId is { } preferredId)
            {
                selection = Printers.FirstOrDefault(p => p.Id == preferredId);
            }

            if (selection is null && previousSelection is { } previousId)
            {
                selection = Printers.FirstOrDefault(p => p.Id == previousId);
            }

            if (selection is null && !string.IsNullOrWhiteSpace(preferredPrinterName))
            {
                selection = Printers.FirstOrDefault(p =>
                    string.Equals(p.Name, preferredPrinterName, StringComparison.OrdinalIgnoreCase));
            }

            SelectedPrinter = selection ?? Printers.FirstOrDefault();
        }
        finally
        {
            Printers.CollectionChanged += OnPrintersChanged;
            OnPropertyChanged(nameof(HasPrinters));
            OnPropertyChanged(nameof(SelectedPrinterDisplayName));
        }
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
        if (propertyName == nameof(SelectedPrinter))
        {
            OnPropertyChanged(nameof(SelectedPrinterDisplayName));
        }
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void OnPrintersChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasPrinters));
}
