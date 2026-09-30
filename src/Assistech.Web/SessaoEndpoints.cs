using Assistech.Shared.Dtos;
using Assistech.Web.Services;
using Microsoft.AspNetCore.Authentication;

namespace Assistech.Web;

/// <summary>
/// Recebe a sessao vinda da tela de login e a grava no cookie criptografado.
/// Endpoints internos: nao ha CORS aberto para eles, portanto um site de
/// terceiros nao consegue disparar esses POSTs pelo navegador.
/// </summary>
public static class SessaoEndpoints
{
    public static void MapSessaoEndpoints(this IEndpointRouteBuilder rotas)
    {
        const string Cabecalho = "X-Assistech-Sessao";

        var grupo = rotas.MapGroup("/sessao");

        grupo.MapPost("/entrar", async (SessaoDto sessao, HttpContext contexto) =>
        {
            // O header so chega num fetch do navegador: um site de terceiros
            // precisaria de preflight de CORS, e este host nao responde preflight.
            if (!contexto.Request.Headers.ContainsKey(Cabecalho))
                return Results.BadRequest(new { erro = "Origem nao permitida." });

            if (string.IsNullOrWhiteSpace(sessao.Token))
                return Results.BadRequest(new { erro = "Sessao invalida." });

            await contexto.SignInAsync(
                SessaoCookie.Esquema,
                SessaoCookie.CriarTicket(sessao).Principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = SessaoCookie.Expiracao(sessao)
                });

            return Results.Ok();
        });

        grupo.MapPost("/sair", async (HttpContext contexto) =>
        {
            if (!contexto.Request.Headers.ContainsKey(Cabecalho))
                return Results.BadRequest(new { erro = "Origem nao permitida." });

            await contexto.SignOutAsync(SessaoCookie.Esquema);
            return Results.Ok();
        });
    }
}
