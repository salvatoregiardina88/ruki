namespace Ruki.Core.Memory;

/// <summary>Natura di un nodo dell'albero di memoria.</summary>
public enum MemoryNodeType
{
    /// <summary>Nodo interno: categoria/sottocategoria che raggruppa altri nodi.</summary>
    Category,

    /// <summary>Nodo foglia: una singola memoria con contenuto esteso.</summary>
    Memory,
}

/// <summary>
/// Nodo completo dell'albero di memoria. Le categorie organizzano l'albero (titolo + riassunto),
/// le memorie (foglie) contengono la conoscenza vera e propria in <see cref="Content"/>.
/// </summary>
public sealed class MemoryNode
{
    /// <summary>Identificativo univoco (GUID). Assegnato dallo store se non valorizzato.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Nodo padre. <c>null</c> per i nodi radice.</summary>
    public string? ParentId { get; set; }

    public MemoryNodeType Type { get; set; }

    /// <summary>Titolo breve mostrato durante la navigazione.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Riassunto breve, usato per la navigazione senza caricare il contenuto esteso.</summary>
    public string? Summary { get; set; }

    /// <summary>Contenuto esteso della memoria (significativo solo per le foglie).</summary>
    public string? Content { get; set; }

    /// <summary>Metadati liberi in formato JSON (software, tag, sessione di origine…).</summary>
    public string? Metadata { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Quante volte la memoria è stata usata (per ranking/pruning).</summary>
    public int UseCount { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Se true la memoria è "archiviata": non viene usata dagli agenti, ma resta visibile in
    /// Impostazioni → Memoria e l'utente può riattivarla.
    /// </summary>
    public bool IsObsolete { get; set; }
}
