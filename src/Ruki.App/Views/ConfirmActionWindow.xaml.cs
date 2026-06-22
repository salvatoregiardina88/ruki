using System.Windows;

namespace Ruki.App.Views;

/// <summary>
/// Dialogo modale di conferma per un'azione rischiosa: sempre in primo piano. Restituisce l'esito
/// tramite <see cref="Window.DialogResult"/> (true = approvata, false = rifiutata).
/// </summary>
public partial class ConfirmActionWindow : Window
{
    public ConfirmActionWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;

        // Portala davanti a tutto e dà il focus al pulsante "No" (scelta più prudente di default).
        Loaded += (_, _) =>
        {
            Activate();
            NoButton.Focus();
        };
    }

    // Impostare DialogResult chiude automaticamente una finestra aperta con ShowDialog.
    private void Yes_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void No_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
