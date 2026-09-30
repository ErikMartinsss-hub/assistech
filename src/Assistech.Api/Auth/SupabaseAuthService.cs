using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assistech.Data;
using Microsoft.Extensions.Options;

namespace Assistech.Api.Auth;

public sealed record SupabaseUsuario(Guid Id, string Email, string? Nome);

public sealed record SupabaseSessao(string AccessToken, int ExpiresIn);

/// <summary>Por que o login foi negado. A tela mostra o motivo certo, nao um genérico.</summary>
public enum MotivoFalha
{
    Credenciais,
    ChaveInvalida,
    EmailNaoConfirmado,
    MuitasTentativas,
    Indisponivel
}

public sealed record ResultadoAutenticacao(SupabaseSessao? Sessao, MotivoFalha Motivo, string? Detalhe = null);

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

    public async Task<ResultadoAutenticacao> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        var url = "auth/v1/token?grant_type=password";

        HttpResponseMessage resposta;
        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new { email, password = senha }, options: Json)
            };

            resposta = await _http.SendAsync(requisicao, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException erro)
        {
            // Sem este log, uma falha de rede virava "senha invalida" na tela.
            _log.LogError(erro, "Falha de rede ao falar com o Supabase Auth ({Host}).", _options.Url);
            return new ResultadoAutenticacao(null, MotivoFalha.Indisponivel, "sem conexao com o Supabase");
        }

        using (resposta)
        {
            if (!resposta.IsSuccessStatusCode)
            {
                // A senha nunca entra no log; o corpo do GoTrue mostra o motivo.
                var corpo = await resposta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.LogWarning(
                    "Supabase Auth recusou o login de {Email}: {Status} {Corpo}",
                    email,
                    (int)resposta.StatusCode,
                    corpo.Length > 300 ? corpo[..300] : corpo);

                var falha = Classificar(resposta.StatusCode, corpo);
                return new ResultadoAutenticacao(null, falha.Motivo, falha.Detalhe);
            }

            var conteudo = await resposta.Content.ReadFromJsonAsync<RespostaToken>(Json, ct).ConfigureAwait(false);
            if (conteudo is null || string.IsNullOrWhiteSpace(conteudo.AccessToken))
            {
                // HTTP 200 sem token significa que o GoTrue respondeu outra coisa.
                // Sem registrar o corpo, isso vira um mistério sem pista.
                var corpo200 = await resposta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _log.LogWarning(
                    "Supabase Auth respondeu {Status} sem access_token para {Email}. Content-Type={Tipo}. Corpo={Corpo}",
                    (int)resposta.StatusCode,
                    email,
                    resposta.Content.Headers.ContentType?.ToString() ?? "(nenhum)",
                    corpo200.Length > 500 ? corpo200[..500] : corpo200);

                return new ResultadoAutenticacao(
                    null,
                    MotivoFalha.Indisponivel,
                    $"HTTP {(int)resposta.StatusCode} sem access_token, corpo: {Resumir(corpo200)}");
            }

            return new ResultadoAutenticacao(new SupabaseSessao(conteudo.AccessToken, conteudo.ExpiresIn), MotivoFalha.Credenciais);
        }
    }

    private static (MotivoFalha Motivo, string Detalhe) Classificar(HttpStatusCode status, string corpo)
    {
        // O status e o error_code do GoTrue sao a unica forma de saber o que houve de verdade.
        var codigo = ExtrairCodigoErro(corpo);
        var detalhe = $"HTTP {(int)status}{(codigo is null ? "" : $" {codigo}")}";

        if (status == HttpStatusCode.TooManyRequests) return (MotivoFalha.MuitasTentativas, detalhe);
        if (status == HttpStatusCode.Unauthorized) return (MotivoFalha.ChaveInvalida, detalhe);

        if (corpo.Contains("email_not_confirmed", StringComparison.OrdinalIgnoreCase)
            || corpo.Contains("Email not confirmed", StringComparison.OrdinalIgnoreCase))
            return (MotivoFalha.EmailNaoConfirmado, detalhe);

        if ((int)status >= 500) return (MotivoFalha.Indisponivel, detalhe);
        if (status == HttpStatusCode.BadRequest) return (MotivoFalha.Credenciais, detalhe);

        return (MotivoFalha.Indisponivel, detalhe);
    }

    /// <summary>Troca por um resumo curto e sem token: o corpo pode ser o proprio token.</summary>
    private static string Resumir(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo)) return "(vazio)";

        var limpo = System.Text.RegularExpressions.Regex.Replace(corpo, @"eyJ[A-Za-z0-9_\-]{16,}", "<jwt>");
        limpo = System.Text.RegularExpressions.Regex.Replace(limpo, @"\s+", " ").Trim();
        return limpo.Length > 160 ? $"[{limpo.Length} bytes] {limpo[..160]}" : limpo;
    }

    private static string? ExtrairCodigoErro(string corpo)    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            foreach (var campo in (ReadOnlySpan<string>)["error_code", "code", "msg"])
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty(campo, out var valor)
                    && valor.ValueKind == JsonValueKind.String)
                    return valor.GetString();
        }
        catch (JsonException)
        {
            // corpo nao-JSON: o status ainda identifica o problema
        }

        return null;
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

        var nome = corpo.UserMetadata?.Texto("full_name")
                   ?? corpo.UserMetadata?.Texto("name")
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
        // O GoTrue usa snake_case. Sem o atributo, o System.Text.Json procura "accessToken"
        // e devolve nulo: o login dava certo e a API jogava o token fora.
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class RespostaUsuario
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("user_metadata")]
        public Dictionary<string, JsonElement>? UserMetadata { get; set; }
    }
}

internal static class MetadataExtensions
{
    /// <summary>
    /// O metadata vem como objeto livre: um numero ou lista quebraria o login.
    /// So interessa o que for texto.
    /// </summary>
    public static string? Texto(this Dictionary<string, JsonElement> metadata, string chave) =>
        metadata.TryGetValue(chave, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}
