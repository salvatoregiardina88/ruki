namespace Ruki.Core.Agents;

/// <summary>Tipo di azione che l'Action Agent può eseguire sul PC, o segnali di fine.</summary>
public enum AgentActionType
{
    Click,
    DoubleClick,
    RightClick,
    Type,
    Key,
    Scroll,
    Wait,
    ExpandNode,    // chiede i figli di un nodo di memoria
    ReadMemory,    // chiede il contenuto esteso di una o più memorie
    ListWindows,   // chiede l'elenco aggiornato delle finestre aperte
    Done,          // compito completato
    Fail,          // impossibile completare
}

/// <summary>
/// Una singola azione decisa dal modello a ogni passo del loop "computer use".
/// I campi non pertinenti a un certo tipo restano <c>null</c>.
/// </summary>
public sealed record AgentAction(
    AgentActionType Type,
    int? X = null,
    int? Y = null,
    string? Text = null,    // testo da digitare (Type) o combinazione di tasti (Key)
    int? Amount = null,     // tacche di scroll (Scroll) o millisecondi (Wait)
    string? Message = null, // motivazione per Done/Fail
    string? Window = null,  // finestra/app attesa in primo piano per gli input da tastiera (Type/Key)
    string? NodeId = null,  // nodo di memoria da espandere (ExpandNode)
    IReadOnlyList<string>? NodeIds = null, // memorie da leggere (ReadMemory)
    bool Risky = false)     // il modello segnala un'azione distruttiva/irreversibile da confermare
{
    /// <summary>
    /// Descrizione tecnica e breve dell'azione (in inglese, come i prompt): usata nei log,
    /// nelle note inviate al modello e nel dialogo di conferma. Per "type"/"key" mostra anche
    /// il contenuto, utile per decidere se confermare. Le coordinate X/Y sono nella scala del
    /// modello (normalizzate 0–1000), così la nota rimandata al modello parla la sua stessa lingua.
    /// </summary>
    public string Describe() => Type switch
    {
        AgentActionType.Click => $"Click at ({X},{Y})",
        AgentActionType.DoubleClick => $"Double-click at ({X},{Y})",
        AgentActionType.RightClick => $"Right-click at ({X},{Y})",
        AgentActionType.Type => $"Type: \"{Text}\"",
        AgentActionType.Key => $"Press: {Text}",
        AgentActionType.Scroll => $"Scroll {Amount} at ({X},{Y})",
        AgentActionType.Wait => $"Wait {Amount} ms",
        AgentActionType.ExpandNode => $"Expand node {NodeId}",
        AgentActionType.ReadMemory => $"Read memories {(NodeIds is null ? NodeId : string.Join(", ", NodeIds))}",
        AgentActionType.ListWindows => "List open windows",
        AgentActionType.Done => $"Done: {Message}",
        AgentActionType.Fail => $"Failed: {Message}",
        _ => Type.ToString(),
    };
}
