using Assistech.Shared.Dtos;
using Npgsql;

namespace Assistech.Data;

public interface IEquipamentoRepository
{
    Task<IReadOnlyList<EquipamentoDto>> ListarPorClienteAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default);
    Task<EquipamentoDto?> ObterAsync(Guid empresaId, Guid id, CancellationToken ct = default);
    Task<EquipamentoDto> SalvarAsync(Guid empresaId, EquipamentoInput input, CancellationToken ct = default);
    Task ExcluirAsync(Guid empresaId, Guid id, CancellationToken ct = default);
}

public sealed class EquipamentoRepository : IEquipamentoRepository
{
    private const string Selecao = """
        select e.id, e.cliente_id, c.nome, e.tipo::text, e.marca, e.modelo,
               coalesce(e.numero_serie, ''), coalesce(e.cor, ''), e.ano, coalesce(e.observacoes, ''),
               (select count(*) from public.ordens_servico o where o.equipamento_id = e.id)
          from public.equipamentos e
          join public.clientes c on c.id = e.cliente_id
        """;

    private readonly IAssistechDb _db;

    public EquipamentoRepository(IAssistechDb db) => _db = db;

    public async Task<IReadOnlyList<EquipamentoDto>> ListarPorClienteAsync(Guid empresaId, Guid clienteId, CancellationToken ct = default)
    {
        var sql = $"{Selecao} where e.empresa_id = $1 and e.cliente_id = $2 order by e.marca, e.modelo";

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(clienteId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.LerListaAsync(Mapear, ct);
    }

    public async Task<EquipamentoDto?> ObterAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        var sql = $"{Selecao} where e.empresa_id = $1 and e.id = $2";

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Mapear(reader) : null;
    }

    public async Task<EquipamentoDto> SalvarAsync(Guid empresaId, EquipamentoInput input, CancellationToken ct = default)
    {
        var id = input.Id ?? Guid.NewGuid();
        var tipo = ValidarTipo(input.Tipo);

        var sql = input.Id.HasValue
            ? """
              update public.equipamentos
                 set cliente_id = $3, tipo = $4, marca = $5, modelo = $6,
                     numero_serie = $7, cor = $8, ano = $9, observacoes = $10
               where empresa_id = $1 and id = $2
              """
            : """
              insert into public.equipamentos
                     (id, empresa_id, cliente_id, tipo, marca, modelo, numero_serie, cor, ano, observacoes)
              values ($2, $1, $3, $4, $5, $6, $7, $8, $9, $10)
              """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(input.ClienteId);
        cmd.Parameters.AddWithValue(tipo);
        cmd.Parameters.AddWithValue(input.Marca.Trim());
        cmd.Parameters.AddWithValue(input.Modelo.Trim());
        cmd.Parameters.AddWithValue(Nz(input.NumeroSerie));
        cmd.Parameters.AddWithValue(Nz(input.Cor));
        cmd.Parameters.AddWithValue(input.Ano.HasValue && input.Ano is > 1900 and <= 2200 ? input.Ano.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue(Nz(input.Observacoes));

        await cmd.ExecuteNonQueryAsync(ct);

        return await ObterAsync(empresaId, id, ct)
               ?? throw new InvalidOperationException("Equipamento nao encontrado apos o salvamento.");
    }

    public async Task ExcluirAsync(Guid empresaId, Guid id, CancellationToken ct = default)
    {
        const string sql = "delete from public.equipamentos where empresa_id = $1 and id = $2";

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static string ValidarTipo(string? tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "celular" or "" or null => "celular",
        "desktop" => "desktop",
        "notebook" => "notebook",
        "tablet" => "tablet",
        _ => "outro"
    };

    private static EquipamentoDto Mapear(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        ClienteId = r.GetGuid(1),
        ClienteNome = r.GetString(2),
        Tipo = r.GetString(3),
        Marca = r.GetString(4),
        Modelo = r.GetString(5),
        NumeroSerie = r.GetString(6),
        Cor = r.GetString(7),
        Ano = r.IsDBNull(8) ? null : Convert.ToInt32(r.GetValue(8)),
        Observacoes = r.GetString(9),
        TotalOrdens = Convert.ToInt32(r.GetValue(10))
    };

    private static string Nz(string? valor) => string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();
}
