using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Ruki.Core.Abstractions;

namespace Ruki.Infrastructure.Startup;

/// <summary>
/// Avvio automatico tramite la chiave di registro PER-UTENTE
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>: non richiede diritti di amministratore
/// ed è la stessa voce usata (opzionalmente) dall'installer, così i due restano coerenti.
/// </summary>
public sealed class RegistryStartupManager : IStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Stesso nome valore usato dall'installer (vedi build/ruki.iss).
    private const string ValueName = "Ruki";

    private readonly ILogger<RegistryStartupManager> _logger;

    public RegistryStartupManager(ILogger<RegistryStartupManager> logger) => _logger = logger;

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lettura dello stato di avvio automatico fallita.");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
                return;

            if (enabled)
            {
                // Percorso dell'eseguibile corrente, tra virgolette (può contenere spazi).
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                    return;
                key.SetValue(ValueName, $"\"{exe}\"");
                _logger.LogInformation("Avvio automatico con Windows attivato.");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger.LogInformation("Avvio automatico con Windows disattivato.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impostazione dell'avvio automatico fallita.");
        }
    }
}
