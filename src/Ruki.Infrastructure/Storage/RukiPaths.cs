namespace Ruki.Infrastructure.Storage;

/// <summary>
/// Punto unico per i percorsi su disco usati da Ruki.
/// <para>
/// Tutto vive sotto <c>%APPDATA%\Ruki</c> (cartella roaming dell'utente Windows),
/// così i dati seguono l'utente e non richiedono permessi di amministratore.
/// </para>
/// </summary>
public static class RukiPaths
{
    /// <summary>Cartella radice dell'app: <c>%APPDATA%\Ruki</c>.</summary>
    public static string BaseDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ruki");

    /// <summary>File JSON con le impostazioni applicative.</summary>
    public static string SettingsFile => Path.Combine(BaseDirectory, "settings.json");

    /// <summary>Cartella che contiene i segreti cifrati (uno per file).</summary>
    public static string SecretsDirectory => Path.Combine(BaseDirectory, "secrets");

    /// <summary>File JSON con il conteggio dei token consumati nel mese corrente.</summary>
    public static string UsageFile => Path.Combine(BaseDirectory, "usage.json");

    /// <summary>Cartella dei log applicativi.</summary>
    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");

    /// <summary>Cartella dei dati (database e sessioni di addestramento).</summary>
    public static string DataDirectory => Path.Combine(BaseDirectory, "data");

    /// <summary>Cartella che raccoglie le registrazioni delle sessioni di addestramento.</summary>
    public static string SessionsDirectory => Path.Combine(DataDirectory, "sessions");

    /// <summary>File del database SQLite (memoria dell'agente).</summary>
    public static string DatabaseFile => Path.Combine(DataDirectory, "ruki.db");

    /// <summary>
    /// Crea tutte le cartelle se mancanti. È idempotente: chiamarla più volte non fa danni.
    /// Va invocata una volta all'avvio, prima di usare gli archivi su disco.
    /// </summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(SecretsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(SessionsDirectory);
    }
}
