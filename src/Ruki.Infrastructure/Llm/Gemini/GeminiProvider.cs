using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Configuration;
using Ruki.Core.Llm;
using Ruki.Core.Localization;

namespace Ruki.Infrastructure.Llm.Gemini;

/// <summary>
/// Implementazione di <see cref="ILlmProvider"/> per l'API Google Gemini
/// (<c>generativelanguage.googleapis.com</c>, metodo <c>generateContent</c>).
/// <para>
/// Legge la chiave API dal secret store e il nome del modello dalle impostazioni a ogni
/// chiamata, così cambiarli dalla UI ha effetto immediato senza riavviare.
/// </para>
/// </summary>
public sealed class GeminiProvider : ILlmProvider
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string FilesBaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const string FilesUploadUrl = "https://generativelanguage.googleapis.com/upload/v1beta/files";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ISecretStore _secrets;
    private readonly ISettingsService _settings;
    private readonly IUsageTracker _usage;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(
        IHttpClientFactory httpFactory,
        ISecretStore secrets,
        ISettingsService settings,
        IUsageTracker usage,
        ILogger<GeminiProvider> logger)
    {
        _httpFactory = httpFactory;
        _secrets = secrets;
        _settings = settings;
        _usage = usage;
        _logger = logger;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = RequireApiKey();
        var model = _settings.Current.GeminiModel;
        var url = $"{BaseUrl}/{Uri.EscapeDataString(model)}:generateContent";
        var payloadJson = JsonSerializer.Serialize(BuildPayload(request), Json);

        // La richiesta viene ricreata a ogni tentativo (un HttpRequestMessage non è riutilizzabile).
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await SendWithRetryAsync(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
                };
                req.Headers.Add("x-goog-api-key", apiKey);
                return req;
            }, cancellationToken);
        }
        catch (Exception ex) when (IsTransientException(ex, cancellationToken))
        {
            // Tutti i tentativi falliti per rete/timeout: messaggio chiaro invece di un'eccezione grezza.
            throw new LlmException(Localizer.T("Llm_NetworkError"), ex);
        }

        using (httpResponse)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini ha risposto {Status}. Corpo: {Body}", (int)httpResponse.StatusCode, Truncate(body, 800));
                var detail = TryExtractApiError(body) ?? httpResponse.ReasonPhrase;
                throw new LlmException(Localizer.T("Llm_ApiError", (int)httpResponse.StatusCode, detail));
            }

            return ParseResponse(body);
        }
    }

    public async Task<LlmFile> UploadFileAsync(string filePath, string mimeType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new LlmException($"File to upload not found: {filePath}.");

        var apiKey = RequireApiKey();
        var http = _httpFactory.CreateClient("gemini");
        var fileLength = new FileInfo(filePath).Length;

        // 1. Avvia l'upload "resumable" e ottieni l'URL su cui inviare i byte.
        using var startRequest = new HttpRequestMessage(HttpMethod.Post, FilesUploadUrl);
        startRequest.Headers.Add("x-goog-api-key", apiKey);
        startRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        startRequest.Headers.Add("X-Goog-Upload-Command", "start");
        startRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", fileLength.ToString());
        startRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
        startRequest.Content = new StringContent(
            JsonSerializer.Serialize(new { file = new { displayName = Path.GetFileName(filePath) } }, Json),
            Encoding.UTF8, "application/json");

        using var startResponse = await http.SendAsync(startRequest, cancellationToken);
        if (!startResponse.IsSuccessStatusCode)
            throw new LlmException($"Failed to start the file upload ({(int)startResponse.StatusCode}).");

        var uploadUrl = startResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var values)
            ? values.FirstOrDefault()
            : null;
        if (string.IsNullOrEmpty(uploadUrl))
            throw new LlmException("Upload URL not returned by Gemini.");

        // 2. Invia i byte e finalizza, leggendo dal file in streaming (niente caricamento in RAM).
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        await using var fileStream = File.OpenRead(filePath);
        uploadRequest.Content = new StreamContent(fileStream);

        using var uploadResponse = await http.SendAsync(uploadRequest, cancellationToken);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!uploadResponse.IsSuccessStatusCode)
            throw new LlmException($"File upload failed ({(int)uploadResponse.StatusCode}): {Truncate(uploadBody, 300)}");

        var file = JsonSerializer.Deserialize<GeminiFileEnvelope>(uploadBody, Json)?.File
            ?? throw new LlmException("Invalid upload response.");

        // 3. Attendi che il file sia elaborato: i video passano da PROCESSING ad ACTIVE.
        var active = await WaitUntilActiveAsync(http, apiKey, file, cancellationToken);
        _logger.LogInformation("File caricato su Gemini: {Uri}.", active.Uri);
        return new LlmFile(active.Uri!, mimeType);
    }

    public async Task ValidateKeyAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = RequireApiKey();
        var http = _httpFactory.CreateClient("gemini");

        // Elenco modelli (GET): conferma che la chiave è valida senza consumare quota di generazione.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}?pageSize=1");
        request.Headers.Add("x-goog-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (IsTransientException(ex, cancellationToken))
        {
            throw new LlmException(Localizer.T("Llm_NetworkError"), ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = TryExtractApiError(body) ?? response.ReasonPhrase;
            throw new LlmException(Localizer.T("Llm_InvalidKey", (int)response.StatusCode, detail));
        }
    }

    private string RequireApiKey()
    {
        var apiKey = _secrets.Get(SecretKeys.GeminiApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new LlmException(Localizer.T("Llm_NoApiKey"));
        return apiKey;
    }

    // Numero massimo di tentativi complessivi per gli errori "transitori" (rete/timeout, 429, 5xx).
    private const int MaxAttempts = 3;

    /// <summary>
    /// Invia una richiesta HTTP con retry sugli errori transitori (problemi di rete, timeout, 429 e
    /// 5xx), con attesa esponenziale tra i tentativi. La richiesta va ricreata a ogni tentativo
    /// perché un <see cref="HttpRequestMessage"/> non è riutilizzabile dopo l'invio.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        var http = _httpFactory.CreateClient("gemini");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = createRequest();
                var response = await http.SendAsync(request, cancellationToken);

                if (attempt < MaxAttempts && IsTransientStatus((int)response.StatusCode))
                {
                    _logger.LogWarning("Gemini ha risposto {Status}: nuovo tentativo {Attempt}/{Max}.",
                        (int)response.StatusCode, attempt, MaxAttempts);
                    response.Dispose();
                    await BackoffAsync(attempt, cancellationToken);
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransientException(ex, cancellationToken))
            {
                _logger.LogWarning(ex, "Errore di rete verso Gemini: nuovo tentativo {Attempt}/{Max}.", attempt, MaxAttempts);
                await BackoffAsync(attempt, cancellationToken);
            }
        }
    }

    private static bool IsTransientStatus(int status)
        => status is 429 or 500 or 502 or 503 or 504;

    /// <summary>Transitorio: errore di rete, oppure timeout dell'HttpClient (non un annullamento dell'utente).</summary>
    private static bool IsTransientException(Exception ex, CancellationToken cancellationToken)
        => ex is HttpRequestException
           || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static Task BackoffAsync(int attempt, CancellationToken cancellationToken)
    {
        // Attesa esponenziale con un po' di jitter: ~0,5s, 1s, 2s…
        var seconds = 0.5 * Math.Pow(2, attempt - 1) + Random.Shared.NextDouble() * 0.25;
        return Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }

    private async Task<GeminiFileResource> WaitUntilActiveAsync(
        HttpClient http, string apiKey, GeminiFileResource file, CancellationToken cancellationToken)
    {
        var current = file;
        var deadline = DateTime.UtcNow.AddMinutes(5);

        while (string.Equals(current.State, "PROCESSING", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTime.UtcNow > deadline)
                throw new LlmException("The uploaded file did not become available in time.");

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"{FilesBaseUrl}/{current.Name}");
            statusRequest.Headers.Add("x-goog-api-key", apiKey);
            using var statusResponse = await http.SendAsync(statusRequest, cancellationToken);
            var statusBody = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
            current = JsonSerializer.Deserialize<GeminiFileResource>(statusBody, Json) ?? current;
        }

        if (!string.Equals(current.State, "ACTIVE", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(current.Uri))
            throw new LlmException($"The uploaded file is not usable (state: {current.State}).");

        return current;
    }

    /// <summary>Converte la nostra <see cref="LlmRequest"/> nel corpo JSON atteso da Gemini.</summary>
    private static GeminiRequest BuildPayload(LlmRequest request)
    {
        var contents = request.Messages
            .Select(m => new GeminiContent(
                Role: m.Role == ChatRole.User ? "user" : "model",
                Parts: [new GeminiPart(m.Text)]))
            .ToList();

        // Eventuali file (video) e immagini inline (screenshot) vengono allegati all'ultimo turno utente.
        if (request.Files is { Count: > 0 } || request.Images is { Count: > 0 })
        {
            var target = contents.LastOrDefault(c => c.Role == "user");
            if (target is null)
            {
                target = new GeminiContent("user", []);
                contents.Add(target);
            }

            if (request.Files is not null)
                foreach (var file in request.Files)
                    target.Parts.Add(new GeminiPart(FileData: new GeminiFileData(file.MimeType, file.Uri)));

            if (request.Images is not null)
                foreach (var image in request.Images)
                    target.Parts.Add(new GeminiPart(InlineData: new GeminiInlineData(image.MimeType, Convert.ToBase64String(image.Data))));
        }

        var systemInstruction = string.IsNullOrWhiteSpace(request.SystemInstruction)
            ? null
            : new GeminiSystemInstruction([new GeminiPart(request.SystemInstruction)]);

        var generationConfig = request.Temperature is null
            ? null
            : new GeminiGenerationConfig(request.Temperature);

        return new GeminiRequest(systemInstruction, contents, generationConfig);
    }

    /// <summary>Estrae il testo dalla risposta di Gemini, traducendo i casi di errore in <see cref="LlmException"/>.</summary>
    private LlmResponse ParseResponse(string body)
    {
        GeminiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiResponse>(body, Json);
        }
        catch (JsonException ex)
        {
            throw new LlmException(Localizer.T("Llm_Unparseable"), ex);
        }

        RecordUsage(parsed?.UsageMetadata);

        var candidate = parsed?.Candidates?.FirstOrDefault();

        // Concateniamo il testo di tutte le parti, saltando quelle di "ragionamento" (thought):
        // i modelli reasoning di Gemini possono restituire parti di pensiero senza testo utile.
        var text = candidate?.Content?.Parts is { } parts
            ? string.Concat(parts.Where(p => p.Thought != true).Select(p => p.Text))
            : null;

        if (!string.IsNullOrEmpty(text))
            return new LlmResponse(text);

        // Nessun testo: bloccato dai filtri di sicurezza, troncato, oppure davvero vuoto.
        var blockReason = parsed?.PromptFeedback?.BlockReason;
        if (blockReason is not null)
            throw new LlmException(Localizer.T("Llm_Blocked", blockReason));

        var finishReason = candidate?.FinishReason;
        throw new LlmException(finishReason is not null
            ? Localizer.T("Llm_NoText", finishReason)
            : Localizer.T("Llm_EmptyResponse"));
    }

    /// <summary>Accumula i token consumati (input = inviati, output = ricevuti, incl. ragionamento).</summary>
    private void RecordUsage(GeminiUsageMetadata? usage)
    {
        if (usage is null)
            return;

        var input = usage.PromptTokenCount ?? 0;
        var output = (usage.CandidatesTokenCount ?? 0) + (usage.ThoughtsTokenCount ?? 0);
        _usage.Record(input, output);
    }

    /// <summary>Prova a leggere il messaggio d'errore strutturato dell'API Gemini.</summary>
    private static string? TryExtractApiError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<GeminiErrorEnvelope>(body, Json)?.Error?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    // ---------------------------------------------------------------------------------------
    // DTO di (de)serializzazione per l'API Gemini. Interni: dettaglio d'implementazione.
    // I nomi delle proprietà JSON (camelCase) sono gestiti dalle JsonSerializerOptions sopra.
    // ---------------------------------------------------------------------------------------

    private sealed record GeminiRequest(
        GeminiSystemInstruction? SystemInstruction,
        List<GeminiContent> Contents,
        GeminiGenerationConfig? GenerationConfig);

    private sealed record GeminiSystemInstruction(List<GeminiPart> Parts);

    private sealed record GeminiContent(string Role, List<GeminiPart> Parts);

    private sealed record GeminiPart(
        string? Text = null,
        bool? Thought = null,
        GeminiFileData? FileData = null,
        GeminiInlineData? InlineData = null);

    private sealed record GeminiFileData(string MimeType, string FileUri);

    private sealed record GeminiInlineData(string MimeType, string Data);

    private sealed record GeminiGenerationConfig(double? Temperature);

    private sealed record GeminiResponse(
        List<GeminiCandidate>? Candidates,
        GeminiPromptFeedback? PromptFeedback,
        GeminiUsageMetadata? UsageMetadata);

    private sealed record GeminiUsageMetadata(
        int? PromptTokenCount, int? CandidatesTokenCount, int? TotalTokenCount, int? ThoughtsTokenCount);

    private sealed record GeminiCandidate(GeminiContent? Content, string? FinishReason);

    private sealed record GeminiPromptFeedback(string? BlockReason);

    private sealed record GeminiErrorEnvelope(GeminiErrorDetail? Error);

    private sealed record GeminiErrorDetail(int? Code, string? Message, string? Status);

    private sealed record GeminiFileEnvelope(GeminiFileResource? File);

    private sealed record GeminiFileResource(string? Name, string? Uri, string? State, string? MimeType);
}
