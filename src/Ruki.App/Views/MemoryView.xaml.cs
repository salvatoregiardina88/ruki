using System.Windows;
using System.Windows.Controls;
using Ruki.App.ViewModels;

namespace Ruki.App.Views;

/// <summary>
/// Vista del tab Memoria. La <c>TreeView.SelectedItem</c> non è bindabile, quindi inoltriamo
/// il cambio di selezione al ViewModel da qui (unica responsabilità del code-behind).
/// </summary>
public partial class MemoryView : UserControl
{
    public MemoryView() => InitializeComponent();

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MemoryViewModel viewModel)
            viewModel.Select(e.NewValue as MemoryNodeViewModel);
    }
}
