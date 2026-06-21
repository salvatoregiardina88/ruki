using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Configuration;

namespace Ruki.Infrastructure.Storage;

/// <summary>
/// Implementazione di <see cref="ISettingsService"/> che persiste le impostazioni
/// in un file JSON sotto <c>%APPDATA%\Ruki</c>.
/// <para>
/// Robusta per natura: se il file manca o è corrotto, riparte dai valori di default
/// senza lanciare eccezioni, così l'app si avvia comunque. Le scritture sono atomiche
/// (vedi <see cref="AtomicFile"/>) e serializzate con un lock.
/// </para>
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,             // file leggibile/modificabile a mano
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly object _gate = new();
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly string _settingsFile;
    private RukiSettings _current;

    /// <param name="settingsFilePath">
    /// Percorso del file impostazioni. In produzione si lascia <c>null</c> (usa il percorso
    /// predefinito); nei test si passa un file temporaneo per non toccare i dati reali.
    /// </param>
    public JsonSettingsService(ILogger<JsonSettingsService> logger, string? settingsFilePath = null)
    {
        _logger = logger;
        _settingsFile = settingsFilePath ?? RukiPaths.SettingsFile;
        // Carichiamo subito così Current è valido fin dalla costruzione.
        _current = Load();
    }

    public RukiSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<RukiSettings>? Changed;

    public RukiSettings Load()
    {
        lock (_gate)
        {
            var path = _settingsFile;

            if (!File.Exists(path))
            {
                _logger.LogInformation("Nessun file impostazioni: creo i default in {Path}.", path);
                var defaults = new RukiSettings().Normalize();
                Persist(defaults);
                _current = defaults;
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<RukiSettings>(json, JsonOptions)
                             ?? throw new JsonException("Deserializzazione nulla.");
                loaded.Normalize();
                _current = loaded;
                return loaded;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // File corrotto/illeggibile: lo mettiamo da parte e ripartiamo dai default,
                // così non perdiamo del tutto l'eventuale contenuto da ispezionare.
                _logger.LogError(ex, "File impostazioni illeggibile: uso i default e salvo una copia .corrupt.");
                TryBackupCorrupt(path);

                var defaults = new RukiSettings().Normalize();
                Persist(defaults);
                _current = defaults;
                return defaults;
            }
        }
    }

    public void Save(RukiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        RukiSettings snapshot;
        lock (_gate)
        {
            // Lavoriamo su una copia normalizzata: chi ha passato l'oggetto non deve
            // poterlo modificare "da sotto" dopo il salvataggio.
            snapshot = settings.Clone().Normalize();
            Persist(snapshot);
            _current = snapshot;
        }

        _logger.LogInformation("Impostazioni salvate.");
        Changed?.Invoke(this, snapshot);
    }

    /// <summary>Serializza e scrive su disco. Da chiamare sempre sotto lock.</summary>
    private void Persist(RukiSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        AtomicFile.WriteAllText(_settingsFile, json);
    }

    private void TryBackupCorrupt(string path)
    {
        try
        {
            File.Copy(path, path + ".corrupt", overwrite: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Impossibile salvare la copia .corrupt del file impostazioni.");
        }
    }
}
