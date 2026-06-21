using System.Windows;
using System.Windows.Input;
using Ruki.App.ViewModels;

namespace Ruki.App.Views;

/// <summary>
/// Finestra della chat (overlay). Il code-behind collega il ViewModel, fa scorrere l'elenco
/// fino all'ultimo messaggio e gestisce i comportamenti visivi dell'overlay: trascinamento
/// tramite l'intestazione e chiusura.
/// </summary>
public partial class ChatWindow : Window
{
    public ChatWindow(ChatViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Auto-scroll: quando la collezione cambia, portiamo la vista in fondo.
        viewModel.Messages.CollectionChanged += (_, _) => MessagesScroll.ScrollToEnd();
    }

    /// <summary>Trascina la finestra afferrando l'intestazione.</summary>
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
