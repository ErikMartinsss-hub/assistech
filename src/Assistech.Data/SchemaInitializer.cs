using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Assistech.Data;

public interface ISchemaInitializer
{
    Task AplicarAsync(CancellationToken ct = default);
}

/// <summary>
/// Aplica db/001_schema.sql na subida da API (idempotente: todo DDL usa IF NOT EXISTS).
/// Os scripts 002 (RLS) e 003 (papel da aplicacao) exigem superusuario e sao manuais.
/// </summary>
public sealed class SchemaInitializer : ISchemaInitializer
{
    private readonly IAssistechDb _db;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SchemaInitializer> _log;

    public SchemaInitializer(IAssistechDb db, IOptions<SupabaseOptions> options, ILogger<SchemaInitializer> log)
    {
        _db = db;
        _options = options.Value;
        _log = log;
    }

    public async Task AplicarAsync(CancellationToken ct = default)
    {
        if (!_options.AplicarSchemaAutomaticamente)
        {
            _log.LogInformation("Aplicacao automatica de schema desativada.");
            return;
        }

        var sql = CarregarScript("001_schema.sql");

        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(ct);

        _log.LogInformation("Schema da Assistech aplicado com sucesso.");
    }

    private static string CarregarScript(string nome)
    {
        var assembly = typeof(SchemaInitializer).Assembly;
        var recurso = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(nome, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Script '{nome}' nao encontrado nos recursos do assembly.");

        using var stream = assembly.GetManifestResourceStream(recurso)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
