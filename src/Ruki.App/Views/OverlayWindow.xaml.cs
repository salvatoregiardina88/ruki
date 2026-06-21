using System.Windows;
using System.Windows.Input;
using Ruki.App.ViewModels;

namespace Ruki.App.Views;

/// <summary>
/// Finestra overlay di Ruki. La logica dei pulsanti vive nell'<see cref="OverlayViewModel"/>;
/// qui restano solo i comportamenti puramente visivi: posizionamento iniziale e trascinamento.
/// </summary>
public partial class OverlayWindow : Window
{
    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    /// <summary>Posiziona l'overlay in basso a destra dell'area di lavoro, all'apertura.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = area.Bottom - ActualHeight - 16;
    }

    /// <summary>Permette di trascinare la finestra afferrando la maniglia.</summary>
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
