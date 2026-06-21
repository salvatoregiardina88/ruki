using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Agents;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test del <see cref="TrainingAgent"/> (upload del video + parsing del JSON di conoscenza).</summary>
public class TrainingAgentTests
{
    [Fact]
    public async Task AnalyzeAsync_UploadsVideoAndParsesMemories()
    {
        var provider = new FakeLlmProvider
        {
            Reply =
                """
                {
                  "memories": [
                    {
                      "kind": "procedura",
                      "title": "Registrazione fattura",
                      "summary": "come registrare una fattura in Excel",
                      "content": "1. apri Excel\n2. ...",
                      "categoryPath": ["Software", "Excel"]
                    }
                  ]
                }
                """,
        };
        var agent = new TrainingAgent(provider, NullLogger<TrainingAgent>.Instance);

        var memories = await agent.AnalyzeAsync("C:/tmp/video.mp4", "timeline...", ["Software"]);

        Assert.Contains("C:/tmp/video.mp4", provider.UploadedFiles);   // il video è stato caricato
        var knowledge = Assert.Single(memories);
        Assert.Equal("Registrazione fattura", knowledge.Title);
        Assert.Equal("procedura", knowledge.Kind);
        Assert.Equal(["Software", "Excel"], knowledge.CategoryPath);
        Assert.Contains("apri Excel", knowledge.Content);
    }

    [Fact]
    public async Task AnalyzeAsync_ExtractsMultipleMemoriesFromOneSession()
    {
        var provider = new FakeLlmProvider
        {
            Reply =
                """
                { "memories": [
                    { "kind": "procedura", "title": "A", "summary": "s", "content": "ca", "categoryPath": ["X"] },
                    { "kind": "nozione", "title": "B", "summary": "s", "content": "cb", "categoryPath": ["Y"] }
                ] }
                """,
        };
        var agent = new TrainingAgent(provider, NullLogger<TrainingAgent>.Instance);

        var memories = await agent.AnalyzeAsync("v.mp4", "t", []);

        Assert.Equal(2, memories.Count);
        Assert.Equal("A", memories[0].Title);
        Assert.Equal("procedura", memories[0].Kind);
        Assert.Equal("B", memories[1].Title);
        Assert.Equal("nozione", memories[1].Kind);
    }

    [Fact]
    public async Task AnalyzeAsync_ToleratesMarkdownFencesAndSingleObject()
    {
        // Niente array "memories": un singolo oggetto va comunque accettato (fallback).
        var provider = new FakeLlmProvider
        {
            Reply = "Ecco:\n```json\n{\"title\":\"T\",\"summary\":\"S\",\"content\":\"C\",\"categoryPath\":[\"X\"]}\n```",
        };
        var agent = new TrainingAgent(provider, NullLogger<TrainingAgent>.Instance);

        var memories = await agent.AnalyzeAsync("v.mp4", "t", []);

        var knowledge = Assert.Single(memories);
        Assert.Equal("T", knowledge.Title);
        Assert.Equal(["X"], knowledge.CategoryPath);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenNoContent_Throws()
    {
        var provider = new FakeLlmProvider { Reply = "{ \"memories\": [ { \"title\": \"vuoto\" } ] }" };
        var agent = new TrainingAgent(provider, NullLogger<TrainingAgent>.Instance);

        await Assert.ThrowsAsync<Ruki.Core.Llm.LlmException>(() => agent.AnalyzeAsync("v.mp4", "t", []));
    }
}
