using Assistech.Api.Infrastructure;
using Assistech.Data;
using Assistech.Shared.Dtos;
using Npgsql;

namespace Assistech.Api.Endpoints;

public static class ClienteEndpoints
{
    public static void MapClientesEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/clientes").WithTags("Clientes").RequireAuthorization();

        grupo.MapGet("/", async ([AsParameters] ClienteFiltro filtro, SessaoAtual sessao, IClienteRepository repo, CancellationToken ct) =>
        {
            if (filtro.TamanhoPagina > 200) filtro.TamanhoPagina = 200;
            return Results.Ok(await repo.ListarAsync(sessao.EmpresaId, filtro, ct));
        });

        grupo.MapGet("/{id:guid}", async (Guid id, SessaoAtual sessao, IClienteRepository repo, CancellationToken ct) =>
        {
            var cliente = await repo.ObterAsync(sessao.EmpresaId, id, ct);
            return cliente is null ? Validacao.NaoEncontrado("Cliente nao encontrado.") : Results.Ok(cliente);
        });

        grupo.MapPost("/", async (ClienteInput input, SessaoAtual sessao, IClienteRepository repo, CancellationToken ct) =>
        {
            if (!Validar(input)) return Validacao.ErroDeValidacao(input);
            return Results.Created($"/api/clientes/{input.Id}", await repo.SalvarAsync(sessao.EmpresaId, input, ct));
        });

        grupo.MapPut("/{id:guid}", async (Guid id, ClienteInput input, SessaoAtual sessao, IClienteRepository repo, CancellationToken ct) =>
        {
            if (!Validar(input)) return Validacao.ErroDeValidacao(input);

            input.Id = id;
            if (await repo.ObterAsync(sessao.EmpresaId, id, ct) is null)
                return Validacao.NaoEncontrado("Cliente nao encontrado.");

            return Results.Ok(await repo.SalvarAsync(sessao.EmpresaId, input, ct));
        });

        grupo.MapDelete("/{id:guid}", async (Guid id, SessaoAtual sessao, IClienteRepository repo, IAssistechDb db, CancellationToken ct) =>
        {
            var vinculos = await ContarVinculosAsync(db, sessao.EmpresaId, id, ct);
            if (vinculos.Ordens > 0)
                return Validacao.Falha($"Este cliente possui {vinculos.Ordens} ordem(ns) de servico e nao pode ser excluido.");

            await repo.ExcluirAsync(sessao.EmpresaId, id, ct);
            return Results.NoContent();
        });
    }

    private static bool Validar(ClienteInput input) =>
        !string.IsNullOrWhiteSpace(input.Nome) && input.Nome.Trim().Length <= 160;

    internal static async Task<(int Equipamentos, int Ordens)> ContarVinculosAsync(
        IAssistechDb db, Guid empresaId, Guid clienteId, CancellationToken ct)
    {
        const string sql = """
            select (select count(*) from public.equipamentos where empresa_id = $1 and cliente_id = $2),
                   (select count(*) from public.ordens_servico where empresa_id = $1 and cliente_id = $2)
            """;

        await using var conn = await db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(clienteId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (Convert.ToInt32(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)));
    }
}

public static class EquipamentoEndpoints
{
    public static void MapEquipamentosEndpoints(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/equipamentos").WithTags("Equipamentos").RequireAuthorization();

        grupo.MapGet("/", async (Guid? clienteId, SessaoAtual sessao, IEquipamentoRepository repo, CancellationToken ct) =>
        {
            if (clienteId is null || clienteId == Guid.Empty)
                return Validacao.Falha("Informe o cliente para listar os equipamentos.");

            return Results.Ok(await repo.ListarPorClienteAsync(sessao.EmpresaId, clienteId.Value, ct));
        });

        grupo.MapPost("/", async (EquipamentoInput input, SessaoAtual sessao, IEquipamentoRepository repo, IClienteRepository clientes, CancellationToken ct) =>
        {
            if (input.ClienteId == Guid.Empty) return Validacao.Falha("Informe o cliente do equipamento.");
            if (string.IsNullOrWhiteSpace(input.Marca) || string.IsNullOrWhiteSpace(input.Modelo))
                return Validacao.Falha("Informe a marca e o modelo do equipamento.");

            if (await clientes.ObterAsync(sessao.EmpresaId, input.ClienteId, ct) is null)
                return Validacao.NaoEncontrado("Cliente nao encontrado.");

            var equipamento = await repo.SalvarAsync(sessao.EmpresaId, input, ct);
            return Results.Created($"/api/equipamentos/{equipamento.Id}", equipamento);
        });

        grupo.MapPut("/{id:guid}", async (Guid id, EquipamentoInput input, SessaoAtual sessao, IEquipamentoRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Marca) || string.IsNullOrWhiteSpace(input.Modelo))
                return Validacao.Falha("Informe a marca e o modelo do equipamento.");

            input.Id = id;
            if (await repo.ObterAsync(sessao.EmpresaId, id, ct) is null)
                return Validacao.NaoEncontrado("Equipamento nao encontrado.");

            return Results.Ok(await repo.SalvarAsync(sessao.EmpresaId, input, ct));
        });

        grupo.MapDelete("/{id:guid}", async (Guid id, SessaoAtual sessao, IEquipamentoRepository repo, IAssistechDb db, CancellationToken ct) =>
        {
            var vinculos = await ContarOrdensEquipamentoAsync(db, sessao.EmpresaId, id, ct);
            if (vinculos > 0)
                return Validacao.Falha($"Este equipamento possui {vinculos} ordem(ns) de servico e nao pode ser excluido.");

            await repo.ExcluirAsync(sessao.EmpresaId, id, ct);
            return Results.NoContent();
        });
    }

    private static async Task<int> ContarOrdensEquipamentoAsync(IAssistechDb db, Guid empresaId, Guid equipamentoId, CancellationToken ct)
    {
        const string sql = "select count(*) from public.ordens_servico where empresa_id = $1 and equipamento_id = $2";

        await using var conn = await db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(equipamentoId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}
