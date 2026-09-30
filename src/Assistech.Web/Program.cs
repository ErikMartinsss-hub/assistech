using Assistech.Shared.Client;
using Assistech.Web;
using Assistech.Web.Components;
using Assistech.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// O Render encerra o TLS e injeta a porta. Sem os forwarded headers, o
// UseHttpsRedredirect abaixo fica em laco: o app "ve" http e redireciona
// para https, o Render devolve http de novo.
var portaRender = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(portaRender))
    builder.WebHost.UseUrls($"http://0.0.0.0:{portaRender}");

builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
{
    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                            | ForwardedHeaders.XForwardedProto
                            | ForwardedHeaders.XForwardedHost;
    opcoes.KnownNetworks.Clear();
    opcoes.KnownProxies.Clear();
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<AssistechWebOptions>(builder.Configuration.GetSection(AssistechWebOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SessaoState>();
builder.Services.AddScoped<SessaoCookieCliente>();
builder.Services.AddCascadingAuthenticationState();

// O cookie e a fonte da verdade da sessao: ele existe no prerender (via
// HttpContext) e tambem na abertura do circuito (o WebSocket carrega o
// cookie). Antes a sessao vivia so na memoria do circuito e um F5 deslogava.
builder.Services.AddScoped<ServerAuthenticationStateProvider>();
builder.Services.AddScoped<AppAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AppAuthenticationStateProvider>());
builder.Services.AddScoped<IHostEnvironmentAuthenticationStateProvider>(sp => sp.GetRequiredService<AppAuthenticationStateProvider>());

builder.Services
    .AddAuthentication(opcoes => opcoes.DefaultScheme = SessaoCookie.Esquema)
    .AddCookie(SessaoCookie.Esquema, opcoes =>
    {
        opcoes.Cookie.Name = SessaoCookie.Nome;
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Lax;
        opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opcoes.LoginPath = "/entrar";
        opcoes.LogoutPath = "/entrar";
        opcoes.AccessDeniedPath = "/entrar";
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(12);
        opcoes.SlidingExpiration = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("assistech-api", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddHttpClient(SessaoCookieCliente.Cliente, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Um cliente por circuito: o token sai do cookie, nao da memoria do circuito.
builder.Services.AddScoped<IAssistechApi>(sp =>
{
    var opcoes = sp.GetRequiredService<IOptions<AssistechWebOptions>>().Value;
    var estado = sp.GetRequiredService<SessaoState>();
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("assistech-api");

    http.BaseAddress = new Uri(opcoes.ApiBaseUrl.TrimEnd('/') + "/");

    return new HttpAssistechApi(
        http,
        sessao => { if (sessao is null) estado.Sair(); },
        () => estado.Atual);
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();

    // Atrás de um proxy que termina o TLS (Render, Azure, Nginx), o
    // UseHttpsRedirection não descobre a porta HTTPS e loga
    // "Failed to determine the https port" toda requisição. Como os
    // forwarded headers já Configure, IsHttps reflete o protocolo real.
    app.Use(async (contexto, proximo) =>
    {
        if (!contexto.Request.IsHttps)
        {
            var alvo = $"{contexto.Request.Scheme}://{contexto.Request.Host}{contexto.Request.PathBase}{contexto.Request.Path}{contexto.Request.QueryString}";
            contexto.Response.Redirect($"https://{alvo.Split("://", 2)[1]}");
            return;
        }

        await proximo();
    });
}

app.UseStatusCodePagesWithReExecute("/nao-encontrado");
app.UseStaticFiles();

// Sem estes dois, nada popula HttpContext.User: o cookie era emitido pelo
// /sessao/entrar e nunca lido, entao toda pagina voltava para /entrar.
app.UseAuthentication();
app.UseAuthorization();

// O prerender com streaming zera o HttpContext antes dos componentes rodarem,
// entao o SessaoState nao consegue ler o cookie de la. A sessao e lida aqui e
// injetada no escopo da requisicao, que o render compartilha.
app.Use(async (contexto, proximo) =>
{
    if (contexto.User.Identity?.IsAuthenticated == true)
    {
        var sessao = SessaoCookie.LerPrincipal(contexto.User);
        if (sessao is not null)
            contexto.RequestServices.GetRequiredService<SessaoState>().Entrar(sessao);
    }

    await proximo();
});

app.UseAntiforgery();

// As paginas usam [Authorize], que o .NET 8 transforma em metadado do
// endpoint. Como a sessao vive dentro do circuito do Blazor (nunca no
// prerender estatico), a autorizacao por HTTP rejeitaria toda pagina
// protegida com corpo vazio - tela branca. AllowAnonymous desliga essa
// camada e deixa a checagem com o AuthorizeRouteView, no circuito.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.MapSessaoEndpoints();

app.MapGet("/__config", (IOptions<AssistechWebOptions> opcoes) => Results.Ok(new
{
    apiBaseUrl = opcoes.Value.ApiBaseUrl,
    apelido = opcoes.Value.ApiBaseUrl.Replace("https://", "").Replace("http://", ""),
    ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "nulo"
}));

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
