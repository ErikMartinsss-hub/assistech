using Assistech.Shared.Client;
using Assistech.Web;
using Assistech.Web.Components;
using Assistech.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AssistechWebOptions>(builder.Configuration.GetSection(AssistechWebOptions.SectionName));

builder.Services.AddScoped<SessaoState>();
builder.Services.AddScoped<AppAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddAuthentication(opcoes =>
    {
        opcoes.DefaultAuthenticateScheme = AutenticacaoHandler.Esquema;
        opcoes.DefaultChallengeScheme = AutenticacaoHandler.Esquema;
        opcoes.DefaultSignInScheme = AutenticacaoHandler.Esquema;
    })
    .AddScheme<AuthenticationSchemeOptions, AutenticacaoHandler>(AutenticacaoHandler.Esquema, _ => { });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("assistech-api", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

// Um cliente por circuito: o token fica na memoria do servidor.
builder.Services.AddScoped<IAssistechApi>(sp =>
{
    var opcoes = sp.GetRequiredService<IOptions<AssistechWebOptions>>().Value;
    var estado = sp.GetRequiredService<SessaoState>();
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("assistech-api");

    http.BaseAddress = new Uri(opcoes.ApiBaseUrl.TrimEnd('/') + "/");

    var api = new HttpAssistechApi(http, sessao =>
    {
        if (sessao is null) estado.Sair();
        else estado.Entrar(sessao);
    });

    estado.Reaplicar(api);
    return api;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/nao-encontrado");
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Proxy da logo: mantem a URL da API (que exige contexto de loja) fora do navegador.
app.MapGet("/logo/{empresaId:guid}", async (
    Guid empresaId,
    IHttpClientFactory factory,
    IOptions<AssistechWebOptions> opcoes,
    CancellationToken ct) =>
{
    var http = factory.CreateClient("assistech-api");

    try
    {
        using var resposta = await http.GetAsync(new Uri(new Uri(opcoes.Value.ApiBaseUrl.TrimEnd('/') + "/"), $"api/config/logo/{empresaId}"), ct);
        if (!resposta.IsSuccessStatusCode) return Results.NotFound();

        var tipo = resposta.Content.Headers.ContentType?.ToString() ?? "image/png";
        var bytes = await resposta.Content.ReadAsByteArrayAsync(ct);
        return Results.File(bytes, tipo);
    }
    catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
    {
        return Results.NotFound();
    }
});

app.Run();
