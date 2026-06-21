using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Agents;
using Ruki.Core.Memory;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test del <see cref="MemoryMaintenanceAgent"/> (deduplica/fusione delle memorie).</summary>
public class MemoryMaintenanceAgentTests
{
    [Fact]
    public async Task RunAsync_MergesDuplicateMemories()
    {
        var store = new FakeMemoryStore();
        store.Add(new MemoryNode { Id = "a", Title = "Jira URL", Type = MemoryNodeType.Memory, Content = "https://jira" });
        store.Add(new MemoryNode { Id = "b", Title = "Indirizzo Jira", Type = MemoryNodeType.Memory, Content = "jira: https://jira" });

        var provider = new FakeLlmProvider
        {
            Reply = "{ \"merges\": [ { \"ids\": [\"a\",\"b\"], \"title\": \"Jira\", \"summary\": \"url\", \"content\": \"https://jira\" } ] }",
        };
        var agent = new MemoryMaintenanceAgent(store, provider, new FakeSettingsService(), NullLogger<MemoryMaintenanceAgent>.Instance);

        var report = await agent.RunAsync();

        Assert.Equal(1, report.MergedGroups);
        Assert.Equal(1, report.RemovedMemories);
        Assert.Null(store.GetNode("b"));                 // accorpata e rimossa
        Assert.Equal("Jira", store.GetNode("a")!.Title); // la prima conserva la posizione, aggiornata
        Assert.Equal("https://jira", store.GetNode("a")!.Content);
    }

    [Fact]
    public async Task RunAsync_WithFewerThanTwoMemories_DoesNothing()
    {
        var store = new FakeMemoryStore();
        store.Add(new MemoryNode { Id = "a", Title = "x", Type = MemoryNodeType.Memory, Content = "c" });
        var provider = new FakeLlmProvider();
        var agent = new MemoryMaintenanceAgent(store, provider, new FakeSettingsService(), NullLogger<MemoryMaintenanceAgent>.Instance);

        var report = await agent.RunAsync();

        Assert.False(report.MadeChanges);
        Assert.Null(provider.LastRequest);   // nemmeno chiamato il modello
    }

    [Fact]
    public async Task RunAsync_ReorganizesMemoriesIntoCategories_AndRemovesEmptyCategories()
    {
        var store = new FakeMemoryStore();
        store.Add(new MemoryNode { Id = "m1", Title = "Fatture Excel", Type = MemoryNodeType.Memory, Content = "..." });
        store.Add(new MemoryNode { Id = "m2", Title = "Ticket Jira", Type = MemoryNodeType.Memory, Content = "..." });
        store.Add(new MemoryNode { Id = "old", Title = "Vecchia", Type = MemoryNodeType.Category });

        var provider = new FakeLlmProvider
        {
            Reply =
                """
                { "merges": [],
                  "placements": [
                    { "id": "m1", "categoryPath": ["Software", "Excel"] },
                    { "id": "m2", "categoryPath": ["Software", "Jira"] }
                  ] }
                """,
        };
        var agent = new MemoryMaintenanceAgent(store, provider, new FakeSettingsService(), NullLogger<MemoryMaintenanceAgent>.Instance);

        var report = await agent.RunAsync();

        Assert.Equal(2, report.MovedMemories);
        Assert.Null(store.GetNode("old"));   // categoria rimasta vuota → rimossa

        var roots = store.GetChildren(null);
        Assert.Single(roots);
        Assert.Equal("Software", roots[0].Title);
        Assert.Equal(2, store.GetChildren(roots[0].Id).Count);   // Excel, Jira
    }

    [Fact]
    public async Task RunAsync_StripsCategoryPrefixFromTitle()
    {
        var store = new FakeMemoryStore();
        store.Add(new MemoryNode { Id = "m1", Title = "Procedure / Jira / Ricerca ticket", Type = MemoryNodeType.Memory, Content = "x" });
        store.Add(new MemoryNode { Id = "m2", Title = "Altro", Type = MemoryNodeType.Memory, Content = "y" });

        var provider = new FakeLlmProvider
        {
            Reply = """{ "merges": [], "placements": [ { "id": "m1", "categoryPath": ["Procedure", "Jira"] } ] }""",
        };
        var agent = new MemoryMaintenanceAgent(store, provider, new FakeSettingsService(), NullLogger<MemoryMaintenanceAgent>.Instance);

        await agent.RunAsync();

        Assert.Equal("Ricerca ticket", store.GetNode("m1")!.Title);   // tolto il prefisso di categoria
    }

    [Fact]
    public async Task RunAsync_ArchivesOldUnusedMemories()
    {
        var store = new FakeMemoryStore();
        var old = store.Add(new MemoryNode { Title = "Vecchia inutilizzata", Type = MemoryNodeType.Memory, Content = "c" });
        old.LastUsedAt = DateTimeOffset.UtcNow.AddDays(-200);   // vecchia e con 0 utilizzi
        var used = store.Add(new MemoryNode { Title = "Usata spesso", Type = MemoryNodeType.Memory, Content = "c", UseCount = 5 });

        var provider = new FakeLlmProvider { Reply = """{ "merges": [], "placements": [] }""" };
        var agent = new MemoryMaintenanceAgent(store, provider, new FakeSettingsService(), NullLogger<MemoryMaintenanceAgent>.Instance);

        var report = await agent.RunAsync();

        Assert.Equal(1, report.ObsoletedMemories);
        Assert.True(store.GetNode(old.Id)!.IsObsolete);     // archiviata
        Assert.False(store.GetNode(used.Id)!.IsObsolete);   // attiva
    }

    [Fact]
    public async Task RunAsync_IgnoresMergeGroupsWithUnknownIds()
    {
        var store = new FakeMemoryStore();
        store.Add(new MemoryNode { Id = "a", Title = "A", Type = MemoryNodeType.Memory, Content = "ca" });
        store.Add(new MemoryNode { Id = "b", Title = "B", Type = MemoryNodeType.Memory, Content = "cb" });

        // Un id inesistente: il gruppo resta con un solo id valido → nessuna fusione.
        var provider = new FakeLlmProvider
        {
            Reply = "{ \"merges\": [ { \"ids\": [\"a\",\"zzz\"], \"title\": \"X\", \"content\": \"y\" } ] }",
        };
        var agent = new MemoryMaintenanceAgent(store, provider, new FakeSettingsService(), NullLogger<MemoryMaintenanceAgent>.Instance);

        var report = await agent.RunAsync();

        Assert.Equal(0, report.MergedGroups);
        Assert.NotNull(store.GetNode("a"));
        Assert.NotNull(store.GetNode("b"));
    }
}
