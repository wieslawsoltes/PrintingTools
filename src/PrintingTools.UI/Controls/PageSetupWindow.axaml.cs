using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PrintingTools.UI.ViewModels;

namespace PrintingTools.UI.Controls;

/// <summary>
/// Hosts the <see cref="PageSetupDialog"/> inside a window with modal semantics.
/// </summary>
public partial class PageSetupWindow : Window
{
    public PageSetupWindow()
        : this(new PageSetupViewModel())
    {
    }

    public PageSetupWindow(PageSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= OnRequestClose;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnRequestClose(object? sender, EventArgs e)
    {
        var applied = false;
        if (sender is PageSetupViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            applied = vm.WasApplied;
        }

        Close(applied);
    }
}
