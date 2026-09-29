using System.Net.Http.Json;
using Assistech.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace Assistech.Web.Services;

/// <summary>
/// Assina/encerra a sessao no proprio servidor da web. A chamada sai do
/// circuito (que nao tem HttpContext) para o proprio host, com JSON e o header
/// X-Assistech-Sessao. O content-type application/json e o header obrigam o
/// navegador a um preflight de CORS, que este host nega - e o que barra um
/// site de terceiros de assinar a sessao por CSRF.
/// </summary>
public sealed class SessaoCookieCliente
{
    public const string Cliente = "assistech-propria";
    private const string Cabecalho = "X-Assistech-Sessao";

    private readonly IHttpClientFactory _fabrica;
    private readonly NavigationManager _navegacao;

    public SessaoCookieCliente(IHttpClientFactory fabrica, NavigationManager navegacao)
    {
        _fabrica = fabrica;
        _navegacao = navegacao;
    }

    public Task AssinarAsync(SessaoDto sessao, CancellationToken ct = default)
        => EnviarAsync("sessao/entrar", JsonContent.Create(sessao), ct);

    public Task SairAsync(CancellationToken ct = default)
        => EnviarAsync("sessao/sair", null, ct);

    private async Task EnviarAsync(string caminho, HttpContent? conteudo, CancellationToken ct)
    {
        var http = _fabrica.CreateClient(Cliente);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_navegacao.BaseUri), caminho))
        {
            Content = conteudo
        };
        requisicao.Headers.Add(Cabecalho, "1");

        using var resposta = await http.SendAsync(requisicao, ct);
        resposta.EnsureSuccessStatusCode();
    }
}
