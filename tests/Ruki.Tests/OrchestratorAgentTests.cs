using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Abstractions;
using Ruki.Core.Agents;
using Ruki.Core.Llm;
using Ruki.Core.Memory;
using Xunit;

namespace Ruki.Tests;

/// <summary>
/// Test dell'<see cref="OrchestratorAgent"/> con provider LLM e memory store finti:
/// nessuna chiamata reale, nessun file. Verifichiamo cronologia, system prompt e profilo.
/// </summary>
public class OrchestratorAgentTests
{
    private static OrchestratorAgent CreateAgent(
        ILlmProvider provider, IMemoryStore? memory = null, bool hasApiKey = true)
        => new(provider, memory ?? new FakeMemoryStore(),
            new FakeSecretStore(withGeminiKey: hasApiKey), new FakeActivityState(),
            NullLogger<OrchestratorAgent>.Instance);

    [Fact]
    public async Task SendAsync_AppendsUserAndAssistantToHistory()
    {
        var provider = new FakeLlmProvider { Reply = "risposta" };
        var agent = CreateAgent(provider);

        var reply = await agent.SendAsync("ciao");

        Assert.Equal("risposta", reply.Text);
        Assert.Collection(agent.History,
            m => { Assert.Equal(ChatRole.User, m.Role); Assert.Equal("ciao", m.Text); },
            m => { Assert.Equal(ChatRole.Assistant, m.Role); Assert.Equal("risposta", m.Text); });
    }

    [Fact]
    public async Task SendAsync_PassesSystemInstructionAndFullHistory()
    {
        var provider = new FakeLlmProvider { Reply = "ok" };
        var agent = CreateAgent(provider);

        await agent.SendAsync("primo");
        await agent.SendAsync("secondo");

        Assert.False(string.IsNullOrWhiteSpace(provider.LastRequest!.SystemInstruction));
        // Alla seconda chiamata il modello deve vedere: utente1, assistente1, utente2.
        Assert.Equal(3, provider.LastRequest.Messages.Count);
        Assert.Equal("primo", provider.LastRequest.Messages[0].Text);
        Assert.Equal("secondo", provider.LastRequest.Messages[2].Text);
    }

    [Fact]
    public async Task SendAsync_WhenModelRequestsAction_ReturnsActionGoal()
    {
        var provider = new FakeLlmProvider
        {
            Reply = "{ \"reply\": \"Vado subito.\", \"actionGoal\": \"controlla il ticket 123 su Jira\" }",
        };
        var agent = CreateAgent(provider);

        var reply = await agent.SendAsync("controlla il ticket 123");

        Assert.Equal("Vado subito.", reply.Text);
        Assert.Equal("controlla il ticket 123 su Jira", reply.ActionGoal);
        // In cronologia teniamo il testo pulito, non il JSON grezzo.
        Assert.Equal("Vado subito.", agent.History[^1].Text);
    }

    [Fact]
    public async Task SendAsync_IncludesCurrentActivityInSystemInstruction()
    {
        var provider = new FakeLlmProvider { Reply = "ok" };
        var agent = new OrchestratorAgent(provider, new FakeMemoryStore(),
            new FakeSecretStore(withGeminiKey: true), new FakeActivityState(RukiActivity.Training),
            NullLogger<OrchestratorAgent>.Instance);

        await agent.SendAsync("ciao");

        Assert.Contains("TEACHING", provider.LastRequest!.SystemInstruction);
    }

    [Fact]
    public async Task SendAsync_WhenProviderFails_RollsBackUserMessage()
    {
        var provider = new FakeLlmProvider { Throw = new LlmException("boom") };
        var agent = CreateAgent(provider);

        await Assert.ThrowsAsync<LlmException>(() => agent.SendAsync("ciao"));

        Assert.Empty(agent.History);
    }

    [Fact]
    public async Task SendAsync_IncludesKnownProfileInSystemInstruction()
    {
        var memory = new FakeMemoryStore();
        memory.Add(new MemoryNode
        {
            Title = "Profilo utente",
            Type = MemoryNodeType.Memory,
            Content = "È un commercialista.",
        });
        var provider = new FakeLlmProvider { Reply = "ok" };
        var agent = CreateAgent(provider, memory);

        await agent.SendAsync("ciao");

        Assert.Contains("commercialista", provider.LastRequest!.SystemInstruction);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_SavesProfileNode()
    {
        var memory = new FakeMemoryStore();
        var provider = new FakeLlmProvider { Reply = "- Si occupa di contabilità" };
        var agent = CreateAgent(provider, memory);

        await agent.SendAsync("mi occupo di contabilità");
        await agent.UpdateUserProfileAsync();

        var profile = memory.GetChildren(null).Single(n => n.Title == "Profilo utente");
        Assert.Equal("- Si occupa di contabilità", memory.GetNode(profile.Id)!.Content);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithoutUserMessages_DoesNothing()
    {
        var memory = new FakeMemoryStore();
        var agent = CreateAgent(new FakeLlmProvider { Reply = "x" }, memory);

        await agent.UpdateUserProfileAsync();

        Assert.Empty(memory.GetChildren(null));
    }

    [Fact]
    public void WelcomeMessage_DiffersWhenProfileExists()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
            var memory = new FakeMemoryStore();
            var agent = CreateAgent(new FakeLlmProvider(), memory);

            Assert.Contains("raccontami", agent.WelcomeMessage);   // primo avvio: chiede l'introduzione

            memory.Add(new MemoryNode { Title = "Profilo utente", Type = MemoryNodeType.Memory, Content = "È uno sviluppatore." });
            Assert.Contains("Bentornato", agent.WelcomeMessage);   // utente già conosciuto
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void WelcomeMessage_WhenProfileDisabled_DoesNotClaimToKnowUser()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
            var memory = new FakeMemoryStore();
            memory.Add(new MemoryNode
            {
                Title = "Profilo utente",
                Type = MemoryNodeType.Memory,
                Content = "È uno sviluppatore.",
                IsObsolete = true,   // profilo archiviato/disattivato
            });
            var agent = CreateAgent(new FakeLlmProvider(), memory);

            Assert.Contains("raccontami", agent.WelcomeMessage);   // primo avvio, non "Bentornato"
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void WelcomeMessage_WhenNoApiKey_AsksToConfigureIt()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
            var agent = CreateAgent(new FakeLlmProvider(), hasApiKey: false);

            Assert.Contains("chiave API", agent.WelcomeMessage);   // chiede di configurare la chiave
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void WelcomeMessage_IsEnglishWhenUiCultureIsEnglish()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var agent = CreateAgent(new FakeLlmProvider(), new FakeMemoryStore());

            Assert.Contains("tell me a bit about yourself", agent.WelcomeMessage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public async Task Reset_ClearsHistory()
    {
        var agent = CreateAgent(new FakeLlmProvider { Reply = "ok" });
        await agent.SendAsync("ciao");

        agent.Reset();

        Assert.Empty(agent.History);
    }
}
