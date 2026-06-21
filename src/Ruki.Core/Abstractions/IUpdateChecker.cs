namespace Ruki.Core.Abstractions;

/// <summary>Informazioni su un aggiornamento disponibile.</summary>
/// <param name="Version">Versione della nuova release (es. "0.2.0").</param>
/// <param name="Url">Pagina da aprire per scaricare/leggere l'aggiornamento.</param>
public sealed record UpdateInfo(string Version, string Url);

/// <summary>
/// Verifica in modo silenzioso se esiste una versione più recente di Ruki.
/// <para>
/// Pensato per l'avvio: non deve mai disturbare l'utente in caso di problemi (rete assente,
/// servizio irraggiungibile, nessun aggiornamento) — in tutti questi casi restituisce <c>null</c>.
/// </para>
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Restituisce le info sull'aggiornamento se ne esiste uno più recente di quello installato,
    /// altrimenti <c>null</c>. Non lancia: gli errori vengono assorbiti (e loggati).
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
