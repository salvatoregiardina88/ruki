using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;

namespace Ruki.Infrastructure.Update;

/// <summary>
/// Verifica gli aggiornamenti tramite le Release di GitHub: interroga l'API pubblica
/// <c>/repos/{owner}/{repo}/releases/latest</c> e confronta il tag della release con la versione
/// installata. Nessuna chiave o autenticazione richiesta.
/// <para>
/// Finché il repository non è configurato (<see cref="RepositorySlug"/> vuoto) la verifica è
/// disattivata e restituisce sempre <c>null</c>, senza alcuna chiamata di rete.
/// </para>
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    // Repository GitHub delle release (owner/repo). Vuoto = verifica aggiornamenti disattivata.
    public const string RepositorySlug = "salvatoregiardina88/ruki";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GitHubUpdateChecker> _logger;

    public GitHubUpdateChecker(IHttpClientFactory httpFactory, ILogger<GitHubUpdateChecker> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RepositorySlug))
            return null;   // repository non ancora configurato: verifica disattivata

        try
        {
            var http = _httpFactory.CreateClient("github");

            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"https://api.github.com/repos/{RepositorySlug}/releases/latest");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Ruki", CurrentVersion().ToString()));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Verifica aggiornamenti: GitHub ha risposto {Status}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = JsonSerializer.Deserialize<GitHubRelease>(body, Json);
            if (release?.TagName is null || release.HtmlUrl is null)
                return null;

            if (!IsNewerVersion(release.TagName, CurrentVersion()))
                return null;

            _logger.LogInformation("Aggiornamento disponibile: {Tag}.", release.TagName);
            return new UpdateInfo(NormalizeTag(release.TagName), release.HtmlUrl);
        }
        catch (Exception ex)
        {
            // Rete assente, timeout, JSON inatteso… la verifica è silenziosa: non disturbiamo l'utente.
            _logger.LogInformation(ex, "Verifica aggiornamenti non riuscita (ignorata).");
            return null;
        }
    }

    /// <summary>Versione attualmente installata (dall'eseguibile dell'app).</summary>
    private static Version CurrentVersion()
        => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// True se il tag della release (es. "v0.2.0") rappresenta una versione più recente di quella
    /// installata. Tollerante: ignora una eventuale "v" iniziale e i tag non interpretabili.
    /// </summary>
    public static bool IsNewerVersion(string tag, Version current)
        => Version.TryParse(NormalizeTag(tag), out var released) && released > current;

    private static string NormalizeTag(string tag)
        => tag.TrimStart('v', 'V').Trim();

    // DTO parziale della risposta dell'API GitHub (solo i campi che ci servono).
    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
