namespace Assistech.Data;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>URI do projeto Supabase. Ex.: https://abcdefgh.supabase.co</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Chave anon. Usada apenas para validar tokens no endpoint /auth/v1.</summary>
    public string AnonKey { get; set; } = string.Empty;

    /// <summary>Connection string do Postgres (URI). Prefira o papel assistech_api.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Aplica os scripts de db/ automaticamente na subida da API.</summary>
    public bool AplicarSchemaAutomaticamente { get; set; } = true;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(Url)
        && !string.IsNullOrWhiteSpace(AnonKey)
        && !string.IsNullOrWhiteSpace(ConnectionString);

    /// <summary>
    /// O painel do Supabase entrega a conexao no formato URI
    /// (postgresql://usuario:senha@host:5432/postgres), que o Npgsql nao
    /// aceita. Aqui ela vira o formato key=value esperado pelo driver.
    /// </summary>
    public static string NormalizarConnectionString(string connectionString)
    {
        var valor = connectionString.Trim();
        if (!valor.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !valor.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return valor;

        var uri = new Uri(valor);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            // Supabase exige TLS. "Require" cifra sem validar o certificado,
            // que nao casa com o host do projeto (o curinga e *.supabase.co).
            SslMode = Npgsql.SslMode.Require
        };

        var usuario = uri.UserInfo;
        if (usuario.Length > 0)
        {
            var separador = usuario.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separador < 0 ? usuario : usuario[..separador]);
            if (separador >= 0)
                builder.Password = Uri.UnescapeDataString(usuario[(separador + 1)..]);
        }

        return builder.ConnectionString;
    }
}
