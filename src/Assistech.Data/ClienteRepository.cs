using Assistech.Shared.Dtos;
using Npgsql;

namespace Assistech.Data;

public interface IClienteRepository
{
    Task<PagedResult<ClienteDto>> ListarAsync(Guid empresaId, ClienteFiltro filtro, CancellationToken ct = default);
    Task<ClienteDto?> ObterAsync(Guid empresaId, Guid id, CancellationToken ct = default);
    Task<ClienteDto> SalvarAsync(Guid empresaId, ClienteInput input, CancellationToken ct = default);
    Task ExcluirAsync(Guid empresaId, Guid id, CancellationToken ct = default);
}

public sealed class ClienteRepository : IClienteRepository
{
    private const string Selecao = """
        select c.id, c.nome, c.documento, c.telefone, c.email, c.endereco,
               c.observacoes, c.criado_em,
               (select count(*) from public.equipamentos e where e.cliente_id = c.id),
               (select count(*) from public.ordens_servico o where o.cliente_id = c.id)
          from public.clientes c
        """;

    private readonly IAssistechDb _db;

    public ClienteRepository(IAssistechDb db) => _db = db;

    public async Task<PagedResult<ClienteDto>> ListarAsync(Guid empresaId, ClienteFiltro filtro, CancellationToken ct = default)
    {
        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, 200);
        var busca = (filtro.Busca ?? string.Empty).Trim();

        var where = "where c.empresa_id = $1";
        if (busca.Length > 0)
            where += " and (c.nome ilike $2 or c.telefone ilike $2 or c.documento ilike $2 or coalesce(c.email,'') ilike $2)";

        await using var conn = await _db.OpenAsync(empresaId, ct);

        int total;
        await using (var cmd = new NpgsqlCommand($"select count(*) from public.clientes c {where}", conn))
        {
            cmd.Parameters.AddWithValue(empresaId);
            if (busca.Length > 0) cmd.Parameters.AddWithValue($"%{busca}%");
            total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        var sql = $"{Selecao} {where} order by c.nome limit {tamanho} offset {(pagina - 1) * tamanho}";
        await using var cmdPagina = new NpgsqlCommand(sql, conn);
        cmdPagina.Parameters.AddWithValue(empresaId);
        if (busca.Length > 0) cmdPagina.Parameters.AddWithValue($"%{busca}%");

        await using var reader = await cmdPagina.ExecuteReaderAsync(ct);
        var itens = await reader.LerListaAsync(Mapear, ct);

        return new PagedResult<ClienteDto>
        {
            Itens = itens,
            Total = total,
            Pagina = pagina,
            TamanhoPagina = tamanho
        };
    }

    public async Task<ClienteDto?> ObterAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        var sql = $"{Selecao} where c.empresa_id = $1 and c.id = $2";

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Mapear(reader) : null;
    }

    public async Task<ClienteDto> SalvarAsync(Guid empresaId, ClienteInput input, CancellationToken ct = default)
    {
        var id = input.Id ?? Guid.NewGuid();

        var sql = input.Id.HasValue
            ? """
              update public.clientes
                 set nome = $3, documento = $4, telefone = $5, email = $6, endereco = $7, observacoes = $8
               where empresa_id = $1 and id = $2
              """
            : """
              insert into public.clientes (id, empresa_id, nome, documento, telefone, email, endereco, observacoes)
              values ($2, $1, $3, $4, $5, $6, $7, $8)
              """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(input.Nome.Trim());
        cmd.Parameters.AddWithValue(Nz(input.Documento));
        cmd.Parameters.AddWithValue(Nz(input.Telefone));
        cmd.Parameters.AddWithValue(Nz(input.Email));
        cmd.Parameters.AddWithValue(Nz(input.Endereco));
        cmd.Parameters.AddWithValue(Nz(input.Observacoes));

        await cmd.ExecuteNonQueryAsync(ct);

        return await ObterAsync(empresaId, id, ct)
               ?? throw new InvalidOperationException("Cliente nao encontrado apos o salvamento.");
    }

    public async Task ExcluirAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        const string sql = "delete from public.clientes where empresa_id = $1 and id = $2";

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ClienteDto Mapear(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Nome = r.GetString(1),
        Documento = r.GetStringOrNull(2) ?? string.Empty,
        Telefone = r.GetStringOrNull(3) ?? string.Empty,
        Email = r.GetStringOrNull(4) ?? string.Empty,
        Endereco = r.GetStringOrNull(5) ?? string.Empty,
        Observacoes = r.GetStringOrNull(6) ?? string.Empty,
        CriadoEm = r.GetUtc(7),
        TotalEquipamentos = Convert.ToInt32(r.GetValue(8)),
        TotalOrdens = Convert.ToInt32(r.GetValue(9))
    };

    private static string Nz(string? valor) => string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();
}
