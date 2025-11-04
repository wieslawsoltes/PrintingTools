using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using PrintingTools.Core;

namespace PrintingTools.UI.ViewModels;

public sealed class PageSetupViewModel : INotifyPropertyChanged
{
    private Size _selectedPaperSize;
    private PageOrientation _selectedOrientation;
    private Thickness _margins = new(0.5);
    private bool _showHeaderFooter;
    private bool _usePrintableArea = true;
    private bool _centerHorizontally;
    private bool _centerVertically;
    private PrintLayoutKind _selectedLayoutKind = PrintLayoutKind.Standard;
    private int _nUpRows = 1;
    private int _nUpColumns = 2;
    private NUpPageOrder _nUpOrder = NUpPageOrder.LeftToRightTopToBottom;
    private bool _bookletBindLongEdge = true;
    private int _posterTileCount = 4;

    public PageSetupViewModel()
    {
        PaperSizes = new ObservableCollection<Size>
        {
            new Size(8.5, 11),
            new Size(8.5, 14),
            new Size(11, 17),
            new Size(210 / 25.4, 297 / 25.4),
            new Size(148 / 25.4, 210 / 25.4)
        };
        _selectedPaperSize = PaperSizes[0];
        _selectedOrientation = PageOrientation.Portrait;
        LayoutKinds = Enum.GetValues<PrintLayoutKind>();
        NUpOrders = Enum.GetValues<NUpPageOrder>();

        ApplyCommand = new RelayCommand(_ => Apply());
        CancelCommand = new RelayCommand(_ => Cancel());
    }

    public ObservableCollection<Size> PaperSizes { get; }

    public IReadOnlyList<PrintLayoutKind> LayoutKinds { get; }

    public IReadOnlyList<NUpPageOrder> NUpOrders { get; }

    public Size SelectedPaperSize
    {
        get => _selectedPaperSize;
        set
        {
            if (SetProperty(ref _selectedPaperSize, value))
            {
                OnPropertyChanged(nameof(PreviewPageSize));
            }
        }
    }

    public PageOrientation SelectedOrientation
    {
        get => _selectedOrientation;
        set
        {
            if (SetProperty(ref _selectedOrientation, value))
            {
                OnPropertyChanged(nameof(PreviewPageSize));
            }
        }
    }

    public Thickness Margins
    {
        get => _margins;
        set
        {
            if (SetProperty(ref _margins, value))
            {
                OnPropertyChanged(nameof(PreviewMargins));
            }
        }
    }

    public bool ShowHeaderFooter
    {
        get => _showHeaderFooter;
        set => SetProperty(ref _showHeaderFooter, value);
    }

    public bool UsePrintableArea
    {
        get => _usePrintableArea;
        set => SetProperty(ref _usePrintableArea, value);
    }

    public bool CenterHorizontally
    {
        get => _centerHorizontally;
        set => SetProperty(ref _centerHorizontally, value);
    }

    public bool CenterVertically
    {
        get => _centerVertically;
        set => SetProperty(ref _centerVertically, value);
    }

    public PrintLayoutKind SelectedLayoutKind
    {
        get => _selectedLayoutKind;
        set
        {
            if (SetProperty(ref _selectedLayoutKind, value))
            {
                OnPropertyChanged(nameof(IsNUpSelected));
                OnPropertyChanged(nameof(IsBookletSelected));
                OnPropertyChanged(nameof(IsPosterSelected));
            }
        }
    }

    public bool IsNUpSelected => SelectedLayoutKind == PrintLayoutKind.NUp;

    public bool IsBookletSelected => SelectedLayoutKind == PrintLayoutKind.Booklet;

    public bool IsPosterSelected => SelectedLayoutKind == PrintLayoutKind.Poster;

    public int NUpRows
    {
        get => _nUpRows;
        set => SetProperty(ref _nUpRows, Math.Clamp(value, 1, 16));
    }

    public int NUpColumns
    {
        get => _nUpColumns;
        set => SetProperty(ref _nUpColumns, Math.Clamp(value, 1, 16));
    }

    public NUpPageOrder NUpOrder
    {
        get => _nUpOrder;
        set => SetProperty(ref _nUpOrder, value);
    }

    public bool BookletBindLongEdge
    {
        get => _bookletBindLongEdge;
        set => SetProperty(ref _bookletBindLongEdge, value);
    }

    public int PosterTileCount
    {
        get => _posterTileCount;
        set => SetProperty(ref _posterTileCount, Math.Clamp(value, 1, 64));
    }

    public Size PreviewPageSize =>
        SelectedOrientation == PageOrientation.Landscape
            ? new Size(SelectedPaperSize.Height, SelectedPaperSize.Width)
            : SelectedPaperSize;

    public Thickness PreviewMargins =>
        new(Margins.Left * 96, Margins.Top * 96, Margins.Right * 96, Margins.Bottom * 96);

    public double PreviewScale => 3.0;

    public ICommand ApplyCommand { get; }

    public ICommand CancelCommand { get; }

    public event EventHandler? RequestClose;

    public bool WasApplied { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Apply()
    {
        WasApplied = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel()
    {
        WasApplied = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public PrintOptions ApplyTo(PrintOptions options)
    {
        var updated = options.Clone();
        updated.Orientation = SelectedOrientation;
        updated.Margins = Margins;
        updated.UsePrintableArea = UsePrintableArea;
        updated.CenterHorizontally = CenterHorizontally;
        updated.CenterVertically = CenterVertically;
        updated.PaperSize = SelectedPaperSize;
        updated.LayoutKind = SelectedLayoutKind;
        updated.NUpRows = NUpRows;
        updated.NUpColumns = NUpColumns;
        updated.NUpOrder = NUpOrder;
        updated.BookletBindLongEdge = BookletBindLongEdge;
        updated.PosterTileCount = PosterTileCount;
        return updated;
    }

    public void LoadFrom(PrintOptions options)
    {
        SelectedOrientation = options.Orientation;
        Margins = options.Margins;
        UsePrintableArea = options.UsePrintableArea;
        CenterHorizontally = options.CenterHorizontally;
        CenterVertically = options.CenterVertically;
        SelectedPaperSize = options.PaperSize;
        SelectedLayoutKind = options.LayoutKind;
        NUpRows = options.NUpRows;
        NUpColumns = options.NUpColumns;
        NUpOrder = options.NUpOrder;
        BookletBindLongEdge = options.BookletBindLongEdge;
        PosterTileCount = options.PosterTileCount;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
