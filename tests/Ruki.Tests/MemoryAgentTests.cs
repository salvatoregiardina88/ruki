using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Agents;
using Ruki.Core.Memory;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test del <see cref="MemoryAgent"/> (collocazione della conoscenza nell'albero).</summary>
public class MemoryAgentTests
{
    private static MemoryAgent CreateAgent(IMemoryStore store)
        => new(store, NullLogger<MemoryAgent>.Instance);

    [Fact]
    public void Store_CreatesCategoryPathAndLeaf()
    {
        var store = new FakeMemoryStore();
        var agent = CreateAgent(store);

        var path = agent.Store(new LearnedKnowledge(
            Title: "Inserimento fattura",
            Summary: "come si registra una fattura",
            Content: "passi...",
            CategoryPath: ["Software", "Excel"]));

        Assert.Equal("Software / Excel / Inserimento fattura", path);

        var software = store.GetChildren(null).Single();
        Assert.Equal("Software", software.Title);
        var excel = store.GetChildren(software.Id).Single();
        Assert.Equal("Excel", excel.Title);
        var memory = store.GetChildren(excel.Id).Single();
        Assert.Equal("Inserimento fattura", memory.Title);
        Assert.Equal(MemoryNodeType.Memory, memory.Type);
    }

    [Fact]
    public void Store_ReusesExistingCategory_CaseInsensitive()
    {
        var store = new FakeMemoryStore();
        var agent = CreateAgent(store);

        agent.Store(new LearnedKnowledge("Prima", "s", "c", ["Software"]));
        agent.Store(new LearnedKnowledge("Seconda", "s", "c", ["SOFTWARE"]));

        // Una sola categoria "Software", con due memorie sotto.
        var roots = store.GetChildren(null);
        Assert.Single(roots);
        Assert.Equal(2, store.GetChildren(roots[0].Id).Count);
    }

    [Fact]
    public void Store_WithEmptyPath_PutsMemoryAtRoot()
    {
        var store = new FakeMemoryStore();
        var agent = CreateAgent(store);

        agent.Store(new LearnedKnowledge("Senza categoria", "s", "c", []));

        var roots = store.GetChildren(null);
        Assert.Single(roots);
        Assert.Equal(MemoryNodeType.Memory, roots[0].Type);
    }
}
