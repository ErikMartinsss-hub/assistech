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
        Response.StatusCode = StatusCodes.Status200OK;
        return Task.CompletedTask;
    }
}
