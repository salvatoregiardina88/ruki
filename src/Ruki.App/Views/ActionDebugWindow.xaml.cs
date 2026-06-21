using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ruki.App.ViewModels;

namespace Ruki.App.Views;

/// <summary>
/// Finestra di debug dell'Action Agent: si posiziona sul bordo destro dello schermo e scorre
/// automaticamente sull'ultima voce. Alla chiusura stacca le sottoscrizioni del ViewModel.
/// </summary>
public partial class ActionDebugWindow : Window
{
    private readonly ActionDebugViewModel _viewModel;

    public ActionDebugWindow(ActionDebugViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += (_, _) => _viewModel.Detach();
        _viewModel.Entries.CollectionChanged += (_, _) => EntriesScroll.ScrollToEnd();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Height = area.Height;
        Left = area.Right - Width;
        Top = area.Top;
    }

    private void OnImageClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Image { Source: { } source })
            new ImageViewerWindow(source) { Owner = this }.Show();
    }
}

