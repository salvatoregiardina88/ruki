using Ruki.Core.Memory;

namespace Ruki.Tests;

/// <summary>
/// Implementazione in memoria di <see cref="IMemoryStore"/> per i test: niente file, niente SQLite.
/// </summary>
internal sealed class FakeMemoryStore : IMemoryStore
{
    private readonly Dictionary<string, MemoryNode> _nodes = new();

    public IReadOnlyList<MemoryNodeInfo> GetChildren(string? parentId)
        => _nodes.Values
            .Where(n => n.ParentId == parentId)
            .Select(n => new MemoryNodeInfo(
                n.Id, n.ParentId, n.Type, n.Title, n.Summary,
                _nodes.Values.Any(c => c.ParentId == n.Id), n.IsObsolete))
            .ToList();

    public MemoryNode? GetNode(string id) => _nodes.TryGetValue(id, out var node) ? node : null;

    public MemoryNode Add(MemoryNode node)
    {
        if (string.IsNullOrEmpty(node.Id))
            node.Id = Guid.NewGuid().ToString("N");
        node.CreatedAt = node.UpdatedAt = DateTimeOffset.UtcNow;
        _nodes[node.Id] = node;
        return node;
    }

    public void Update(MemoryNode node)
    {
        node.UpdatedAt = DateTimeOffset.UtcNow;
        _nodes[node.Id] = node;
    }

    public void Move(string id, string? newParentId)
    {
        if (_nodes.TryGetValue(id, out var node))
            node.ParentId = newParentId;
    }

    public void Delete(string id)
    {
        // Rimuove il nodo e, ricorsivamente, i suoi discendenti.
        foreach (var child in _nodes.Values.Where(n => n.ParentId == id).ToList())
            Delete(child.Id);
        _nodes.Remove(id);
    }

    public void TouchUsage(string id)
    {
        if (_nodes.TryGetValue(id, out var node))
        {
            node.UseCount++;
            node.LastUsedAt = DateTimeOffset.UtcNow;
        }
    }

    public void SetObsolete(string id, bool obsolete)
    {
        if (_nodes.TryGetValue(id, out var node))
            node.IsObsolete = obsolete;
    }
}
