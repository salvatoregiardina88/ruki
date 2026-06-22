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

        // Alla chiusura, stacca le iscrizioni del ViewModel (es. all'esito delle azioni).
        Closed += (_, _) => viewModel.Detach();
    }

    /// <summary>Trascina la finestra afferrando l'intestazione.</summary>
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Invio = invia il messaggio; Shift+Invio = a capo (comportamento di default del TextBox).</summary>
    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            if (DataContext is ChatViewModel viewModel && viewModel.SendCommand.CanExecute(null))
                viewModel.SendCommand.Execute(null);
            e.Handled = true;   // evita che l'Invio inserisca un a capo
        }
    }
}
