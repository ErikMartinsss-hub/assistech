using Assistech.Shared.Dtos;

namespace Assistech.Shared.Client;

public interface IAssistechApi
{
    Task<SessaoDto> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    SessaoDto? Sessao { get; }

    Task<EmpresaDto> ObterEmpresaAsync(CancellationToken ct = default);
    Task<EmpresaDto> AtualizarEmpresaAsync(AtualizarEmpresaRequest request, CancellationToken ct = default);
    Task EnviarLogoAsync(Stream conteudo, string nomeArquivo, CancellationToken ct = default);
    Task<EmpresaDto> RemoverLogoAsync(CancellationToken ct = default);

    Task<PagedResult<ClienteDto>> ListarClientesAsync(ClienteFiltro filtro, CancellationToken ct = default);
    Task<ClienteDto?> ObterClienteAsync(Guid id, CancellationToken ct = default);
    Task<ClienteDto> SalvarClienteAsync(ClienteInput input, CancellationToken ct = default);
    Task ExcluirClienteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<EquipamentoDto>> ListarEquipamentosDoClienteAsync(Guid clienteId, CancellationToken ct = default);
    Task<EquipamentoDto> SalvarEquipamentoAsync(EquipamentoInput input, CancellationToken ct = default);
    Task ExcluirEquipamentoAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<OrdemServicoResumo>> ListarOrdensAsync(OrdemServicoFiltro filtro, CancellationToken ct = default);
    Task<OrdemServicoDto?> ObterOrdemAsync(Guid id, CancellationToken ct = default);
    Task<OrdemServicoDto> SalvarOrdemAsync(OrdemServicoInput input, CancellationToken ct = default);
    Task AlterarStatusOrdemAsync(Guid id, AlterarStatusRequest request, CancellationToken ct = default);
    Task ExcluirOrdemAsync(Guid id, CancellationToken ct = default);
    Task<TermoOrdemServicoDto?> ObterTermoAsync(Guid id, CancellationToken ct = default);
}
