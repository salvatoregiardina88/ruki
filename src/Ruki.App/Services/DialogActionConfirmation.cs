using System.Windows;
using Ruki.App.Localization;
using Ruki.Core.Agents;

namespace Ruki.App.Services;

/// <summary>
/// Implementazione UI di <see cref="IActionConfirmation"/>: mostra un dialogo modale Sì/No sul
/// thread della UI, descrivendo l'azione rischiosa. Usata quando "Chiedi conferma prima di azioni
/// rischiose" è attivo. L'Action Agent gira su un thread in background, quindi marshalliamo la
/// richiesta sul Dispatcher e attendiamo la risposta.
/// </summary>
public sealed class DialogActionConfirmation : IActionConfirmation
{
    public Task<bool> ConfirmAsync(string actionDescription, CancellationToken cancellationToken = default)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return Task.FromResult(true);   // nessuna UI (es. in test): non blocchiamo l'esecuzione

        return dispatcher.InvokeAsync(() =>
        {
            var result = MessageBox.Show(
                Loc.T("Confirm_Body", actionDescription),
                Loc.T("Confirm_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }).Task;
    }
}
