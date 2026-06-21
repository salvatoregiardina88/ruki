using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;

namespace Ruki.Infrastructure.Storage;

/// <summary>
/// Implementazione di <see cref="ISecretStore"/> basata su Windows DPAPI
/// (<see cref="ProtectedData"/>).
/// <para>
/// Ogni segreto viene cifrato con la chiave dell'utente Windows corrente
/// (<see cref="DataProtectionScope.CurrentUser"/>) e salvato in un file dedicato
/// dentro <see cref="RukiPaths.SecretsDirectory"/>. Solo lo stesso utente sullo stesso
/// profilo può decifrarlo: copiare il file altrove lo rende inutilizzabile.
/// </para>
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    // "Entropia" addizionale non segreta, mescolata alla cifratura DPAPI: lega i nostri
    // segreti a questa applicazione. Non è una password, solo un discriminante.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Ruki.SecretStore.v1");

    // Le operazioni su file non sono thread-safe: serializziamo gli accessi.
    private readonly object _gate = new();
    private readonly ILogger<DpapiSecretStore> _logger;
    private readonly string _secretsDirectory;

    /// <param name="secretsDirectory">
    /// Cartella dei segreti. In produzione si lascia <c>null</c> (usa il percorso
    /// predefinito); nei test si passa una cartella temporanea per non toccare i dati reali.
    /// </param>
    public DpapiSecretStore(ILogger<DpapiSecretStore> logger, string? secretsDirectory = null)
    {
        _logger = logger;
        _secretsDirectory = secretsDirectory ?? RukiPaths.SecretsDirectory;
        Directory.CreateDirectory(_secretsDirectory);
    }

    public void Set(string key, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(secret);

        lock (_gate)
        {
            var plain = Encoding.UTF8.GetBytes(secret);
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            AtomicFile.WriteAllBytes(GetFilePath(key), encrypted);
            _logger.LogInformation("Segreto '{Key}' salvato.", key);
        }
    }

    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            var path = GetFilePath(key);
            if (!File.Exists(path))
                return null;

            try
            {
                var encrypted = File.ReadAllBytes(path);
                var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException ex)
            {
                // Tipico se il file è stato copiato da un altro utente/PC: non è recuperabile.
                _logger.LogWarning(ex, "Impossibile decifrare il segreto '{Key}'. Verrà trattato come assente.", key);
                return null;
            }
        }
    }

    public bool Has(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return File.Exists(GetFilePath(key));
    }

    public void Delete(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_gate)
        {
            var path = GetFilePath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Segreto '{Key}' eliminato.", key);
            }
        }
    }

    /// <summary>
    /// Mappa una chiave logica sul percorso del file che la contiene, ripulendo
    /// eventuali caratteri non validi per un nome di file (le nostre chiavi sono
    /// costanti note, ma la sanificazione protegge da usi imprevisti).
    /// </summary>
    private string GetFilePath(string key)
    {
        var safe = string.Concat(key.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        return Path.Combine(_secretsDirectory, safe + ".dat");
    }
}
