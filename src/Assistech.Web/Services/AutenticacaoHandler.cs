using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Assistech.Web.Services;

/// <summary>
/// Esquema de autenticamento vazio: a sessao do Blazor e mantida por
/// <see cref="SessaoState"/> e<AppAuthenticationStateProvider />, entao o desafio do
/// middleware apenas devolve a pagina para que o <c>NotAuthorized</c> redirecione ao login.
/// </summary>
public sealed class AutenticacaoHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "Assistech";

    public AutenticacaoHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> opcoes,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(opcoes, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // A sessao vive em SessaoState, entao o desafio nao precisa cortar a
        // resposta: quem chamador decide o status. Logar ajuda a diagnosticar
        // porque o framework nao emite nenhum log proprio nesse caminho.
        Logger.LogDebug("Desafio em {Path} (endpoint: {Endpoint})", Request.Path, Response.HttpContext.GetEndpoint()?.DisplayName);
        return Task.CompletedTask;
    }
}
