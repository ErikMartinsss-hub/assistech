namespace Assistech.Shared.Dtos;

/// <summary>
/// Dados ja formatados do Termo de Ordem de Servico.
/// O desktop (WPF) e a web (Blazor) renderizam o termo a partir deste contrato,
/// cada um com a tecnologia de impressao que preferir.
/// </summary>
public sealed class TermoOrdemServicoDto
{
    public Guid OrdemServicoId { get; set; }
    public int Numero { get; set; }

    public Guid EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = string.Empty;
    public string EmpresaDocumento { get; set; } = string.Empty;
    public string EmpresaTelefone { get; set; } = string.Empty;
    public string Whatsapp { get; set; } = string.Empty;
    public string EmpresaEndereco { get; set; } = string.Empty;
    public string EmpresaCorPrimaria { get; set; } = "#0d6efd";
    public bool EmpresaTemLogo { get; set; }
    public string EmpresaUrlLogo { get; set; } = string.Empty;

    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteDocumento { get; set; } = string.Empty;
    public string ClienteTelefone { get; set; } = string.Empty;
    public string ClienteEndereco { get; set; } = string.Empty;

    public string EquipamentoTipo { get; set; } = string.Empty;
    public string EquipamentoMarca { get; set; } = string.Empty;
    public string EquipamentoModelo { get; set; } = string.Empty;
    public string EquipamentoNumeroSerie { get; set; } = string.Empty;
    public string EquipamentoCor { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public string TipoServico { get; set; } = string.Empty;
    public string RelatoCliente { get; set; } = string.Empty;
    public string Diagnostico { get; set; } = string.Empty;
    public string LaudoConclusao { get; set; } = string.Empty;
    public string Acessorios { get; set; } = string.Empty;

    public DateTimeOffset DataEntrada { get; set; }
    public DateTimeOffset? DataPrevisao { get; set; }
    public DateTimeOffset? DataConclusao { get; set; }
    public int GarantiaDias { get; set; }

    public List<ItemOrdemServicoDto> Itens { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Desconto { get; set; }
    public decimal ValorTotal { get; set; }

    public DateTimeOffset EmitidoEm { get; set; }
    public string Responsavel { get; set; } = string.Empty;
}
