using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Memory;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test di <see cref="UserProfileMemory"/>: merge parsimonioso del profilo utente.</summary>
public class UserProfileMemoryTests
{
    private static UserProfileMemory Create(FakeLlmProvider provider, IMemoryStore memory)
        => new(provider, memory, NullLogger<UserProfileMemory>.Instance);

    [Fact]
    public async Task RememberAsync_CreatesProfileNode_WhenNoneExists()
    {
        var memory = new FakeMemoryStore();
        var provider = new FakeLlmProvider { Reply = "- È un commercialista" };
        var profile = Create(provider, memory);

        await profile.RememberAsync("L'utente è un commercialista");

        var node = memory.GetChildren(null).Single(n => n.Title == UserProfileMemory.ProfileNodeTitle);
        Assert.Equal("- È un commercialista", memory.GetNode(node.Id)!.Content);
    }

    [Fact]
    public async Task RememberAsync_PassesExistingProfileToModel_ForMerge()
    {
        var memory = new FakeMemoryStore();
        memory.Add(new MemoryNode { Title = UserProfileMemory.ProfileNodeTitle, Type = MemoryNodeType.Memory, Content = "- Sviluppatore C#" });
        var provider = new FakeLlmProvider { Reply = "- Sviluppatore C#\n- Usa Git" };
        var profile = Create(provider, memory);

        await profile.RememberAsync("usa git");

        // Il profilo ESISTENTE è passato al modello: unisce, non sovrascrive alla cieca.
        Assert.Contains("Sviluppatore C#", provider.LastRequest!.Messages[0].Text);
    }

    [Fact]
    public async Task RememberAsync_IgnoresBlankFact()
    {
        var memory = new FakeMemoryStore();
        var provider = new FakeLlmProvider { Reply = "x" };
        var profile = Create(provider, memory);

        await profile.RememberAsync("   ");

        Assert.Null(provider.LastRequest);          // niente chiamata al modello
        Assert.Empty(memory.GetChildren(null));     // niente scritto
    }

    [Fact]
    public void GetActiveProfile_ReturnsNull_WhenObsolete()
    {
        var memory = new FakeMemoryStore();
        memory.Add(new MemoryNode
        {
            Title = UserProfileMemory.ProfileNodeTitle,
            Type = MemoryNodeType.Memory,
            Content = "È uno sviluppatore.",
            IsObsolete = true,   // archiviato dall'utente
        });
        var profile = Create(new FakeLlmProvider(), memory);

        Assert.Null(profile.GetActiveProfile());
    }
}
