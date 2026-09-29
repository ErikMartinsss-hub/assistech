using Assistech.Shared.Dtos;

namespace Assistech.Web.Services;

/// <summary>
/// Sessao visivel na UI (nome, empresa, cor). A fonte da verdade e o cookie:
/// no prerender o valor sai do HttpContext e, no circuito, do estado de
/// autenticacao que o framework entrega ao abrir o WebSocket.
/// </summary>
public sealed class SessaoState
{
    private readonly IHttpContextAccessor? _acesso;
    private SessaoDto? _atual;
    private bool _consultouHttp;

    public SessaoState(IHttpContextAccessor? acesso = null) => _acesso = acesso;

    public SessaoDto? Atual
    {
        get
        {
            if (_atual is null && !_consultouHttp)
            {
                _consultouHttp = true;
                _atual = SessaoCookie.LerPrincipal(_acesso?.HttpContext?.User);
            }

            return _atual;
        }
    }

    public bool Autenticado => Atual is not null;

    public event Action? Alterada;

    public void Entrar(SessaoDto sessao)
    {
        _atual = sessao;
        _consultouHttp = true;
        Alterada?.Invoke();
    }

    public void Sair()
    {
        _atual = null;
        _consultouHttp = true;
        Alterada?.Invoke();
    }

    /// <summary>Sincroniza com o principal do cookie sem disparar navegacao.</summary>
    public void Sincronizar(SessaoDto? sessao)
    {
        _atual = sessao;
        _consultouHttp = true;
        Alterada?.Invoke();
    }
}
