using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Memory;
using Ruki.Infrastructure.Storage;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test di <see cref="SqliteMemoryStore"/> su un database temporaneo isolato.</summary>
public sealed class SqliteMemoryStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly SqliteMemoryStore _store;

    public SqliteMemoryStoreTests()
    {
        // Sottocartella dedicata e unica per questo test: così il cleanup non tocca gli altri.
        _dir = Path.Combine(Path.GetTempPath(), "ruki-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new SqliteMemoryStore(NullLogger<SqliteMemoryStore>.Instance, Path.Combine(_dir, "ruki.db"));
    }

    private MemoryNode AddNode(string title, MemoryNodeType type, string? parentId = null, string? content = null)
        => _store.Add(new MemoryNode { Title = title, Type = type, ParentId = parentId, Content = content });

    [Fact]
    public void Add_And_GetNode_RoundTrips()
    {
        var node = AddNode("Memoria 1", MemoryNodeType.Memory, content: "contenuto esteso");

        var loaded = _store.GetNode(node.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Memoria 1", loaded!.Title);
        Assert.Equal("contenuto esteso", loaded.Content);
        Assert.Equal(MemoryNodeType.Memory, loaded.Type);
    }

    [Fact]
    public void GetChildren_ReturnsRootsForNull_AndChildrenForParent()
    {
        var category = AddNode("Categoria", MemoryNodeType.Category);
        AddNode("Figlia", MemoryNodeType.Memory, parentId: category.Id);

        var roots = _store.GetChildren(null);
        Assert.Single(roots);
        Assert.Equal("Categoria", roots[0].Title);
        Assert.True(roots[0].HasChildren);

        var children = _store.GetChildren(category.Id);
        Assert.Single(children);
        Assert.Equal("Figlia", children[0].Title);
        Assert.False(children[0].HasChildren);
    }

    [Fact]
    public void GetChildren_OrdersCategoriesBeforeMemories()
    {
        AddNode("zeta memoria", MemoryNodeType.Memory);
        AddNode("alfa categoria", MemoryNodeType.Category);

        var roots = _store.GetChildren(null);

        Assert.Equal(MemoryNodeType.Category, roots[0].Type);
        Assert.Equal(MemoryNodeType.Memory, roots[1].Type);
    }

    [Fact]
    public void Update_ChangesPersistedFields()
    {
        var node = AddNode("Vecchio", MemoryNodeType.Memory, content: "v1");

        node.Title = "Nuovo";
        node.Content = "v2";
        _store.Update(node);

        var loaded = _store.GetNode(node.Id)!;
        Assert.Equal("Nuovo", loaded.Title);
        Assert.Equal("v2", loaded.Content);
    }

    [Fact]
    public void Move_ReparentsNode()
    {
        var catA = AddNode("A", MemoryNodeType.Category);
        var catB = AddNode("B", MemoryNodeType.Category);
        var leaf = AddNode("foglia", MemoryNodeType.Memory, parentId: catA.Id);

        _store.Move(leaf.Id, catB.Id);

        Assert.Empty(_store.GetChildren(catA.Id));
        Assert.Single(_store.GetChildren(catB.Id));
    }

    [Fact]
    public void Delete_RemovesSubtreeViaCascade()
    {
        var category = AddNode("Categoria", MemoryNodeType.Category);
        var child = AddNode("figlia", MemoryNodeType.Memory, parentId: category.Id);

        _store.Delete(category.Id);

        Assert.Null(_store.GetNode(category.Id));
        Assert.Null(_store.GetNode(child.Id));   // rimossa a cascata
    }

    [Fact]
    public void TouchUsage_IncrementsUseCount()
    {
        var node = AddNode("m", MemoryNodeType.Memory);

        _store.TouchUsage(node.Id);
        _store.TouchUsage(node.Id);

        var loaded = _store.GetNode(node.Id)!;
        Assert.Equal(2, loaded.UseCount);
        Assert.NotNull(loaded.LastUsedAt);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();   // rilascia il file prima di cancellarlo
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best effort */ }
    }
}
