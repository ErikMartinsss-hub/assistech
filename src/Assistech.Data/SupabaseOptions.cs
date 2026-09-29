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
}
