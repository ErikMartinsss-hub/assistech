using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Assistech.Data;
using Assistech.Shared.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Assistech.Api.Auth;

public sealed class SupabaseAuthOptions : AuthenticationSchemeOptions
{
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Valida o token do Supabase Auth, resolve o vinculo usuario -&gt; empresa
/// e popula o <see cref="SessaoAtual"/> da requisicao.
/// </summary>
public sealed class SupabaseBearerHandler : AuthenticationHandler<SupabaseAuthOptions>
{
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new();

    private readonly SupabaseAuthService _supabase;
    private readonly IUsuarioRepository _usuarios;
    private readonly IEmpresaRepository _empresas;
    private readonly SessaoAtual _sessaoAtual;
    private readonly ILogger<SupabaseBearerHandler> _log;

    private sealed record CacheEntry(DateTimeOffset ExpiraEm, ClaimsPrincipal Principal);

    public SupabaseBearerHandler(
        IOptionsMonitor<SupabaseAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SupabaseAuthService supabase,
        IUsuarioRepository usuarios,
        IEmpresaRepository empresas,
        SessaoAtual sessaoAtual)
        : base(options, logger, encoder)
    {
        _supabase = supabase;
        _usuarios = usuarios;
        _empresas = empresas;
        _sessaoAtual = sessaoAtual;
        _log = logger.CreateLogger<SupabaseBearerHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtrairToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return AuthenticateResult.NoResult();

        var agora = DateTimeOffset.UtcNow;
        if (Cache.TryGetValue(token, out var entrada) && entrada.ExpiraEm > agora)
        {
            return AuthenticateResult.Success(new AuthenticationTicket(entrada.Principal, Scheme.Name));
        }

        var usuario = await _supabase.ObterUsuarioAsync(token, Context.RequestAborted);
        if (usuario is null) return AuthenticateResult.Fail("Token invalido ou expirado.");

        var vinculo = await _usuarios.ObterPorUsuarioAuthAsync(usuario.Id, Context.RequestAborted)
                      ?? await ProvisionarAsync(usuario);

        if (vinculo is null)
        {
            return AuthenticateResult.Fail(
                "Usuario autenticado, porem sem vinculo com uma loja. Execute o passo de vinculo descrito no README.");
        }

        var empresa = await _empresas.ObterAsync(vinculo.EmpresaId, Context.RequestAborted);

        _sessaoAtual.EmpresaId = vinculo.EmpresaId;
        _sessaoAtual.UsuarioId = vinculo.UsuarioId;
        _sessaoAtual.Nome = vinculo.Nome;
        _sessaoAtual.Email = vinculo.Email;
        _sessaoAtual.Perfil = vinculo.Perfil;
        _sessaoAtual.EmpresaNome = empresa?.Nome ?? string.Empty;
        _sessaoAtual.EmpresaCorPrimaria = empresa?.CorPrimaria ?? "#0d6efd";

        var identidade = new ClaimsIdentity(CriarClaims(vinculo), Scheme.Name);
        var principal = new ClaimsPrincipal(identidade);

        Cache[token] = new CacheEntry(agora.Add(Options.CacheDuration), principal);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json; charset=utf-8";
        return Response.WriteAsJsonAsync(new { erro = "Sessao expirada. Entre novamente." });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json; charset=utf-8";
        return Response.WriteAsJsonAsync(new { erro = "Voce nao tem permissao para esta acao." });
    }

    private static string? ExtrairToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)) return null;

        const string prefixo = "Bearer ";
        return header.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
            ? header[prefixo.Length..].Trim()
            : null;
    }

    private static Claim[] CriarClaims(VinculoUsuario vinculo) =>
    [
        new(ClaimTypes.NameIdentifier, vinculo.UsuarioId.ToString()),
        new(ClaimTypes.Name, vinculo.Nome),
        new(ClaimTypes.Email, vinculo.Email),
        new(ClaimTypes.Role, vinculo.Perfil.ToString()),
        new("empresa_id", vinculo.EmpresaId.ToString())
    ];

    /// <summary>
    /// Primeiro acesso: se existir apenas uma loja, o usuario passa a pertencer a ela.
    /// Com varias lojas, o vinculo precisa ser feito pelo administrador (ver README).
    /// </summary>
    private async Task<VinculoUsuario?> ProvisionarAsync(SupabaseUsuario usuario)
    {
        var empresas = await _empresas.ListarAsync(Context.RequestAborted);
        if (empresas.Count != 1)
        {
            _log.LogWarning("Usuario {Email} sem vinculo e com {Total} lojas cadastradas.", usuario.Email, empresas.Count);
            return null;
        }

        var nome = string.IsNullOrWhiteSpace(usuario.Nome) ? usuario.Email : usuario.Nome;
        _log.LogInformation("Vinculando {Email} a loja {Empresa} (primeiro acesso).", usuario.Email, empresas[0]);

        return await _usuarios.CriarVinculoAsync(empresas[0].Id, usuario.Id, nome, usuario.Email, PerfilUsuario.Admin, Context.RequestAborted);
    }
}
