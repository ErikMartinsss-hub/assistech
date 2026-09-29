using Assistech.Shared.Enums;
using Npgsql;

namespace Assistech.Data;

public sealed record VinculoUsuario(Guid EmpresaId, Guid UsuarioId, string Nome, string Email, PerfilUsuario Perfil);

public interface IUsuarioRepository
{
    Task<VinculoUsuario?> ObterPorUsuarioAuthAsync(Guid usuarioAuthId, CancellationToken ct = default);
    Task<VinculoUsuario> CriarVinculoAsync(Guid empresaId, Guid usuarioAuthId, string nome, string email, PerfilUsuario perfil, CancellationToken ct = default);
    Task<string?> ObterNomeAsync(Guid empresaId, Guid usuarioAuthId, CancellationToken ct = default);
}

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly IAssistechDb _db;

    public UsuarioRepository(IAssistechDb db) => _db = db;

    public async Task<VinculoUsuario?> ObterPorUsuarioAuthAsync(Guid usuarioAuthId, CancellationToken ct = default)
    {
        // A RLS nao deixa ler public.usuarios sem app.empresa_id; a funcao de
        // bootstrap devolve o id da loja e o restante da consulta ja fica no escopo.
        var empresaId = await ResolverEmpresaAsync(usuarioAuthId, ct);
        if (empresaId is null) return null;

        const string sql = """
            select u.empresa_id, u.id, u.nome, coalesce(u.email, ''), u.perfil
              from public.usuarios u
             where u.usuario_id = $1 and u.ativo
            """;

        await using var conn = await _db.OpenAsync(empresaId.Value, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(usuarioAuthId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new VinculoUsuario(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<PerfilUsuario>(4))
            : null;
    }

    private async Task<Guid?> ResolverEmpresaAsync(Guid usuarioAuthId, CancellationToken ct)
    {
        await using var conn = await _db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("select public.empresa_do_usuario($1)", conn);
        cmd.Parameters.AddWithValue(usuarioAuthId);

        return await cmd.ExecuteScalarAsync(ct) is Guid empresaId && empresaId != Guid.Empty
            ? empresaId
            : null;
    }

    public async Task<VinculoUsuario> CriarVinculoAsync(Guid empresaId, Guid usuarioAuthId, string nome, string email, PerfilUsuario perfil, CancellationToken ct = default)
    {
        const string sql = """
            insert into public.usuarios (empresa_id, usuario_id, nome, email, perfil)
            values ($1, $2, $3, $4, $5)
            returning empresa_id, id, nome, coalesce(email, ''), perfil
            on conflict (usuario_id) do update
               set nome = excluded.nome, email = excluded.email, perfil = excluded.perfil
            returning empresa_id, id, nome, coalesce(email, ''), perfil
            """;

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(usuarioAuthId);
        cmd.Parameters.AddWithValue(nome);
        cmd.Parameters.AddWithValue(email);
        cmd.Parameters.AddWithValue(perfil);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new VinculoUsuario(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<PerfilUsuario>(4));
    }

    public async Task<string?> ObterNomeAsync(Guid empresaId, Guid usuarioAuthId, CancellationToken ct = default)
    {
        const string sql = "select nome from public.usuarios where empresa_id = $1 and usuario_id = $2";

        await using var conn = await _db.OpenAsync(empresaId, ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(empresaId);
        cmd.Parameters.AddWithValue(usuarioAuthId);

        return (string?)await cmd.ExecuteScalarAsync(ct);
    }
}
