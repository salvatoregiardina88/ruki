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
        ILlmProvider provider, IUserProfileMemory? profile = null, bool hasApiKey = true)
        => new(provider, profile ?? new FakeUserProfileMemory(),
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
        var agent = new OrchestratorAgent(provider, new FakeUserProfileMemory(),
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
        var profile = new FakeUserProfileMemory { Active = "È un commercialista." };
        var provider = new FakeLlmProvider { Reply = "ok" };
        var agent = CreateAgent(provider, profile);

        await agent.SendAsync("ciao");

        Assert.Contains("commercialista", provider.LastRequest!.SystemInstruction);
    }

    [Fact]
    public async Task SendAsync_WhenModelSetsProfileNote_RemembersDurableFact()
    {
        var profile = new FakeUserProfileMemory();
        var provider = new FakeLlmProvider
        {
            Reply = "{ \"reply\": \"Annotato!\", \"actionGoal\": null, \"profileNote\": \"L'utente è un commercialista\" }",
        };
        var agent = CreateAgent(provider, profile);

        var reply = await agent.SendAsync("sono un commercialista");

        Assert.Equal("Annotato!", reply.Text);
        Assert.Equal("L'utente è un commercialista", profile.LastRemembered);   // davvero scritto, non solo "detto"
    }

    [Fact]
    public async Task SendAsync_WhenNoProfileNote_RemembersNothing()
    {
        var profile = new FakeUserProfileMemory();
        var provider = new FakeLlmProvider { Reply = "{ \"reply\": \"Ciao!\", \"actionGoal\": null, \"profileNote\": null }" };
        var agent = CreateAgent(provider, profile);

        await agent.SendAsync("ciao");

        Assert.Null(profile.LastRemembered);
    }

    [Fact]
    public void WelcomeMessage_DiffersWhenProfileExists()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
            var profile = new FakeUserProfileMemory();
            var agent = CreateAgent(new FakeLlmProvider(), profile);

            Assert.Contains("raccontami", agent.WelcomeMessage);   // primo avvio: chiede l'introduzione

            profile.Active = "È uno sviluppatore.";
            Assert.Contains("Bentornato", agent.WelcomeMessage);   // utente già conosciuto
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
            var agent = CreateAgent(new FakeLlmProvider());

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
