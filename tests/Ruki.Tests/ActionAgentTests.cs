using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Agents;
using Ruki.Core.Automation;
using Ruki.Core.Capture;
using Ruki.Core.Memory;
using Xunit;

namespace Ruki.Tests;

/// <summary>
/// Test del loop dell'<see cref="ActionAgent"/> con modello, schermo e automazione finti:
/// nessuna azione reale sul PC.
/// </summary>
public class ActionAgentTests
{
    [Fact]
    public async Task RunAsync_WhenModelSaysDone_SucceedsWithoutActing()
    {
        var provider = new FakeLlmProvider
        {
            Replies = new(["{ \"action\": \"done\", \"message\": \"completato\" }"]),
        };
        var automation = new FakeAutomation();
        var agent = CreateAgent(provider, automation);

        var result = await agent.RunAsync("fai qualcosa", new ActionController());

        Assert.True(result.Success);
        Assert.Equal(1, result.Steps);
        Assert.Empty(automation.Actions);
    }

    [Fact]
    public async Task RunAsync_ExecutesActionsThenStopsOnDone()
    {
        var provider = new FakeLlmProvider
        {
            Replies = new(
            [
                // x,y normalizzati 0–1000: su uno schermo finto 800x600 → pixel (400, 300).
                "{ \"action\": \"click\", \"x\": 500, \"y\": 500 }",
                "{ \"action\": \"done\", \"message\": \"fatto\" }",
            ]),
        };
        var automation = new FakeAutomation();
        var agent = CreateAgent(provider, automation);

        var result = await agent.RunAsync("clicca qualcosa", new ActionController());

        Assert.True(result.Success);
        Assert.Equal(2, result.Steps);
        Assert.Contains("click(400,300)", automation.Actions);   // eseguito davvero in pixel

        // La nota rimandata al modello deve usare le SUE coordinate (0–1000), non i pixel: altrimenti
        // il modello si confonde ("ho cliccato a 500 ma il sistema dice 400?") e "si corregge" sbagliando.
        Assert.Contains(provider.LastRequest!.Messages, m => m.Text.Contains("Click at (500,500)"));
        Assert.DoesNotContain(provider.LastRequest!.Messages, m => m.Text.Contains("(400,300)"));
    }

    [Fact]
    public async Task RunAsync_WhenStopped_ThrowsCancellation()
    {
        var provider = new FakeLlmProvider { Reply = "{ \"action\": \"wait\", \"amount\": 100 }" };
        var agent = CreateAgent(provider, new FakeAutomation());

        var controller = new ActionController();
        controller.Stop();   // l'utente ferma prima ancora di iniziare

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agent.RunAsync("x", controller));
    }

    [Fact]
    public async Task RunAsync_ReadsMemoryContentOnRequest()
    {
        var memory = new FakeMemoryStore();
        memory.Add(new MemoryNode { Id = "n1", Title = "Jira", Type = MemoryNodeType.Memory, Content = "https://jira.esempio" });
        var provider = new FakeLlmProvider
        {
            Replies = new(
            [
                "{ \"action\": \"read_memory\", \"nodeIds\": [\"n1\"] }",
                "{ \"action\": \"done\", \"message\": \"ok\" }",
            ]),
        };
        var agent = CreateAgent(provider, new FakeAutomation(), memory: memory);

        var result = await agent.RunAsync("vai su jira", new ActionController());

        Assert.True(result.Success);
        // Dopo la lettura, il contenuto della memoria è finito nella conversazione inviata al modello.
        Assert.Contains(provider.LastRequest!.Messages, m => m.Text.Contains("jira.esempio"));
    }

    [Fact]
    public async Task RunAsync_SkipsKeyboardAction_WhenExpectedWindowNotActive()
    {
        // L'agente vuole digitare in "Notepad" ma in primo piano c'è Chrome: l'azione va saltata.
        var provider = new FakeLlmProvider
        {
            Replies = new(
            [
                "{ \"action\": \"type\", \"text\": \"ciao\", \"window\": \"Notepad\" }",
                "{ \"action\": \"done\", \"message\": \"ok\" }",
            ]),
        };
        var automation = new FakeAutomation();
        var foreground = new FakeForegroundWindow(new ForegroundWindowInfo("chrome", "Gmail - Google Chrome"));
        var agent = CreateAgent(provider, automation, foreground);

        var result = await agent.RunAsync("scrivi", new ActionController());

        Assert.True(result.Success);
        Assert.DoesNotContain("type(ciao)", automation.Actions);   // non digitato: finestra sbagliata
    }

    [Fact]
    public async Task RunAsync_WhenRiskyActionDeclined_DoesNotExecuteIt()
    {
        var provider = new FakeLlmProvider
        {
            Replies = new(
            [
                "{ \"action\": \"click\", \"x\": 500, \"y\": 500, \"risky\": true }",
                "{ \"action\": \"done\", \"message\": \"ok\" }",
            ]),
        };
        var automation = new FakeAutomation();
        var confirmation = new FakeConfirmation(approve: false);
        var agent = CreateAgent(provider, automation, confirmation: confirmation);

        var result = await agent.RunAsync("fai qualcosa di rischioso", new ActionController());

        Assert.True(result.Success);
        Assert.Equal(1, confirmation.Calls);
        Assert.Empty(automation.Actions);   // azione rischiosa rifiutata: non eseguita
    }

    [Fact]
    public async Task RunAsync_WhenRiskyActionApproved_ExecutesIt()
    {
        var provider = new FakeLlmProvider
        {
            Replies = new(
            [
                "{ \"action\": \"click\", \"x\": 500, \"y\": 500, \"risky\": true }",
                "{ \"action\": \"done\", \"message\": \"ok\" }",
            ]),
        };
        var automation = new FakeAutomation();
        var confirmation = new FakeConfirmation(approve: true);
        var agent = CreateAgent(provider, automation, confirmation: confirmation);

        await agent.RunAsync("x", new ActionController());

        Assert.Equal(1, confirmation.Calls);
        Assert.Contains("click(400,300)", automation.Actions);   // approvata: eseguita
    }

    private static ActionAgent CreateAgent(
        FakeLlmProvider provider,
        FakeAutomation automation,
        IForegroundWindowService? foreground = null,
        IMemoryStore? memory = null,
        IActionConfirmation? confirmation = null)
        => new(
            provider,
            new FakeScreen(),
            automation,
            foreground ?? new FakeForegroundWindow(),
            new NullCaretContextProvider(),
            new NullClickIndicator(),
            memory ?? new FakeMemoryStore(),
            new FakeSettingsService(),
            new ActionTrace(),
            confirmation ?? new FakeConfirmation(),
            NullLogger<ActionAgent>.Instance);

    private sealed class FakeConfirmation(bool approve = true) : IActionConfirmation
    {
        public int Calls { get; private set; }

        public Task<bool> ConfirmAsync(string actionDescription, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(approve);
        }
    }

    private sealed class FakeForegroundWindow(ForegroundWindowInfo? info = null) : IForegroundWindowService
    {
        public ForegroundWindowInfo? GetForeground() => info;
        public IReadOnlyList<ForegroundWindowInfo> GetOpenWindows() => [];
    }

    private sealed class NullClickIndicator : IClickIndicator
    {
        public void Start() { }
        public void Stop() { }
        public void SetClicking(bool clicking) { }
    }

    private sealed class NullCaretContextProvider : ICaretContextProvider
    {
        public string? Describe() => null;
    }

    private sealed class FakeScreen : IScreenCaptureService
    {
        public CapturedFrame Capture(bool highlightCursor = false) => new([0, 1, 2], 800, 600);
    }

    private sealed class FakeAutomation : IInputAutomationService
    {
        public List<string> Actions { get; } = [];

        public void MoveMouse(int x, int y) => Actions.Add($"move({x},{y})");
        public void Click(int x, int y, MouseButton button = MouseButton.Left) => Actions.Add($"click({x},{y})");
        public void DoubleClick(int x, int y, MouseButton button = MouseButton.Left) => Actions.Add($"double({x},{y})");
        public void Scroll(int x, int y, int notches) => Actions.Add($"scroll({x},{y},{notches})");
        public void TypeText(string text) => Actions.Add($"type({text})");
        public void PressKeys(string combination) => Actions.Add($"keys({combination})");
    }
}
