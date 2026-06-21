using System.Diagnostics;
using System.Windows;
using Ruki.App.ViewModels;

namespace Ruki.App.Views;

/// <summary>
/// Finestra delle impostazioni. La logica sta nel <see cref="SettingsViewModel"/>;
/// il code-behind serve solo a fare da ponte per la <c>PasswordBox</c>, che per motivi
/// di sicurezza non espone la propria proprietà al data binding.
/// </summary>
public partial class SettingsWindow : Window
{
    // Video tutorial su come ottenere la chiave API Gemini.
    private const string ApiKeyHelpVideoUrl = "https://youtube.com/watch?v=AZDl5ggz2Jc";

    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Passiamo manualmente il contenuto della PasswordBox al ViewModel, poi salviamo.
        _viewModel.ApiKey = ApiKeyBox.Password;
        _viewModel.SaveCommand.Execute(null);
        ApiKeyBox.Clear();
    }

    /// <summary>Apre nel browser il video tutorial su come ottenere la chiave API.</summary>
    private void OnApiKeyHelpClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ApiKeyHelpVideoUrl) { UseShellExecute = true });
        }
        catch
        {
            // Link d'aiuto: se l'apertura fallisce non è un problema bloccante.
        }
    }

    /// <summary>Mostra l'informativa sulla privacy.</summary>
    private void OnPrivacyClick(object sender, RoutedEventArgs e)
        => new PrivacyWindow { Owner = this }.ShowDialog();
}
