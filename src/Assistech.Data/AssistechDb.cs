using Assistech.Shared.Enums;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.NameTranslation;
using Npgsql.TypeMapping;

namespace Assistech.Data;

/// <summary>
/// Fabrica de conexoes. Toda conexao devolvida ja carrega o contexto de
/// empresa em <c>app.empresa_id</c>, que e lido pelas politicas de RLS.
/// </summary>
public interface IAssistechDb
{
    Task<NpgsqlConnection> OpenAsync(Guid empresaId, CancellationToken ct = default);
    Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default);
    Task<NpgsqlCommand> CreateCommandAsync(NpgsqlConnection conn, string sql, CancellationToken ct = default);
    Task TestarConexaoAsync(CancellationToken ct = default);
}

public sealed class AssistechDb : IAssistechDb
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly SupabaseOptions _options;

    public AssistechDb(IOptions<SupabaseOptions> options)
    {
        _options = options.Value;

        if (!_options.EstaConfigurado)
            throw new InvalidOperationException(
                "Supabase nao configurado. Defina Supabase:Url, Supabase:AnonKey e Supabase:ConnectionString.");

        var builder = new NpgsqlDataSourceBuilder(SupabaseOptions.NormalizarConnectionString(_options.ConnectionString));

        // Os enums do C# (PascalCase) espelham os enums do Postgres (snake_case).
        var tradutor = new NpgsqlSnakeCaseNameTranslator();
        builder.MapEnum<StatusOrdemServico>("status_os", tradutor);
        builder.MapEnum<TipoEquipamento>("tipo_equipamento", tradutor);
        builder.MapEnum<TipoItemOrdemServico>("tipo_item_os", tradutor);
        builder.MapEnum<PerfilUsuario>("perfil_usuario", tradutor);

        _dataSource = builder.Build();
    }

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        return conn;
    }

    public async Task<NpgsqlConnection> OpenAsync(Guid empresaId, CancellationToken ct = default)
    {
        var conn = await OpenAsync(ct).ConfigureAwait(false);
        try
        {
            await DefinirEmpresaAsync(conn, empresaId, ct).ConfigureAwait(false);
            return conn;
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<NpgsqlCommand> CreateCommandAsync(NpgsqlConnection conn, string sql, CancellationToken ct = default)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await Task.FromResult(cmd).ConfigureAwait(false);
    }

    public async Task TestarConexaoAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand("select 1", conn);
        await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    private static async Task DefinirEmpresaAsync(NpgsqlConnection conn, Guid empresaId, CancellationToken ct)
    {
        // set_config com is_local=false porque o Npgsql reaproveita conexoes do pool.
        await using var cmd = new NpgsqlCommand("select set_config('app.empresa_id', $1, false)", conn);
        cmd.Parameters.AddWithValue(empresaId.ToString());
        await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }
}
