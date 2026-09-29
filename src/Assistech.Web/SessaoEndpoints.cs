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
        var grupo = rotas.MapGroup("/sessao");

        grupo.MapPost("/entrar", async (SessaoDto sessao, HttpContext contexto) =>
        {
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
            await contexto.SignOutAsync(SessaoCookie.Esquema);
            return Results.Ok();
        });
    }
}
