using System.ComponentModel.DataAnnotations;

namespace Assistech.Shared.Dtos;

// ---------------------------------------------------------------- Auth

public sealed record LoginRequest(string Email, string Senha);

public sealed class SessaoDto
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiraEm { get; set; }
    public Guid UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Perfil { get; set; } = "tecnico";
    public Guid EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = string.Empty;
    public string EmpresaCorPrimaria { get; set; } = "#0d6efd";
}

public sealed record TrocarSenhaRequest(string SenhaAtual, string NovaSenha);

// ---------------------------------------------------------------- Empresa / Logo

public sealed class EmpresaDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string WhatsApp { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string CorPrimaria { get; set; } = "#0d6efd";
    public bool TemLogo { get; set; }
    public string UrlLogo { get; set; } = string.Empty;
}

public sealed class AtualizarEmpresaRequest
{
    [MaxLength(160)] public string Nome { get; set; } = string.Empty;
    [MaxLength(32)] public string Documento { get; set; } = string.Empty;
    [MaxLength(32)] public string Telefone { get; set; } = string.Empty;
    [MaxLength(32)] public string WhatsApp { get; set; } = string.Empty;
    [MaxLength(160)] public string Email { get; set; } = string.Empty;
    [MaxLength(240)] public string Endereco { get; set; } = string.Empty;
    [RegularExpression("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Cor deve estar no formato #RRGGBB")]
    public string CorPrimaria { get; set; } = "#0d6efd";
}

// ---------------------------------------------------------------- Clientes

public sealed class ClienteDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
    public int TotalEquipamentos { get; set; }
    public int TotalOrdens { get; set; }
}

public sealed class ClienteInput
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do cliente")]
    [MaxLength(160)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(32)] public string Documento { get; set; } = string.Empty;
    [MaxLength(32)] public string Telefone { get; set; } = string.Empty;
    [MaxLength(160)] public string Email { get; set; } = string.Empty;
    [MaxLength(240)] public string Endereco { get; set; } = string.Empty;
    [MaxLength(2000)] public string Observacoes { get; set; } = string.Empty;
}

public sealed class ClienteFiltro
{
    public string? Busca { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanhoPagina { get; set; } = 25;
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Itens { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
    public int Pagina { get; init; }
    public int TamanhoPagina { get; init; }
    public int TotalPaginas => TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoPagina);
}

// ---------------------------------------------------------------- Equipamentos

public sealed class EquipamentoDto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string NumeroSerie { get; set; } = string.Empty;
    public string Cor { get; set; } = string.Empty;
    public int? Ano { get; set; }
    public string Observacoes { get; set; } = string.Empty;
    public int TotalOrdens { get; set; }
}

public sealed class EquipamentoInput
{
    public Guid? Id { get; set; }
    public Guid ClienteId { get; set; }

    [MaxLength(24)] public string Tipo { get; set; } = "celular";
    [Required(ErrorMessage = "Informe a marca")]
    [MaxLength(80)] public string Marca { get; set; } = string.Empty;
    [Required(ErrorMessage = "Informe o modelo")]
    [MaxLength(120)] public string Modelo { get; set; } = string.Empty;
    [MaxLength(80)] public string NumeroSerie { get; set; } = string.Empty;
    [MaxLength(60)] public string Cor { get; set; } = string.Empty;
    public int? Ano { get; set; }
    [MaxLength(2000)] public string Observacoes { get; set; } = string.Empty;
}

// ---------------------------------------------------------------- Ordens de Servico

public sealed class OrdemServicoDto
{
    public Guid Id { get; set; }
    public int Numero { get; set; }

    public Guid ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteTelefone { get; set; } = string.Empty;

    public Guid EquipamentoId { get; set; }
    public string EquipamentoDescricao { get; set; } = string.Empty;

    public string Status { get; set; } = "aberta";
    public string TipoServico { get; set; } = string.Empty;
    public string RelatoCliente { get; set; } = string.Empty;
    public string Diagnostico { get; set; } = string.Empty;
    public string LaudoConclusao { get; set; } = string.Empty;
    public string SenhaEquipamento { get; set; } = string.Empty;
    public string Acessorios { get; set; } = string.Empty;

    public decimal? OrcamentoValor { get; set; }
    public decimal CustoPecas { get; set; }
    public decimal Desconto { get; set; }
    public int GarantiaDias { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal LucroEstimado { get; set; }

    public DateTimeOffset DataEntrada { get; set; }
    public DateTimeOffset? DataPrevisao { get; set; }
    public DateTimeOffset? DataConclusao { get; set; }
    public bool Atrasada { get; set; }

    public string CriadoPor { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }

    public List<ItemOrdemServicoDto> Itens { get; set; } = new();
    public List<HistoricoOrdemServicoDto> Historico { get; set; } = new();
}

public sealed class OrdemServicoResumo
{
    public Guid Id { get; set; }
    public int Numero { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteTelefone { get; set; } = string.Empty;
    public string Equipamento { get; set; } = string.Empty;
    public string Status { get; set; } = "aberta";
    public decimal ValorTotal { get; set; }
    public DateTimeOffset DataEntrada { get; set; }
    public DateTimeOffset? DataPrevisao { get; set; }
    public bool Atrasada { get; set; }
}

public sealed class OrdemServicoInput
{
    public Guid? Id { get; set; }
    public Guid? ClienteId { get; set; }
    public Guid? EquipamentoId { get; set; }

    [MaxLength(120)] public string Status { get; set; } = "aberta";
    [MaxLength(160)] public string TipoServico { get; set; } = string.Empty;
    [MaxLength(4000)] public string RelatoCliente { get; set; } = string.Empty;
    [MaxLength(4000)] public string Diagnostico { get; set; } = string.Empty;
    [MaxLength(4000)] public string LaudoConclusao { get; set; } = string.Empty;
    [MaxLength(120)] public string SenhaEquipamento { get; set; } = string.Empty;
    [MaxLength(1000)] public string Acessorios { get; set; } = string.Empty;
    public decimal? OrcamentoValor { get; set; }
    public decimal Desconto { get; set; }
    public int GarantiaDias { get; set; } = 90;
    public DateTimeOffset? DataPrevisao { get; set; }
    public List<ItemOrdemServicoInput> Itens { get; set; } = new();
}

public sealed class ItemOrdemServicoDto
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = "servico";
    public string Descricao { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal CustoUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public sealed class ItemOrdemServicoInput
{
    public Guid? Id { get; set; }
    [MaxLength(24)] public string Tipo { get; set; } = "servico";
    [Required(ErrorMessage = "Informe a descricao do item")]
    [MaxLength(240)] public string Descricao { get; set; } = string.Empty;
    public decimal Quantidade { get; set; } = 1;
    public decimal ValorUnitario { get; set; }
    public decimal CustoUnitario { get; set; }
}

public sealed class AlterarStatusRequest
{
    [Required] public string Status { get; set; } = string.Empty;
    [MaxLength(1000)] public string Comentario { get; set; } = string.Empty;
}

public sealed class HistoricoOrdemServicoDto
{
    public string StatusAnterior { get; set; } = string.Empty;
    public string StatusNovo { get; set; } = string.Empty;
    public string Comentario { get; set; } = string.Empty;
    public string UsuarioNome { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
}

public sealed class OrdemServicoFiltro
{
    public string? Busca { get; set; }
    public string? Status { get; set; }
    public Guid? ClienteId { get; set; }
    public DateTimeOffset? De { get; set; }
    public DateTimeOffset? Ate { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanhoPagina { get; set; } = 25;
}
