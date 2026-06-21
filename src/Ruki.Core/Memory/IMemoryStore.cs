namespace Ruki.Core.Memory;

/// <summary>
/// Archivio della memoria di Ruki, organizzata ad albero (categorie → sottocategorie → memorie).
/// <para>
/// La navigazione è pensata per essere "esplosa" un livello alla volta (<see cref="GetChildren"/>),
/// così agenti e UI non devono mai caricare tutto l'albero in una volta.
/// </para>
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Figli diretti del nodo indicato (viste leggere). Passare <c>null</c> per ottenere i nodi radice.
    /// </summary>
    IReadOnlyList<MemoryNodeInfo> GetChildren(string? parentId);

    /// <summary>Nodo completo (incluso il contenuto esteso), oppure <c>null</c> se non esiste.</summary>
    MemoryNode? GetNode(string id);

    /// <summary>
    /// Inserisce un nuovo nodo, assegnando id (se mancante) e timestamp. Restituisce il nodo salvato.
    /// </summary>
    MemoryNode Add(MemoryNode node);

    /// <summary>Aggiorna i campi modificabili del nodo e ne aggiorna il timestamp.</summary>
    void Update(MemoryNode node);

    /// <summary>Sposta un nodo (e il suo sottoalbero) sotto un nuovo padre (<c>null</c> = radice).</summary>
    void Move(string id, string? newParentId);

    /// <summary>Elimina un nodo e, a cascata, tutto il suo sottoalbero.</summary>
    void Delete(string id);

    /// <summary>Registra un "uso" del nodo (incrementa il contatore e aggiorna l'ultimo accesso).</summary>
    void TouchUsage(string id);

    /// <summary>Marca il nodo come obsoleto (archiviato) o lo riattiva.</summary>
    void SetObsolete(string id, bool obsolete);
}
