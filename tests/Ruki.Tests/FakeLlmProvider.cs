using Ruki.Core.Llm;

namespace Ruki.Tests;

/// <summary>
/// Provider LLM finto per i test: restituisce una risposta fissa (o lancia), registra l'ultima
/// richiesta e i file "caricati". Nessuna chiamata di rete.
/// </summary>
internal sealed class FakeLlmProvider : ILlmProvider
{
    public string Reply = "ok";
    public Exception? Throw;
    public LlmRequest? LastRequest;
    public List<string> UploadedFiles { get; } = [];

    /// <summary>Se valorizzata, ogni chiamata consuma la prossima risposta in coda (per i loop).</summary>
    public Queue<string>? Replies;

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        if (Throw is not null)
            return Task.FromException<LlmResponse>(Throw);

        var text = Replies is { Count: > 0 } ? Replies.Dequeue() : Reply;
        return Task.FromResult(new LlmResponse(text));
    }

    public Task<LlmFile> UploadFileAsync(string filePath, string mimeType, CancellationToken cancellationToken = default)
    {
        UploadedFiles.Add(filePath);
        return Task.FromResult(new LlmFile($"files/fake-{Path.GetFileName(filePath)}", mimeType));
    }

    public Task ValidateKeyAsync(CancellationToken cancellationToken = default)
        => Throw is not null ? Task.FromException(Throw) : Task.CompletedTask;
}
