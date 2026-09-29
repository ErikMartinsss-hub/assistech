using System.Data;
using Npgsql;

namespace Assistech.Data;

internal static class ReaderExtensions
{
    /// <summary>timestamptz volta como DateTime UTC; converte preservando o instante.</summary>
    public static DateTimeOffset GetUtc(this NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dto => dto.ToUniversalTime(),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => throw new InvalidCastException($"Valor '{value?.GetType().Name}' nao e uma data valida.")
        };
    }

    public static DateTimeOffset? GetUtcNullable(this NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetUtc(ordinal);

    public static string? GetStringOrNull(this NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static decimal GetDecimal(this NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0m : reader.GetFieldValue<decimal>(ordinal);

    public static decimal? GetDecimalNullable(this NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<decimal>(ordinal);

    public static Guid? GetGuidNullable(this NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    public static async Task<List<T>> LerListaAsync<T>(
        this NpgsqlDataReader reader,
        Func<NpgsqlDataReader, T> mapear,
        CancellationToken ct = default)
    {
        var lista = new List<T>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            lista.Add(mapear(reader));
        return lista;
    }

    public static void AddIfNotNull(this NpgsqlParameterCollection parametros, string nome, object? valor)
    {
        if (valor is not null)
            parametros.AddWithValue(nome, valor);
    }

    public static NpgsqlParameter WithNullable(this NpgsqlParameterCollection parametros, string nome, object? valor)
        => parametros.AddWithValue(nome, valor ?? (object)DBNull.Value);
}
