using System.Security.Claims;
using Assistech.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace Assistech.Web.Services;

/// <summary>
/// Traduz o <see cref="SessaoState"/> para o modelo de autorizacao do Blazor.
/// Nenhum token trafega para o navegador.
/// </summary>
public sealed class AppAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly SessaoState _sessao;

    public AppAuthenticationStateProvider(SessaoState sessao)
    {
        _sessao = sessao;
        _sessao.Alterada += Notificar;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var atual = _sessao.Atual;

        if (atual is null)
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var identidade = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, atual.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, atual.Nome),
            new Claim(ClaimTypes.Email, atual.Email),
            new Claim(ClaimTypes.Role, atual.Perfil)
        ], "assistech");

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identidade)));
    }

    private void Notificar() => NotifyAuthenticationStateChanged(Task.FromResult(GetAuthenticationStateAsync().Result));
}
