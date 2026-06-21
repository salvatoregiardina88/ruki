using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using Ruki.Core.Capture;

namespace Ruki.App.Services;

/// <summary>
/// Rileva se il campo con il focus è un campo password tramite UI Automation (l'accessibilità di
/// Windows). Copre i campi nativi, WPF/WinUI e i principali browser. Le API UIA possono bloccare se
/// invocate dal thread UI, quindi la registrazione del gestore di focus avviene su un thread in
/// background; il gestore si limita ad aggiornare un flag (letto dal callback della tastiera).
/// </summary>
public sealed class UiaPasswordFieldDetector : IPasswordFieldDetector
{
    private readonly ILogger<UiaPasswordFieldDetector> _logger;
    private volatile bool _isPassword;
    private AutomationFocusChangedEventHandler? _handler;

    public UiaPasswordFieldDetector(ILogger<UiaPasswordFieldDetector> logger) => _logger = logger;

    public bool IsPasswordFieldFocused => _isPassword;

    public void Start()
    {
        if (_handler is not null)
            return;

        _handler = OnFocusChanged;
        var handler = _handler;
        Task.Run(() =>
        {
            try
            {
                Automation.AddAutomationFocusChangedEventHandler(handler);
                SetFrom(AutomationElement.FocusedElement);   // controllo iniziale dell'elemento già a fuoco
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Avvio del rilevamento campi password (UIA) non riuscito.");
            }
        });
    }

    public void Stop()
    {
        var handler = _handler;
        _handler = null;
        _isPassword = false;
        if (handler is null)
            return;

        Task.Run(() =>
        {
            try { Automation.RemoveAutomationFocusChangedEventHandler(handler); }
            catch (Exception ex) { _logger.LogWarning(ex, "Arresto del rilevamento campi password (UIA) non riuscito."); }
        });
    }

    private void OnFocusChanged(object? sender, AutomationFocusChangedEventArgs e)
        => SetFrom(sender as AutomationElement);

    private void SetFrom(AutomationElement? element)
    {
        if (element is null)
        {
            _isPassword = false;
            return;
        }

        try
        {
            _isPassword = element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) is true;
        }
        catch
        {
            // Elemento sparito o non accessibile: manteniamo lo stato precedente; il prossimo
            // cambio di focus lo correggerà.
        }
    }
}
