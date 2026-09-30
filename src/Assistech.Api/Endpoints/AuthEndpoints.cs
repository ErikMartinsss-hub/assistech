using Assistech.Api.Auth;
using Assistech.Api.Infrastructure;
using Assistech.Data;
using Assistech.Shared.Dtos;
using Assistech.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Assistech.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/auth").WithTags("Autenticacao");

        grupo.MapPost("/login", async (
            LoginRequest request,
            SupabaseAuthService supabase,
            IUsuarioRepository usuarios,
            IEmpresaRepository empresas,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
                return Validacao.Falha("Informe e-mail e senha.");

            var autenticacao = await supabase.AutenticarAsync(request.Email.Trim(), request.Senha, ct);
            if (autenticacao.Sessao is not { } sessao)
            {
                var (erro, status) = autenticacao.Motivo switch
                {
                    MotivoFalha.ChaveInvalida => ("Configuracao invalida: a chave do Supabase foi recusada.", StatusCodes.Status502BadGateway),
                    MotivoFalha.EmailNaoConfirmado => ("Confirme o e-mail desta conta antes de entrar.", StatusCodes.Status403Forbidden),
                    MotivoFalha.MuitasTentativas => ("Muitas tentativas. Aguarde alguns minutos e tente de novo.", StatusCodes.Status429TooManyRequests),
                    // So neste caso o detalhe entra na tela: e o unico motivo que nao se explica sozinho.
                    MotivoFalha.Indisponivel => ("O servico de login esta indisponivel. Tente novamente."
                        + (autenticacao.Detalhe is null ? "" : $" ({autenticacao.Detalhe})"), StatusCodes.Status502BadGateway),
                    _ => ("E-mail ou senha invalidos.", StatusCodes.Status401Unauthorized)
                };

                return Results.Json(new { erro }, statusCode: status);
            }

            var usuario = await supabase.ObterUsuarioAsync(sessao.AccessToken, ct);
            if (usuario is null)
                return Results.Json(new { erro = "Sessao nao pode ser lida." }, statusCode: StatusCodes.Status401Unauthorized);

            var vinculo = await usuarios.ObterPorUsuarioAuthAsync(usuario.Id, ct);

            // Primeiro acesso: havendo uma unica loja cadastrada, o usuario entra como admin.
            if (vinculo is null)
            {
                var lojas = await empresas.ListarAsync(ct);
                if (lojas.Count == 1)
                {
                    var nome = string.IsNullOrWhiteSpace(usuario.Nome) ? usuario.Email : usuario.Nome;
                    vinculo = await usuarios.CriarVinculoAsync(lojas[0].Id, usuario.Id, nome, usuario.Email, PerfilUsuario.Admin, ct);
                }
            }

            if (vinculo is null)
                return Results.Json(
                    new { erro = "Seu usuario ainda nao esta vinculado a uma loja. Fale com o administrador." },
                    statusCode: StatusCodes.Status403Forbidden);

            var empresa = await empresas.ObterAsync(vinculo.EmpresaId, ct);

            return Results.Ok(new SessaoDto
            {
                Token = sessao.AccessToken,
                ExpiraEm = DateTimeOffset.UtcNow.AddSeconds(sessao.ExpiresIn),
                UsuarioId = vinculo.UsuarioId,
                Nome = vinculo.Nome,
                Email = vinculo.Email,
                Perfil = vinculo.Perfil.ToString().ToLowerInvariant(),
                EmpresaId = vinculo.EmpresaId,
                EmpresaNome = empresa?.Nome ?? string.Empty,
                EmpresaCorPrimaria = empresa?.CorPrimaria ?? "#0d6efd"
            });
        })
        .AllowAnonymous()
        .WithName("Login");

        grupo.MapPost("/logout", async (HttpContext http, SupabaseAuthService supabase, CancellationToken ct) =>
        {
            var header = http.Request.Headers.Authorization.ToString();
            const string prefixo = "Bearer ";
            if (header.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                await supabase.EncerrarAsync(header[prefixo.Length..].Trim(), ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("Logout");
    }
}

public static class ConfigEndpoints
{
    private static readonly HashSet<string> TiposAceitos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "image/gif", "image/svg+xml"
    };

    private const int LogoMaxBytes = 2 * 1024 * 1024;

    public static void MapConfigEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/config").WithTags("Configuracao").RequireAuthorization();

        grupo.MapGet("/empresa", async (SessaoAtual sessao, IEmpresaRepository empresas, CancellationToken ct) =>
        {
            var empresa = await empresas.ObterAsync(sessao.EmpresaId, ct);
            return empresa is null
                ? Validacao.NaoEncontrado("Empresa nao encontrada.")
                : Results.Ok(empresa);
        });

        grupo.MapPut("/empresa", async (
            AtualizarEmpresaRequest request,
            SessaoAtual sessao,
            IEmpresaRepository empresas,
            CancellationToken ct) =>
        {
            if (!sessao.EhAdmin)
                return Validacao.SemPermissao("Somente administradores alteram os dados da loja.");

            if (string.IsNullOrWhiteSpace(request.Nome))
                return Validacao.Falha("Informe o nome da loja.");

            var empresa = await empresas.AtualizarAsync(sessao.EmpresaId, request, ct);
            return empresa is null
                ? Validacao.NaoEncontrado("Empresa nao encontrada.")
                : Results.Ok(empresa);
        });

        grupo.MapPost("/logo", async (
            HttpRequest request,
            SessaoAtual sessao,
            IEmpresaRepository empresas,
            CancellationToken ct) =>
        {
            if (!sessao.EhAdmin)
                return Validacao.SemPermissao("Somente administradores alteram a identidade visual.");

            if (!request.HasFormContentType)
                return Validacao.Falha("Envie a logo como arquivo (multipart/form-data, campo 'logo').");

            var form = await request.ReadFormAsync(ct);
            var arquivo = form.Files.GetFile("logo");

            if (arquivo is null || arquivo.Length == 0)
                return Validacao.Falha("Selecione um arquivo de imagem.");

            if (arquivo.Length > LogoMaxBytes)
                return Validacao.Falha("A logo deve ter no maximo 2 MB.");

            var contentType = arquivo.ContentType?.Split(';')[0].Trim() ?? string.Empty;
            if (!TiposAceitos.Contains(contentType))
                return Validacao.Falha("Formato nao suportado. Use PNG, JPG, WebP, GIF ou SVG.");

            await using var buffer = new MemoryStream();
            await arquivo.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();

            var empresa = await empresas.SalvarLogoAsync(sessao.EmpresaId, bytes, contentType, ct);
            return empresa is null
                ? Validacao.NaoEncontrado("Empresa nao encontrada.")
                : Results.Ok(empresa);
        })
        .WithName("EnviarLogo");

        grupo.MapDelete("/logo", async (SessaoAtual sessao, IEmpresaRepository empresas, CancellationToken ct) =>
        {
            if (!sessao.EhAdmin)
                return Validacao.SemPermissao("Somente administradores alteram a identidade visual.");

            var empresa = await empresas.RemoverLogoAsync(sessao.EmpresaId, ct);
            return empresa is null
                ? Validacao.NaoEncontrado("Empresa nao encontrada.")
                : Results.Ok(empresa);
        });

        grupo.MapGet("/logo/{empresaId:guid}", async (
            Guid empresaId,
            HttpContext http,
            IAssistechDb db,
            CancellationToken ct) =>
        {
            if (empresaId == Guid.Empty)
                return Validacao.Falha("Empresa invalida.");

            await using var conn = await db.OpenAsync(empresaId, ct);
            await using var cmd = new Npgsql.NpgsqlCommand(
                "select logo, coalesce(logo_content_type, 'image/png') from public.empresas where id = $1 and logo is not null", conn);
            cmd.Parameters.AddWithValue(empresaId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return Validacao.NaoEncontrado("Logo nao encontrada.");

            var bytes = (byte[])reader.GetValue(0);
            var tipo = reader.GetString(1);

            // A logo costuma mudar; o ETag evita reenvio sem perder a correcao.
            var etag = $"\"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..16]}\"";
            if (string.Equals(http.Request.Headers.IfNoneMatch, etag, StringComparison.Ordinal))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            http.Response.Headers.ETag = etag;
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.File(bytes, tipo);
        })
        .AllowAnonymous()
        .WithName("ObterLogo");
    }
}
