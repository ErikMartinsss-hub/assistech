using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Assistech.Web.Services;

/// <summary>
/// Decorador do provedor do servidor: mantem a sessao da UI em sincronia com
/// o cookie, que e a fonte da verdade. Sem isso, o AuthorizeRouteView usaria
/// apenas a memoria do circuito e um F5 deslogaria o usuario.
/// </summary>
public sealed class AppAuthenticationStateProvider : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider
{
    private readonly ServerAuthenticationStateProvider _interno;
    private readonly SessaoState _sessao;

    public AppAuthenticationStateProvider(ServerAuthenticationStateProvider interno, SessaoState sessao)
    {
        _interno = interno;
        _sessao = sessao;
    }

    /// <summary>
    /// No prerender o cookie esta no HttpContext e o provedor do servidor ainda
    /// nao foi inicializado - por isso a sessao tem prioridade sobre ele.
    /// </summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_sessao.Atual is { } sessao)
            return Task.FromResult(new AuthenticationState(SessaoCookie.CriarTicket(sessao).Principal));

        return _interno.GetAuthenticationStateAsync();
    }

    public void SetAuthenticationState(Task<AuthenticationState> tarefa)
    {
        _interno.SetAuthenticationState(tarefa);
        _ = SincronizarAsync(tarefa);
    }

    private async Task SincronizarAsync(Task<AuthenticationState> tarefa)
    {
        try
        {
            var sessao = SessaoCookie.LerPrincipal((await tarefa).User);

            // No prerender o framework entrega um principal anonimo. Tratar isso
            // como "saiu" apagava a sessao recem-lida do cookie e jogava o
            // usuario de volta para /entrar.
            if (sessao is null && _sessao.Atual is not null) return;
            if (sessao?.Token == _sessao.Atual?.Token) return;

            _sessao.Sincronizar(sessao);
        }
        catch (Exception)
        {
            // Sem principal: mantem a sessao atual.
        }
    }
}
