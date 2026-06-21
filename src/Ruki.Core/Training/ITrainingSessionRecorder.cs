using Ruki.Core.Llm;

namespace Ruki.Core.Training;

/// <summary>
/// Registra una sessione di addestramento: schermo (a fps + sugli eventi), audio, eventi del PC
/// e messaggi di chat, tutto su una timeline. Allo stop produce un video MP4 con audio.
/// </summary>
public interface ITrainingSessionRecorder
{
    bool IsRecording { get; }

    /// <summary>
    /// Microfono in muto: se true, durante la registrazione si cattura silenzio al posto della voce.
    /// Si può cambiare in qualsiasi momento mentre la sessione è in corso.
    /// </summary>
    bool IsMicMuted { get; set; }

    /// <summary>Tempo trascorso dall'inizio della sessione corrente.</summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Sollevato quando si raggiunge la durata massima consigliata: serve a mostrare un avviso
    /// NON bloccante (la registrazione continua finché l'utente non la ferma).
    /// </summary>
    event Action? MaxDurationReached;

    /// <summary>Avvia una nuova sessione. Da chiamare dal thread UI (installa gli hook).</summary>
    void Start();

    /// <summary>Annota nella timeline un messaggio di chat (no-op se non si sta registrando).</summary>
    void NoteChatMessage(ChatRole role, string text);

    /// <summary>Ferma la sessione, codifica il video e restituisce il riepilogo.</summary>
    Task<TrainingSessionInfo> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Interrompe subito la registrazione rilasciando le risorse (timer, hook, audio, file) ma
    /// SENZA codificare il video. Pensato per la chiusura dell'app: evita di bloccare lo shutdown
    /// su una codifica lenta. Gli artefatti grezzi (frame, audio, eventi) restano su disco.
    /// </summary>
    void Abort();

    /// <summary>
    /// Annulla la registrazione in corso: ferma tutto come <see cref="Abort"/> e in più ELIMINA
    /// tutti i dati della sessione salvati finora. Niente video, niente apprendimento: come se la
    /// sessione non fosse mai avvenuta.
    /// </summary>
    void Discard();
}
