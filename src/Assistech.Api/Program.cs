using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assistech.Api.Auth;
using Assistech.Api.Endpoints;
using Assistech.Data;
using Assistech.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// O Render encerra o TLS e injeta a porta; sem isso o container sobe em 8080
// e o proxy nao encontra o processo.
var portaRender = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portaRender))
    builder.WebHost.UseUrls($"http://0.0.0.0:{portaRender}");

// O Render repassa X-Forwarded-Proto/Host. A lista e zerada de proposito:
// o container so e alcancavel pelo proxy, que ja knows a porta publica.
builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                            | ForwardedHeaders.XForwardedProto
                            | ForwardedHeaders.XForwardedHost;
    opcoes.KnownNetworks.Clear();
    opcoes.KnownProxies.Clear();
});


// ---------------------------------------------------------------- configuracao
builder.Services.Configure<SupabaseOptions>(builder.Configuration.GetSection(SupabaseOptions.SectionName));

var supabase = builder.Configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();

var faltando = new List<string>();
if (string.IsNullOrWhiteSpace(supabase.Url)) faltando.Add("Supabase:Url");
if (string.IsNullOrWhiteSpace(supabase.AnonKey)) faltando.Add("Supabase:AnonKey");
if (string.IsNullOrWhiteSpace(supabase.ConnectionString)) faltando.Add("Supabase:ConnectionString");
if (faltando.Count > 0)
{
    Console.Error.WriteLine($"Supabase nao configurado. Faltando: {string.Join(", ", faltando)}.");
    Console.Error.WriteLine("No Render, defina na aba Environment do servico (Supabase__Url, Supabase__AnonKey, Supabase__ConnectionString).");
    Console.Error.WriteLine("Local: variables de ambiente, appsettings.json ou user-secrets.");
    return 1;
}

builder.Services.AddAssistechData();


builder.Services.AddHttpClient<SupabaseAuthService>()
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(20));

builder.Services
    .AddAuthentication(SupabaseBearerHandlerDefaults.Scheme)
    .AddScheme<SupabaseAuthOptions, SupabaseBearerHandler>(SupabaseBearerHandlerDefaults.Scheme, _ => { });

builder.Services.AddAuthorization();

builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opcoes.SerializerOptions.PropertyNameCaseInsensitive = true;
    opcoes.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Assistech API",
        Version = "v1",
        Description = "API de ordens de servico para assistencias tecnicas de celulares, desktop e notebook."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Token emitido por POST /api/auth/login (Supabase Auth)."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

const string CorsDesktop = "desktop";
const string CorsWeb = "web";

builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy(CorsDesktop, p => p
        .WithOrigins("http://localhost:5100", "http://localhost:5101", "http://127.0.0.1:5100", "http://127.0.0.1:5101")
        .AllowAnyHeader()
        .AllowAnyMethod());

    opcoes.AddPolicy(CorsWeb, p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:Web").Get<string[]>() ?? Array.Empty<string>())
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// ---------------------------------------------------------------- pipeline
app.UseForwardedHeaders();
app.UseExceptionHandler(handler => handler.Run(async contexto =>
{
    var feature = contexto.Features.Get<IExceptionHandlerFeature>();
    var (status, mensagem) = feature?.Error switch
    {
        Npgsql.PostgresException pg when pg.SqlState == "23503" => (StatusCodes.Status409Conflict, "Operacao bloqueada por um registro vinculado."),
        Npgsql.PostgresException pg when pg.SqlState == "23505" => (StatusCodes.Status409Conflict, "Ja existe um registro com estes dados."),
        Npgsql.PostgresException => (StatusCodes.Status400BadRequest, "Nao foi possivel completar a operacao no banco de dados."),
        Npgsql.NpgsqlException => (StatusCodes.Status503ServiceUnavailable, "Banco de dados indisponivel."),
        InvalidOperationException => (StatusCodes.Status400BadRequest, feature.Error.Message),
        _ => (StatusCodes.Status500InternalServerError, "Erro inesperado na API.")
    };

    if (status >= 500)
        app.Logger.LogError(feature?.Error, "Falha nao tratada em {Path}", contexto.Request.Path);

    await contexto.Response.WriteAsJsonAsync(new { erro = mensagem });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Assistech API v1");
        c.DocumentTitle = "Assistech API";
    });
}

app.UseCors(app.Environment.IsDevelopment() ? CorsDesktop : CorsWeb);
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapConfigEndpoints();
app.MapClientesEndpoints();
app.MapEquipamentosEndpoints();
app.MapOrdensEndpoints();

// A raiz da API nao tem pagina. Sem isto, quem digita assistech.onrender.com
// no navegador leva um 404 e acha que o servico caiu.
app.MapGet("/", () => Results.Ok(new
{
    servico = "Assistech API",
    status = "ok",
    web = "https://assistech-web.onrender.com",
    diagnostico = new[] { "/health", "/health/db" }
}))
   .AllowAnonymous()
   .WithTags("Diagnostico");

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }))
   .AllowAnonymous()
   .WithTags("Diagnostico");

// O /health acima nao toca no banco. Este verifica de verdade a conexao com o
// Postgres: e o que distingue "API no ar" de "API no ar e conseguir falar com o
// Supabase" - falha de rede (host so IPv6, por exemplo) aparece aqui.
app.MapGet("/health/db", async (
    IAssistechDb db,
    IOptions<SupabaseOptions> supabase,
    CancellationToken ct) =>
{
    try
    {
        await using var conn = await db.OpenAsync(ct);
        await using var cmd = new Npgsql.NpgsqlCommand(
            "select current_user, current_database(), coalesce(inet_server_addr()::text, 'pooler')", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return Results.Ok(new
        {
            status = "ok",
            utc = DateTimeOffset.UtcNow,
            // Deixa claro qual projeto do Supabase esta em uso, sem expor segredo.
            supabase = new Uri(supabase.Value.Url).Host,
            banco = new
            {
                usuario = reader.GetString(0),
                nome = reader.GetString(1),
                endereco = reader.GetString(2)
            }
        });
    }
    catch (Exception erro)
    {
        app.Logger.LogError(erro, "Banco indisponivel.");
        return Results.Json(
            new { status = "erro", utc = DateTimeOffset.UtcNow },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
   .AllowAnonymous()
   .WithTags("Diagnostico");

// O schema e aplicado uma vez, na subida, quando Supabase:AplicarSchemaAutomaticamente = true.
if (supabase.AplicarSchemaAutomaticamente)
{
    try
    {
        using var escopo = app.Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<ISchemaInitializer>().AplicarAsync();
    }
    catch (Exception erro)
    {
        app.Logger.LogError(erro, "Falha ao aplicar o schema. Verifique a connection string e se db/001_schema.sql foi executado.");
    }
}


app.Run();
return 0;


public static class SupabaseBearerHandlerDefaults
{
    public const string Scheme = "Supabase";
}

public partial class Program;
