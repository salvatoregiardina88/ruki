namespace Ruki.Core.Training;

/// <summary>
/// Mantiene sotto controllo lo spazio occupato dalle sessioni di addestramento, eliminando
/// le più vecchie oltre il numero configurato.
/// </summary>
public interface ISessionCleaner
{
    /// <summary>Elimina le sessioni più vecchie, conservando solo le più recenti (vedi impostazioni).</summary>
    void CleanupOldSessions();
}
