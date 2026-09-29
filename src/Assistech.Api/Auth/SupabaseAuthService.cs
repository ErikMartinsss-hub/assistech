using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Assistech.Data;
using Microsoft.Extensions.Options;

namespace Assistech.Api.Auth;

public sealed record SupabaseUsuario(Guid Id, string Email, string? Nome);

public sealed record SupabaseSessao(string AccessToken, int ExpiresIn);

/// <summary>
/// Cliente da API de autenticacao do Supabase (GoTrue).
/// A API do Assistech nunca expoe a service_role: apenas valida tokens.
/// </summary>
public sealed class SupabaseAuthService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseAuthService> _log;

    public SupabaseAuthService(HttpClient http, IOptions<SupabaseOptions> options, ILogger<SupabaseAuthService> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;

        _http.BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Add("apikey", _options.AnonKey);
    }

    public async Task<SupabaseSessao?> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        var url = "auth/v1/token?grant_type=password";
        using var resposta = await _http.PostAsJsonAsync(url, new { email, password = senha }, Json, ct).ConfigureAwait(false);

        if (!resposta.IsSuccessStatusCode) return null;

        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaToken>(Json, ct).ConfigureAwait(false);
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.AccessToken)) return null;

        return new SupabaseSessao(corpo.AccessToken, corpo.ExpiresIn);
    }

    public async Task<SupabaseUsuario?> ObterUsuarioAsync(string token, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "auth/v1/user");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resposta = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        if (!resposta.IsSuccessStatusCode)
        {
            _log.LogWarning("Supabase Auth respondeu {Status} ao validar token.", (int)resposta.StatusCode);
            return null;
        }

        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaUsuario>(Json, ct).ConfigureAwait(false);
        if (corpo?.Id is null || string.IsNullOrWhiteSpace(corpo.Id)) return null;

        var nome = corpo.UserMetadata?.GetValueOrProperty("full_name")
                   ?? corpo.UserMetadata?.GetValueOrProperty("name")
                   ?? string.Empty;

        return new SupabaseUsuario(Guid.Parse(corpo.Id), corpo.Email ?? string.Empty, nome);
    }

    public async Task EncerrarAsync(string token, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "auth/v1/logout");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var resposta = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* logout local ja foi feito */ }
    }

    private sealed class RespostaToken
    {
        public string? AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }

    private sealed class RespostaUsuario
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public Dictionary<string, string>? UserMetadata { get; set; }
    }
}

internal static class MetadataExtensions
{
    public static string? GetValueOrProperty(this Dictionary<string, string> metadata, string chave) =>
        metadata.TryGetValue(chave, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor : null;
}
