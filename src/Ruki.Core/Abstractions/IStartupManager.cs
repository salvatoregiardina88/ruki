namespace Ruki.Core.Abstractions;

/// <summary>
/// Gestisce l'avvio automatico di Ruki all'accesso a Windows (per-utente, senza diritti di
/// amministratore). Lo stato "vero" vive nel sistema (registro), non nelle impostazioni di Ruki.
/// </summary>
public interface IStartupManager
{
    /// <summary>True se l'avvio automatico con Windows è attivo.</summary>
    bool IsEnabled();

    /// <summary>Attiva o disattiva l'avvio automatico di Ruki all'accesso a Windows.</summary>
    void SetEnabled(bool enabled);
}
