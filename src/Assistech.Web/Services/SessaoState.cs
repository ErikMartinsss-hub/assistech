using Assistech.Shared.Client;
using Assistech.Shared.Dtos;

namespace Assistech.Web.Services;

/// <summary>
/// Mantem a sessao do usuario logado durante a navegacao (Blazor Server).
/// O token vive apenas no servidor: nada e exposto ao navegador alem do circuito.
/// </summary>
public sealed class SessaoState
{
    public SessaoDto? Atual { get; private set; }
    public bool Autenticado => Atual is not null;
    public event Action? Alterada;

    public void Entrar(SessaoDto sessao)
    {
        Atual = sessao;
        Alterada?.Invoke();
    }

    public void Sair()
    {
        Atual = null;
        Alterada?.Invoke();
    }

    /// <summary>Reaplica a sessao no cliente HTTP da requisicao (a cada circuit).</summary>
    public void Reaplicar(IAssistechApi api)
    {
        if (Atual is { } sessao && api is HttpAssistechApi http)
            http.RestaurarSessao(sessao);
    }
}
