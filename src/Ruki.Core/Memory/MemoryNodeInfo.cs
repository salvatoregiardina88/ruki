namespace Ruki.Core.Memory;

/// <summary>
/// Vista "leggera" di un nodo, usata per navigare l'albero senza caricare il contenuto esteso.
/// È ciò che un agente riceve quando esplode una categoria: titolo, riassunto e se ha figli.
/// </summary>
public sealed record MemoryNodeInfo(
    string Id,
    string? ParentId,
    MemoryNodeType Type,
    string Title,
    string? Summary,
    bool HasChildren,
    bool IsObsolete);
