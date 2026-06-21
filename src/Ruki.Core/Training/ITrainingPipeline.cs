namespace Ruki.Core.Training;

/// <summary>
/// Pipeline di apprendimento da una sessione registrata: prepara i dati, fa analizzare il video al
/// Training Agent e fa salvare il risultato dal Memory Agent.
/// </summary>
public interface ITrainingPipeline
{
    /// <summary>
    /// Processa la sessione e restituisce il NUMERO di memorie create. I dettagli (titoli/percorsi)
    /// restano solo nei log: all'utente mostriamo un'informazione generica.
    /// </summary>
    Task<int> ProcessSessionAsync(TrainingSessionInfo session, CancellationToken cancellationToken = default);
}
