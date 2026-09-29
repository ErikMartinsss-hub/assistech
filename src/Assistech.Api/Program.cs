using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assistech.Api.Auth;
using Assistech.Api.Endpoints;
using Assistech.Data;
using Assistech.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuracao
builder.Services.Configure<SupabaseOptions>(builder.Configuration.GetSection(SupabaseOptions.SectionName));

var supabase = builder.Configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();
if (string.IsNullOrWhiteSpace(supabase.Url)
    || string.IsNullOrWhiteSpace(supabase.AnonKey)
    || string.IsNullOrWhiteSpace(supabase.ConnectionString))
{
    Console.Error.WriteLine("Supabase nao configurado. Defina Supabase:Url, Supabase:AnonKey e Supabase:ConnectionString em src/Assistech.Api/appsettings.json.");
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

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }))
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
