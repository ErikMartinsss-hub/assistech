using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Assistech.Shared.Dtos;

namespace Assistech.Shared.Client;

public sealed class ApiException : Exception
{
    public HttpStatusCode Status { get; }
    public IReadOnlyDictionary<string, string[]> Erros { get; }

    public ApiException(HttpStatusCode status, string mensagem, IReadOnlyDictionary<string, string[]>? erros = null)
        : base(mensagem)
    {
        Status = status;
        Erros = erros ?? new Dictionary<string, string[]>();
    }

    public string MensagemFormatada
    {
        get
        {
            if (Erros.Count == 0) return Message;
            var linhas = Erros.SelectMany(e => e.Value.Select(v => $"• {v}"));
            return string.Join(Environment.NewLine, new[] { Message }.Concat(linhas));
        }
    }
}

/// <summary>
/// Cliente HTTP da API Assistech, compartilhado entre o app desktop (WPF) e a web.
/// </summary>
public sealed class HttpAssistechApi : IAssistechApi
{
    private static readonly JsonSerializerOptions Json = AssistechJson.Options;

    private readonly HttpClient _http;
    private readonly Action<SessaoDto?>? _onSessaoAlterada;
    private readonly Func<SessaoDto?>? _sessaoProvider;

    public SessaoDto? Sessao { get; private set; }

    public HttpAssistechApi(HttpClient http, Action<SessaoDto?>? onSessaoAlterada = null, Func<SessaoDto?>? sessaoProvider = null)
    {
        _http = http;
        _onSessaoAlterada = onSessaoAlterada;
        _sessaoProvider = sessaoProvider;
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void RestaurarSessao(SessaoDto sessao) => Sessao = sessao;

    /// <summary>Sessao corrente: a do provedor (cookie) ou a guardada localmente.</summary>
    private SessaoDto? SessaoAtual => _sessaoProvider?.Invoke() ?? Sessao;

    // ------------------------------------------------------------------ infra

    private HttpRequestMessage Request(HttpMethod metodo, string caminho, bool autenticado = true)
    {
        var req = new HttpRequestMessage(metodo, caminho);
        if (autenticado && SessaoAtual is { } sessao)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao.Token);
        return req;
    }

    private async Task<T?> EnviarAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        using var resposta = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized && req.Headers.Authorization is not null)
            DefinirSessao(null);
        await TratarErroAsync(resposta).ConfigureAwait(false);
        if (resposta.StatusCode == HttpStatusCode.NoContent) return default;
        return await resposta.Content.ReadFromJsonAsync<T>(Json, ct).ConfigureAwait(false);
    }

    private static async Task TratarErroAsync(HttpResponseMessage resposta)
    {
        if (resposta.IsSuccessStatusCode) return;

        var corpo = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);
        var mensagem = resposta.ReasonPhrase ?? "Erro na comunicacao com o servidor.";

        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var root = doc.RootElement;

            if (root.TryGetProperty("erro", out var erro) && erro.ValueKind == JsonValueKind.String)
            {
                mensagem = erro.GetString() ?? mensagem;
            }
            else if (root.TryGetProperty("erros", out var erros))
            {
                var mapa = erros.Deserialize<Dictionary<string, string[]>>(Json);
                if (mapa is not null)
                    throw new ApiException(resposta.StatusCode, mensagem, mapa);
            }
            else if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                mensagem = msg.GetString() ?? mensagem;
            }
        }
        catch (JsonException)
        {
            // corpo nao-JSON: mantem o ReasonPhrase
        }

        throw new ApiException(resposta.StatusCode, mensagem);
    }

    private static string CorpoResumo(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo)) return "(corpo vazio)";
        var linhas = corpo.Replace("&quot;", "\"").Replace("\n", " ");
        return linhas.Length <= 220 ? linhas : linhas[..220];
    }

    // ------------------------------------------------------------------ auth

    public async Task<SessaoDto> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var req = Request(HttpMethod.Post, "api/auth/login", autenticado: false);
        req.Content = JsonContent.Create(request, options: Json);

        using var resposta = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);
            var mensagem = resposta.StatusCode == HttpStatusCode.Unauthorized
                ? "E-mail ou senha invalidos."
                : $"Nao foi possivel entrar. Verifique sua conexao. [HTTP {(int)resposta.StatusCode}] {CorpoResumo(corpo)}";
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                if (doc.RootElement.TryGetProperty("erro", out var e) && e.GetString() is { Length: > 0 } m)
                    mensagem = m;
            }
            catch (JsonException) { }
            throw new ApiException(resposta.StatusCode, mensagem);
        }

        var sessao = await resposta.Content.ReadFromJsonAsync<SessaoDto>(Json, ct).ConfigureAwait(false)
                     ?? throw new ApiException(resposta.StatusCode, "Resposta invalida do servidor.");

        DefinirSessao(sessao);
        return sessao;
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        var req = Request(HttpMethod.Post, "api/auth/logout");
        var task = EnviarAsync<object>(req, ct);
        DefinirSessao(null);
        return task;
    }

    private void DefinirSessao(SessaoDto? sessao)
    {
        Sessao = sessao;
        _onSessaoAlterada?.Invoke(sessao);
    }

    // ------------------------------------------------------------------ empresa / logo

    public async Task<EmpresaDto> ObterEmpresaAsync(CancellationToken ct = default)
    {
        var req = Request(HttpMethod.Get, "api/config/empresa");
        return await EnviarAsync<EmpresaDto>(req, ct).ConfigureAwait(false)
               ?? throw new ApiException(HttpStatusCode.NotFound, "Empresa nao encontrada.");
    }

    public async Task<EmpresaDto> AtualizarEmpresaAsync(AtualizarEmpresaRequest request, CancellationToken ct = default)
    {
        var req = Request(HttpMethod.Put, "api/config/empresa");
        req.Content = JsonContent.Create(request, options: Json);
        return await EnviarAsync<EmpresaDto>(req, ct).ConfigureAwait(false)
               ?? throw new ApiException(HttpStatusCode.NotFound, "Empresa nao encontrada.");
    }

    public async Task EnviarLogoAsync(Stream conteudo, string nomeArquivo, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new StreamContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(nomeArquivo) ? "image/png" : nomeArquivo);
        form.Add(arquivo, "logo", string.IsNullOrWhiteSpace(nomeArquivo) ? "logo.png" : Path.GetFileName(nomeArquivo));

        var req = Request(HttpMethod.Post, "api/config/logo");
        req.Content = form;
        await EnviarAsync<EmpresaDto>(req, ct).ConfigureAwait(false);
    }

    public async Task<EmpresaDto> RemoverLogoAsync(CancellationToken ct = default)
    {
        var req = Request(HttpMethod.Delete, "api/config/logo");
        return await EnviarAsync<EmpresaDto>(req, ct).ConfigureAwait(false)
               ?? throw new ApiException(HttpStatusCode.NotFound, "Empresa nao encontrada.");
    }

    // ------------------------------------------------------------------ clientes

    public async Task<PagedResult<ClienteDto>> ListarClientesAsync(ClienteFiltro filtro, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro.Busca)) query.Add($"busca={Uri.EscapeDataString(filtro.Busca)}");
        query.Add($"pagina={filtro.Pagina}");
        query.Add($"tamanhoPagina={filtro.TamanhoPagina}");

        var req = Request(HttpMethod.Get, $"api/clientes?{string.Join('&', query)}");
        return await EnviarAsync<PagedResult<ClienteDto>>(req, ct).ConfigureAwait(false)
               ?? new PagedResult<ClienteDto>();
    }

    public async Task<ClienteDto?> ObterClienteAsync(Guid id, CancellationToken ct = default)
        => await EnviarAsync<ClienteDto>(Request(HttpMethod.Get, $"api/clientes/{id}"), ct).ConfigureAwait(false);

    public async Task<ClienteDto> SalvarClienteAsync(ClienteInput input, CancellationToken ct = default)
    {
        var req = input.Id.HasValue
            ? Request(HttpMethod.Put, $"api/clientes/{input.Id}")
            : Request(HttpMethod.Post, "api/clientes");
        req.Content = JsonContent.Create(input, options: Json);

        return await EnviarAsync<ClienteDto>(req, ct).ConfigureAwait(false)
               ?? throw new ApiException(HttpStatusCode.InternalServerError, "Falha ao salvar cliente.");
    }

    public async Task ExcluirClienteAsync(Guid id, CancellationToken ct = default)
        => await EnviarAsync<object>(Request(HttpMethod.Delete, $"api/clientes/{id}"), ct).ConfigureAwait(false);

    // ------------------------------------------------------------------ equipamentos

    public async Task<IReadOnlyList<EquipamentoDto>> ListarEquipamentosDoClienteAsync(Guid clienteId, CancellationToken ct = default)
        => await EnviarAsync<List<EquipamentoDto>>(Request(HttpMethod.Get, $"api/equipamentos?clienteId={clienteId}"), ct).ConfigureAwait(false)
           ?? new List<EquipamentoDto>();

    public async Task<EquipamentoDto> SalvarEquipamentoAsync(EquipamentoInput input, CancellationToken ct = default)
    {
        var req = input.Id.HasValue
            ? Request(HttpMethod.Put, $"api/equipamentos/{input.Id}")
            : Request(HttpMethod.Post, "api/equipamentos");
        req.Content = JsonContent.Create(input, options: Json);

        return await EnviarAsync<EquipamentoDto>(req, ct).ConfigureAwait(false)
               ?? throw new ApiException(HttpStatusCode.InternalServerError, "Falha ao salvar equipamento.");
    }

    public async Task ExcluirEquipamentoAsync(Guid id, CancellationToken ct = default)
        => await EnviarAsync<object>(Request(HttpMethod.Delete, $"api/equipamentos/{id}"), ct).ConfigureAwait(false);

    // ------------------------------------------------------------------ ordens de servico

    public async Task<PagedResult<OrdemServicoResumo>> ListarOrdensAsync(OrdemServicoFiltro filtro, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro.Busca)) query.Add($"busca={Uri.EscapeDataString(filtro.Busca)}");
        if (!string.IsNullOrWhiteSpace(filtro.Status)) query.Add($"status={Uri.EscapeDataString(filtro.Status)}");
        if (filtro.ClienteId.HasValue) query.Add($"clienteId={filtro.ClienteId}");
        if (filtro.De.HasValue) query.Add($"de={Uri.EscapeDataString(filtro.De.Value.UtcDateTime.ToString("O"))}");
        if (filtro.Ate.HasValue) query.Add($"ate={Uri.EscapeDataString(filtro.Ate.Value.UtcDateTime.ToString("O"))}");
        query.Add($"pagina={filtro.Pagina}");
        query.Add($"tamanhoPagina={filtro.TamanhoPagina}");

        var req = Request(HttpMethod.Get, $"api/ordens?{string.Join('&', query)}");
        return await EnviarAsync<PagedResult<OrdemServicoResumo>>(req, ct).ConfigureAwait(false)
               ?? new PagedResult<OrdemServicoResumo>();
    }

    public async Task<OrdemServicoDto?> ObterOrdemAsync(Guid id, CancellationToken ct = default)
        => await EnviarAsync<OrdemServicoDto>(Request(HttpMethod.Get, $"api/ordens/{id}"), ct).ConfigureAwait(false);

    public async Task<OrdemServicoDto> SalvarOrdemAsync(OrdemServicoInput input, CancellationToken ct = default)
    {
        var req = input.Id.HasValue
            ? Request(HttpMethod.Put, $"api/ordens/{input.Id}")
            : Request(HttpMethod.Post, "api/ordens");
        req.Content = JsonContent.Create(input, options: Json);

        return await EnviarAsync<OrdemServicoDto>(req, ct).ConfigureAwait(false)
               ?? throw new ApiException(HttpStatusCode.InternalServerError, "Falha ao salvar ordem de servico.");
    }

    public async Task AlterarStatusOrdemAsync(Guid id, AlterarStatusRequest request, CancellationToken ct = default)
    {
        var req = Request(HttpMethod.Post, $"api/ordens/{id}/status");
        req.Content = JsonContent.Create(request, options: Json);
        await EnviarAsync<OrdemServicoDto>(req, ct).ConfigureAwait(false);
    }

    public async Task ExcluirOrdemAsync(Guid id, CancellationToken ct = default)
        => await EnviarAsync<object>(Request(HttpMethod.Delete, $"api/ordens/{id}"), ct).ConfigureAwait(false);

    public async Task<TermoOrdemServicoDto?> ObterTermoAsync(Guid id, CancellationToken ct = default)
        => await EnviarAsync<TermoOrdemServicoDto>(Request(HttpMethod.Get, $"api/ordens/{id}/termo"), ct).ConfigureAwait(false);
}
