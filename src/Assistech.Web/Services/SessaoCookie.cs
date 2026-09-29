using System.Globalization;
using System.Security.Claims;
using Assistech.Shared.Dtos;
using Microsoft.AspNetCore.Authentication;

namespace Assistech.Web.Services;

/// <summary>
/// Sessao por cookie: o token do Supabase fica em um cookie criptografado
/// (DataProtection), HttpOnly, fora do alcance do JavaScript. E o que permite
/// ao prerender estatico saber que existe usuario logado - antes a sessao
/// vivia so na memoria do circuito e qualquer F5 deslogava.
/// </summary>
public static class SessaoCookie
{
    public const string Esquema = "Assistech";
    public const string Nome = "assistech_sessao";

    public const string ClaimToken = "assistech:token";
    public const string ClaimUsuarioId = "assistech:usuario";
    public const string ClaimEmpresaId = "assistech:empresa";
    public const string ClaimEmpresaNome = "assistech:empresa_nome";
    public const string ClaimEmpresaCor = "assistech:empresa_cor";
    public const string ClaimPerfil = "assistech:perfil";
    public const string ClaimExpiraEm = "assistech:expira";

    public static AuthenticationTicket CriarTicket(SessaoDto sessao)
    {
        var identidade = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, sessao.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, sessao.Nome),
            new Claim(ClaimTypes.Email, sessao.Email),
            new Claim(ClaimTypes.Role, sessao.Perfil),
            new Claim(ClaimToken, sessao.Token),
            new Claim(ClaimUsuarioId, sessao.UsuarioId.ToString()),
            new Claim(ClaimEmpresaId, sessao.EmpresaId.ToString()),
            new Claim(ClaimEmpresaNome, sessao.EmpresaNome),
            new Claim(ClaimEmpresaCor, sessao.EmpresaCorPrimaria),
            new Claim(ClaimPerfil, sessao.Perfil),
            new Claim(ClaimExpiraEm, sessao.ExpiraEm.ToString("O", CultureInfo.InvariantCulture))
        ], Esquema);

        return new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema);
    }

    public static SessaoDto? LerPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return null;
        if (principal.FindFirst(ClaimToken)?.Value is not { Length: > 0 } token) return null;

        return new SessaoDto
        {
            Token = token,
            UsuarioId = Guid.TryParse(principal.FindFirst(ClaimUsuarioId)?.Value, out var usuario) ? usuario : Guid.Empty,
            Nome = principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
            Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Perfil = principal.FindFirst(ClaimPerfil)?.Value ?? "tecnico",
            EmpresaId = Guid.TryParse(principal.FindFirst(ClaimEmpresaId)?.Value, out var empresa) ? empresa : Guid.Empty,
            EmpresaNome = principal.FindFirst(ClaimEmpresaNome)?.Value ?? string.Empty,
            EmpresaCorPrimaria = principal.FindFirst(ClaimEmpresaCor)?.Value ?? "#0d6efd",
            ExpiraEm = DateTimeOffset.TryParse(principal.FindFirst(ClaimExpiraEm)?.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expira)
                ? expira
                : DateTimeOffset.UtcNow.AddHours(1)
        };
    }

    /// <summary>Vida do cookie: acompanha o token, com piso e teto de seguranca.</summary>
    public static DateTimeOffset Expiracao(SessaoDto sessao)
    {
        var minimo = DateTimeOffset.UtcNow.AddMinutes(5);
        var maximo = DateTimeOffset.UtcNow.AddHours(12);
        if (sessao.ExpiraEm < minimo) return minimo;
        return sessao.ExpiraEm > maximo ? maximo : sessao.ExpiraEm;
    }
}
