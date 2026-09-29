using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Assistech.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddAssistechData(this IServiceCollection services, Action<SupabaseOptions>? configurar = null)
    {
        if (configurar is not null)
            services.Configure(configurar);

        services.TryAddScoped<IAssistechDb, AssistechDb>();
        services.TryAddScoped<ISchemaInitializer, SchemaInitializer>();
        services.TryAddScoped<SessaoAtual>();

        services.TryAddScoped<IEmpresaRepository, EmpresaRepository>();
        services.TryAddScoped<IUsuarioRepository, UsuarioRepository>();
        services.TryAddScoped<IClienteRepository, ClienteRepository>();
        services.TryAddScoped<IEquipamentoRepository, EquipamentoRepository>();
        services.TryAddScoped<IOrdemServicoRepository, OrdemServicoRepository>();

        return services;
    }
}
