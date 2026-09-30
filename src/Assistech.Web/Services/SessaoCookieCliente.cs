using Assistech.Shared.Dtos;
using Microsoft.JSInterop;

namespace Assistech.Web.Services;

/// <summary>
/// Assina/encerra a sessao chamando o proprio host A PARTIR DO NAVEGADOR.
/// O cookie so e gravado na resposta que chega ao navegador: um POST do servidor
/// para si mesmo descarta o Set-Cookie.
/// </summary>
public sealed class SessaoCookieCliente
{
    public const string Cliente = "assistech-propria";
    private readonly IJSRuntime _js;

    public SessaoCookieCliente(IJSRuntime js) => _js = js;

    public ValueTask AssinarAsync(SessaoDto sessao, CancellationToken ct = default)
        => _js.InvokeVoidAsync("assistech.sessao", "sessao/entrar", sessao);

    public ValueTask SairAsync(CancellationToken ct = default)
        => _js.InvokeVoidAsync("assistech.sessao", "sessao/sair", null);
}
