using System.Collections.ObjectModel;
using Assistech.Desktop.Services;
using Assistech.Shared.Client;
using Assistech.Shared.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Assistech.Desktop.ViewModels;

public sealed class ItemOrdem : ObservableObject
{
    public ItemOrdemServicoInput Modelo { get; }
    public ItemOrdem() : this(new ItemOrdemServicoInput()) { }

    public ItemOrdem(ItemOrdemServicoInput modelo)
    {
        Modelo = modelo;
    }

    public string Descricao
    {
        get => Modelo.Descricao;
        set { Modelo.Descricao = value; NotificarTotal(); }
    }

    public decimal Quantidade
    {
        get => Modelo.Quantidade;
        set { Modelo.Quantidade = value; NotificarTotal(); }
    }

    public decimal ValorUnitario
    {
        get => Modelo.ValorUnitario;
        set { Modelo.ValorUnitario = value; NotificarTotal(); }
    }

    public string Tipo => Modelo.Tipo;

    public decimal Subtotal => Quantidade * ValorUnitario;

    private void NotificarTotal() => OnPropertyChanged(nameof(Subtotal));
}

public sealed partial class ItemEquipamento : ObservableObject
{
    public required EquipamentoDto Modelo { get; init; }
    public Guid Id => Modelo.Id;
    public string Titulo => $"{Modelo.Tipo} - {Modelo.Marca} {Modelo.Modelo}".Trim();

    [ObservableProperty] private bool _isSelected;
}

public partial class OrdemEditorViewModel : PaginaBase
{
    private readonly MarcaService _marca;

    [ObservableProperty] private OrdemServicoDto? _ordem;
    [ObservableProperty] private string _titulo = "Nova ordem de servico";
    [ObservableProperty] private bool _salvo;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _erro = string.Empty;

    [ObservableProperty] private string _clienteBusca = string.Empty;
    [ObservableProperty] private ClienteDto? _clienteSelecionado;
    [ObservableProperty] private ItemEquipamento? _equipamentoSelecionado;
    [ObservableProperty] private string _equipamentoFiltro = "Todos";

    [ObservableProperty] private string _status = "aberta";
    [ObservableProperty] private string _tipoServico = string.Empty;
    [ObservableProperty] private string _relato = string.Empty;
    [ObservableProperty] private string _diagnostico = string.Empty;
    [ObservableProperty] private string _laudo = string.Empty;
    [ObservableProperty] private string _senhaEquipamento = string.Empty;
    [ObservableProperty] private string _acessorios = string.Empty;
    [ObservableProperty] private string _orcamento = string.Empty;
    [ObservableProperty] private string _desconto = string.Empty;
    [ObservableProperty] private string _garantia = "90";
    [ObservableProperty] private string _previsao = string.Empty;

    public ObservableCollection<ClienteDto> ResultadosClientes { get; } = new();
    public ObservableCollection<ItemEquipamento> Equipamentos { get; } = new();
    public ObservableCollection<ItemOrdem> Itens { get; } = new();
    public ObservableCollection<HistoricoOrdemServicoDto> Historico { get; } = new();

    public IReadOnlyList<StatusOption> StatusDisponiveis { get; } = StatusOption.Todos;

    public bool EhEdicao => Ordem is not null;

    public string Resumo => Ordem is null
        ? "Preencha os dados e salve para gerar o numero da OS."
        : $"OS #{Ordem.Numero:0000} - criada em {Ordem.DataEntrada:dd/MM/yyyy}";

    public decimal Subtotal => Itens.Sum(i => i.Subtotal);

    public decimal DescontoValor => decimal.TryParse(Desconto, out var d) ? d : 0m;

    public decimal CustoPecas
    {
        get
        {
            var original = Ordem?.CustoPecas ?? 0m;
            return original;
        }
    }

    public decimal Total => Math.Max(0m, Subtotal - DescontoValor);

    public decimal OrcamentoValor
    {
        get
        {
            if (Ordem?.OrcamentoValor is { } v) return v;
            return Total;
        }
    }

    public OrdemEditorViewModel(IAssistechApi api, SessaoDesktop sessao, MarcaService marca) : base(api, sessao)
    {
        _marca = marca;
        Adicionar(new ItemOrdem { Descricao = string.Empty, Quantidade = 1 });
    }

    private void Adicionar(ItemOrdem item)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ItemOrdem.Subtotal) or nameof(ItemOrdem.Quantidade) or nameof(ItemOrdem.ValorUnitario))
                ItemAlterado();
        };

        Itens.Add(item);
        ItemAlterado();
    }

    public async Task InicializarAsync(Guid? ordemId)
    {
        if (ordemId is { } id)
        {
            Ocupado = true;
            try
            {
                var ordem = await Api.ObterOrdemAsync(id);
                if (ordem is not null)
                {
                    Ordem = ordem;
                    Preencher(ordem);
                }
            }
            catch (ApiException e)
            {
                Erro = e.MensagemFormatada;
            }
            finally
            {
                Ocupado = false;
            }
        }
        else
        {
            await BuscarClientesAsync();
        }
    }

    private void Preencher(OrdemServicoDto ordem)
    {
        Titulo = $"Ordem de servico #{ordem.Numero:0000}";
        ClienteSelecionado = new ClienteDto
        {
            Id = ordem.ClienteId,
            Nome = ordem.ClienteNome,
            Telefone = ordem.ClienteTelefone
        };
        ClienteBusca = ordem.ClienteNome;
        Status = ordem.Status;
        TipoServico = ordem.TipoServico;
        Relato = ordem.RelatoCliente;
        Diagnostico = ordem.Diagnostico;
        Laudo = ordem.LaudoConclusao;
        SenhaEquipamento = ordem.SenhaEquipamento;
        Acessorios = ordem.Acessorios;
        Orcamento = ordem.OrcamentoValor?.ToString("0.00") ?? string.Empty;
        Desconto = ordem.Desconto.ToString("0.00");
        Garantia = ordem.GarantiaDias.ToString();
        Previsao = ordem.DataPrevisao?.ToString("yyyy-MM-dd") ?? string.Empty;

        Itens.Clear();
        if (ordem.Itens.Count == 0)
        {
            Adicionar(new ItemOrdem());
        }
        else
        {
            foreach (var item in ordem.Itens)
                Adicionar(new ItemOrdem(new ItemOrdemServicoInput
                {
                    Id = item.Id,
                    Tipo = item.Tipo,
                    Descricao = item.Descricao,
                    Quantidade = item.Quantidade,
                    ValorUnitario = item.ValorUnitario,
                    CustoUnitario = item.CustoUnitario
                }));
        }

        Historico.Clear();
        foreach (var h in ordem.Historico)
            Historico.Add(h);

        _ = CarregarEquipamentosAsync(ordem.ClienteId, ordem.EquipamentoId);
    }

    partial void OnClienteBuscaChanged(string value) => _ = BuscarClientesAsync();

    partial void OnClienteSelecionadoChanged(ClienteDto? value)
    {
        if (value is null) return;
        ClienteBusca = value.Nome;
        Equipamentos.Clear();
        EquipamentoSelecionado = null;
        _ = CarregarEquipamentosAsync(value.Id, null);
    }

    partial void OnDescontoChanged(string value) => OnPropertyChanged(nameof(DescontoValor));
    partial void OnOrcamentoChanged(string value) => OnPropertyChanged(nameof(OrcamentoValor));
    partial void OnOcupadoChanged(bool value) => SalvarCommand.NotifyCanExecuteChanged();

    public void ItemAlterado()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Total));
    }

    private async Task BuscarClientesAsync()
    {
        if (ClienteSelecionado is not null) return;

        try
        {
            var pagina = await Api.ListarClientesAsync(new ClienteFiltro
            {
                Busca = string.IsNullOrWhiteSpace(ClienteBusca) ? null : ClienteBusca,
                TamanhoPagina = 8
            });

            ResultadosClientes.Clear();
            foreach (var c in pagina.Itens)
                ResultadosClientes.Add(c);
        }
        catch (ApiException)
        {
            ResultadosClientes.Clear();
        }
    }

    private async Task CarregarEquipamentosAsync(Guid clienteId, Guid? selecionado)
    {
        try
        {
            var lista = await Api.ListarEquipamentosDoClienteAsync(clienteId);
            Equipamentos.Clear();
            foreach (var e in lista)
                Equipamentos.Add(new ItemEquipamento { Modelo = e });

            EquipamentoSelecionado = selecionado is { } sid
                ? Equipamentos.FirstOrDefault(e => e.Id == sid)
                : Equipamentos.FirstOrDefault();
        }
        catch (ApiException)
        {
            Equipamentos.Clear();
        }
    }

    partial void OnEquipamentoSelecionadoChanged(ItemEquipamento? value)
    {
        foreach (var e in Equipamentos)
            e.IsSelected = ReferenceEquals(e, value);
    }

    public void SelecionarEquipamentoPorId(Guid id)
    {
        var alvo = Equipamentos.FirstOrDefault(e => e.Id == id);
        if (alvo is not null)
            EquipamentoSelecionado = alvo;
    }

    [RelayCommand]
    private void AdicionarItem() => Adicionar(new ItemOrdem { Descricao = string.Empty, Quantidade = 1 });

    [RelayCommand]
    private void RemoverItem(ItemOrdem item)
    {
        if (Itens.Count <= 1)
        {
            item.Descricao = string.Empty;
            item.Quantidade = 1;
            item.ValorUnitario = 0;
            ItemAlterado();
            return;
        }

        Itens.Remove(item);
        ItemAlterado();
    }

    [RelayCommand]
    private void LimparCliente()
    {
        ClienteSelecionado = null;
        ClienteBusca = string.Empty;
        ResultadosClientes.Clear();
        Equipamentos.Clear();
        EquipamentoSelecionado = null;
    }

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    private async Task SalvarAsync()
    {
        Ocupado = true;
        Erro = string.Empty;

        try
        {
            var input = new OrdemServicoInput
            {
                Id = Ordem?.Id,
                ClienteId = ClienteSelecionado?.Id,
                EquipamentoId = EquipamentoSelecionado?.Id,
                Status = Status,
                TipoServico = TipoServico.Trim(),
                RelatoCliente = Relato.Trim(),
                Diagnostico = Diagnostico.Trim(),
                LaudoConclusao = Laudo.Trim(),
                SenhaEquipamento = SenhaEquipamento.Trim(),
                Acessorios = Acessorios.Trim(),
                OrcamentoValor = decimal.TryParse(Orcamento, out var orc) ? orc : null,
                Desconto = DescontoValor,
                GarantiaDias = int.TryParse(Garantia, out var g) ? g : 0,
                DataPrevisao = DateTimeOffset.TryParse(Previsao, out var p) ? p : null,
                Itens = Itens
                    .Where(i => !string.IsNullOrWhiteSpace(i.Descricao))
                    .Select(i => new ItemOrdemServicoInput
                    {
                        Id = i.Modelo.Id,
                        Tipo = i.Tipo,
                        Descricao = i.Descricao.Trim(),
                        Quantidade = i.Quantidade,
                        ValorUnitario = i.ValorUnitario,
                        CustoUnitario = i.Modelo.CustoUnitario
                    })
                    .ToList()
            };

            var salva = await Api.SalvarOrdemAsync(input);
            Ordem = salva;
            Preencher(salva);
            Salvo = true;
        }
        catch (ApiException e)
        {
            Erro = e.MensagemFormatada;
        }
        finally
        {
            Ocupado = false;
        }
    }

    public bool PodeSalvar => !Ocupado && ClienteSelecionado is not null;
}

public sealed record StatusOption(string Valor, string Descricao)
{
    public static IReadOnlyList<StatusOption> Todos { get; } = new[]
    {
        new StatusOption("aberta", "Aberta"),
        new StatusOption("em_diagnostico", "Em diagnostico"),
        new StatusOption("aguardando_pecas", "Aguardando pecas"),
        new StatusOption("aguardando_cliente", "Aguardando cliente"),
        new StatusOption("em_execucao", "Em execucao"),
        new StatusOption("concluida", "Concluida"),
        new StatusOption("entregue", "Entregue"),
        new StatusOption("cancelada", "Cancelada")
    };
}
