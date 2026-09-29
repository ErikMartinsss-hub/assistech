using Assistech.Api.Infrastructure;
using Assistech.Data;
using Assistech.Shared.Dtos;

namespace Assistech.Api.Endpoints;

public static class OrdemServicoEndpoints
{
    public static void MapOrdensEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/ordens").WithTags("Ordens de Servico").RequireAuthorization();

        grupo.MapGet("/", async ([AsParameters] OrdemServicoFiltro filtro, SessaoAtual sessao, IOrdemServicoRepository repo, CancellationToken ct) =>
        {
            if (filtro.TamanhoPagina > 200) filtro.TamanhoPagina = 200;
            return Results.Ok(await repo.ListarAsync(sessao.EmpresaId, filtro, ct));
        });

        grupo.MapGet("/resumo", async (SessaoAtual sessao, IOrdemServicoRepository repo, CancellationToken ct) =>
            Results.Ok(new { EmAndamento = await repo.ContarPorStatusAsync(sessao.EmpresaId, ct) }));

        grupo.MapGet("/{id:guid}", async (Guid id, SessaoAtual sessao, IOrdemServicoRepository repo, CancellationToken ct) =>
        {
            var ordem = await repo.ObterAsync(sessao.EmpresaId, id, ct);
            return ordem is null ? Validacao.NaoEncontrado("Ordem de servico nao encontrada.") : Results.Ok(ordem);
        });

        grupo.MapGet("/{id:guid}/termo", async (Guid id, SessaoAtual sessao, IOrdemServicoRepository repo, CancellationToken ct) =>
        {
            var termo = await repo.ObterTermoAsync(sessao.EmpresaId, id, ct);
            return termo is null ? Validacao.NaoEncontrado("Ordem de servico nao encontrada.") : Results.Ok(termo);
        });

        grupo.MapPost("/", async (OrdemServicoInput input, SessaoAtual sessao, IOrdemServicoRepository repo, CancellationToken ct) =>
        {
            if (Validar(input) is { } erro) return erro;

            var ordem = await repo.SalvarAsync(sessao.EmpresaId, input, sessao.Nome, ct);
            return Results.Created($"/api/ordens/{ordem.Id}", ordem);
        });

        grupo.MapPut("/{id:guid}", async (Guid id, OrdemServicoInput input, SessaoAtual sessao, IOrdemServicoRepository repo, CancellationToken ct) =>
        {
            input.Id = id;
            if (Validar(input) is { } erro) return erro;

            if (await repo.ObterAsync(sessao.EmpresaId, id, ct) is null)
                return Validacao.NaoEncontrado("Ordem de servico nao encontrada.");

            return Results.Ok(await repo.SalvarAsync(sessao.EmpresaId, input, sessao.Nome, ct));
        });

        grupo.MapPost("/{id:guid}/status", async (
            Guid id,
            AlterarStatusRequest request,
            SessaoAtual sessao,
            IOrdemServicoRepository repo,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Status))
                return Validacao.Falha("Informe o novo status.");

            var ordem = await repo.AlterarStatusAsync(sessao.EmpresaId, id, request, sessao.Nome, ct);
            return ordem is null
                ? Validacao.NaoEncontrado("Ordem de servico nao encontrada.")
                : Results.Ok(ordem);
        });

        grupo.MapDelete("/{id:guid}", async (Guid id, SessaoAtual sessao, IOrdemServicoRepository repo, CancellationToken ct) =>
        {
            if (!sessao.EhAdmin)
                return Validacao.SemPermissao("Somente administradores podem excluir ordens de servico.");

            await repo.ExcluirAsync(sessao.EmpresaId, id, ct);
            return Results.NoContent();
        });
    }

    private static IResult? Validar(OrdemServicoInput input)
    {
        if (input.ClienteId is null || input.ClienteId == Guid.Empty)
            return Validacao.Falha("Selecione o cliente da ordem de servico.");

        if (input.EquipamentoId is null || input.EquipamentoId == Guid.Empty)
            return Validacao.Falha("Selecione o equipamento que entrou para o servico.");

        if (input.Itens.Any(i => string.IsNullOrWhiteSpace(i.Descricao)))
            return Validacao.Falha("Todos os itens precisam de descricao.");

        if (input.GarantiaDias is < 0 or > 3650)
            return Validacao.Falha("Garantia deve estar entre 0 e 3650 dias.");

        return null;
    }
}
