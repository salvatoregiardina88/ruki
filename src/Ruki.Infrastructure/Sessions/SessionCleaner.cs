using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Training;
using Ruki.Infrastructure.Storage;

namespace Ruki.Infrastructure.Sessions;

/// <summary>
/// Implementazione di <see cref="ISessionCleaner"/>: conserva solo le N sessioni più recenti
/// (N = <c>MaxStoredSessions</c> nelle impostazioni) ed elimina le più vecchie.
/// </summary>
public sealed class SessionCleaner : ISessionCleaner
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SessionCleaner> _logger;

    public SessionCleaner(ISettingsService settings, ILogger<SessionCleaner> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void CleanupOldSessions()
    {
        var keep = _settings.Current.MaxStoredSessions;
        var sessionsRoot = RukiPaths.SessionsDirectory;
        if (!Directory.Exists(sessionsRoot))
            return;

        // I nomi delle cartelle sono timestamp "yyyyMMdd_HHmmss": l'ordine alfabetico decrescente
        // equivale all'ordine dal più recente al più vecchio.
        var sessions = new DirectoryInfo(sessionsRoot)
            .GetDirectories()
            .OrderByDescending(directory => directory.Name)
            .ToList();

        foreach (var old in sessions.Skip(keep))
        {
            try
            {
                old.Delete(recursive: true);
                _logger.LogInformation("Sessione vecchia eliminata: {Name}.", old.Name);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Eliminazione della sessione {Name} fallita.", old.Name);
            }
        }
    }
}
