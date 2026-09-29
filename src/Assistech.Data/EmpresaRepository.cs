using Assistech.Shared.Dtos;
using Npgsql;

namespace Assistech.Data;

public interface IEmpresaRepository
{
    Task<EmpresaDto?> ObterAsync(Guid empresaId, CancellationToken ct = default);
    Task<IReadOnlyList<EmpresaDto>> ListarAsync(CancellationToken ct = default);
    Task<EmpresaDto?> AtualizarAsync(Guid empresaId, AtualizarEmpresaRequest request, CancellationToken ct = default);
    Task<EmpresaDto?> SalvarLogoAsync(Guid empresaId, byte[] logo, string contentType, CancellationToken ct = default);
    Task<EmpresaDto?> RemoverLogoAsync(Guid empresaId, CancellationToken ct = default);
    Task<EmpresaDto> CriarAsync(string nome, CancellationToken ct = default);
}

public sealed class EmpresaRepository : IEmpresaRepository
{
    private const string Colunas = "id, nome, documento, telefone, whatsapp, email, endereco, cor_primaria, logo";

    private readonly IAssistechDb _db;

    public EmpresaRepository(IAssistechDb db) => _db = db;

    public async Task<EmpresaDto?> ObterAsync(Guid empresaId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand($"select {Colunas} from public.empresas where id = $1", conn);
        cmd.Parameters.AddWithValue(empresaId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Mapear(reader) : null;
    }

    /// <summary>
    /// Lista as lojas do primeiro acesso, quando ainda nao existe contexto de empresa.
    /// A RLS impede <c>select</c> sem <c>app.empresa_id</c>, entao a unica loja
    /// cadastrada e resolvida por <c>public.empresa_unica()</c>.
    /// </summary>
    public async Task<IReadOnlyList<EmpresaDto>> ListarAsync(CancellationToken ct = default)
    {
        var unicaId = await ResolverEmpresaUnicaAsync(ct);
        if (unicaId is null) return Array.Empty<EmpresaDto>();

        await using var conn = await _db.OpenAsync(unicaId.Value, ct);
        await using var cmd = new NpgsqlCommand($"select {Colunas} from public.empresas order by nome", conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.LerListaAsync(Mapear, ct);
    }

    private async Task<Guid?> ResolverEmpresaUnicaAsync(CancellationToken ct)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("select public.empresa_unica()", conn);

        return await cmd.ExecuteScalarAsync(ct) is Guid empresaId && empresaId != Guid.Empty
            ? empresaId
            : null;
    }

    public async Task<EmpresaDto?> AtualizarAsync(Guid empresaId, AtualizarEmpresaRequest r, CancellationToken ct = default)
    {
        const string sql = """
            update public.empresas
               set nome = $2, documento = $3, telefone = $4, whatsapp = $5,
                   email = $6, endereco = $7, cor_primaria = $8
             where id = $1
            returning id, nome, documento, telefone, whatsapp, email, endereco, cor_primaria, logo
            """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(Nz(r.Nome));
        cmd.Parameters.AddWithValue(Nz(r.Documento));
        cmd.Parameters.AddWithValue(Nz(r.Telefone));
        cmd.Parameters.AddWithValue(Nz(r.WhatsApp));
        cmd.Parameters.AddWithValue(Nz(r.Email));
        cmd.Parameters.AddWithValue(Nz(r.Endereco));
        cmd.Parameters.AddWithValue(Nz(r.CorPrimaria));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Mapear(reader) : null;
    }

    public async Task<EmpresaDto?> SalvarLogoAsync(Guid empresaId, byte[] logo, string contentType, CancellationToken ct = default)
    {
        const string sql = """
            update public.empresas
               set logo = $2, logo_content_type = $3
             where id = $1
            returning id, nome, documento, telefone, whatsapp, email, endereco, cor_primaria, logo
            """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(logo);
        cmd.Parameters.AddWithValue(contentType);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Mapear(reader) : null;
    }

    public async Task<EmpresaDto?> RemoverLogoAsync(Guid empresaId, CancellationToken ct = default)
    {
        const string sql = """
            update public.empresas
               set logo = null, logo_content_type = null
             where id = $1
            returning id, nome, documento, telefone, whatsapp, email, endereco, cor_primaria, logo
            """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Mapear(reader) : null;
    }

    public async Task<EmpresaDto> CriarAsync(string nome, CancellationToken ct = default)
    {
        const string sql = """
            insert into public.empresas (nome)
            values ($1)
            returning id, nome, documento, telefone, whatsapp, email, endereco, cor_primaria, logo
            """;

        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(nome);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return Mapear(reader);
    }

    private static EmpresaDto Mapear(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Nome = r.GetString(1),
        Documento = r.GetStringOrNull(2) ?? string.Empty,
        Telefone = r.GetStringOrNull(3) ?? string.Empty,
        WhatsApp = r.GetStringOrNull(4) ?? string.Empty,
        Email = r.GetStringOrNull(5) ?? string.Empty,
        Endereco = r.GetStringOrNull(6) ?? string.Empty,
        CorPrimaria = r.GetString(7),
        TemLogo = !r.IsDBNull(8),
        UrlLogo = r.IsDBNull(8) ? string.Empty : $"api/config/logo/{r.GetGuid(0)}"
    };

    private static string Nz(string? valor) => string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();
}
