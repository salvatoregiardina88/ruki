namespace Ruki.Core.Agents;

/// <summary>Direzione di una voce della conversazione con l'Action Agent.</summary>
public enum ActionTraceKind
{
    /// <summary>Ciò che inviamo al modello (prompt + screenshot).</summary>
    Sent,

    /// <summary>Ciò che il modello risponde (azione decisa).</summary>
    Received,
}

/// <summary>
/// Una voce della conversazione con l'Action Agent, per la finestra di debug.
/// <see cref="X"/>/<see cref="Y"/> sono valorizzate quando l'azione ha delle coordinate (per disegnarci un cerchio).
/// <see cref="Step"/> è il numero del passo (0 = istruzioni iniziali).
/// </summary>
public sealed record ActionTraceEntry(ActionTraceKind Kind, string Text, byte[]? ImageJpeg, int? X, int? Y, int Step);

/// <summary>
/// Raccoglie la conversazione con l'Action Agent quando la modalità debug è attiva, e la espone
/// alla finestra di debug. Quando è disattivata, non fa nulla (zero overhead).
/// </summary>
public interface IActionTrace
{
    /// <summary>Se false, <see cref="Add"/> è un no-op.</summary>
    bool Enabled { get; set; }

    event Action<ActionTraceEntry>? EntryAdded;
    event Action? Cleared;

    void Clear();
    void Add(ActionTraceEntry entry);
}
