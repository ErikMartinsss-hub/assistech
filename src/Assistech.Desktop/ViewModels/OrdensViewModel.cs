using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Media;
using Assistech.Desktop.Services;
using Assistech.Desktop.Views;
using Assistech.Shared.Client;
using Assistech.Shared.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Assistech.Desktop.ViewModels;

public sealed class LinhaOrdem
{
    public required OrdemServicoResumo Modelo { get; init; }
    public Guid Id => Modelo.Id;
    public string Numero => $"#{Modelo.Numero:0000}";
    public string Cliente => Modelo.ClienteNome;
    public string Telefone => Modelo.ClienteTelefone;
    public string Equipamento => Modelo.Equipamento;
    public string Status => StatusTexto.Escrever(Modelo.Status);
    public decimal Total => Modelo.ValorTotal;
    public string Entrada => Modelo.DataEntrada.ToString("dd/MM/yyyy");
    public string Previsao => Modelo.DataPrevisao?.ToString("dd/MM/yyyy") ?? "-";
    public string Prazo => Modelo.Atrasada ? "Atrasada" : Previsao;
    public bool Atrasada => Modelo.Atrasada;
}

public static class StatusTexto
{
    private static readonly Dictionary<string, string> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aberta"] = "Aberta",
        ["em_diagnostico"] = "Em diagnostico",
        ["aguardando_pecas"] = "Aguardando pecas",
        ["aguardando_cliente"] = "Aguardando cliente",
        ["em_execucao"] = "Em execucao",
        ["concluida"] = "Concluida",
        ["entregue"] = "Entregue",
        ["cancelada"] = "Cancelada"
    };

    public static string Escrever(string status) => Mapa.TryGetValue(status, out var texto) ? texto : status;

    public static Brush Cor(string status) => status switch
    {
        "aberta" => new SolidColorBrush(Color.FromRgb(13, 110, 253)),
        "em_diagnostico" => new SolidColorBrush(Color.FromRgb(13, 202, 240)),
        "aguardando_pecas" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
        "aguardando_cliente" => new SolidColorBrush(Color.FromRgb(255, 128, 8)),
        "em_execucao" => new SolidColorBrush(Color.FromRgb(108, 92, 231)),
        "concluida" => new SolidColorBrush(Color.FromRgb(25, 135, 84)),
        "entregue" => new SolidColorBrush(Color.FromRgb(13, 148, 136)),
        "cancelada" => new SolidColorBrush(Color.FromRgb(108, 117, 125)),
        _ => Brushes.SlateGray
    };

    public static System.Windows.Media.Brush Fundo(string status) => status switch
    {
        "aberta" => new SolidColorBrush(Color.FromRgb(231, 241, 255)),
        "em_diagnostico" => new SolidColorBrush(Color.FromRgb(222, 248, 255)),
        "aguardando_pecas" => new SolidColorBrush(Color.FromRgb(255, 249, 219)),
        "aguardando_cliente" => new SolidColorBrush(Color.FromRgb(255, 243, 224)),
        "em_execucao" => new SolidColorBrush(Color.FromRgb(238, 235, 255)),
        "concluida" => new SolidColorBrush(Color.FromRgb(224, 247, 236)),
        "entregue" => new SolidColorBrush(Color.FromRgb(222, 244, 241)),
        _ => new SolidColorBrush(Color.FromRgb(240, 242, 245))
    };
}

public partial class OrdensViewModel : PaginaBase
{
    private readonly MarcaService _marca;

    [ObservableProperty] private string _busca = string.Empty;
    [ObservableProperty] private string _statusFiltro = "";
    [ObservableProperty] private int _pagina = 1;
    [ObservableProperty] private int _totalPaginas;
    [ObservableProperty] private int _total;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _erro = string.Empty;
    [ObservableProperty] private bool _carregado;
    [ObservableProperty] private LinhaOrdem? _ordemSelecionada;

    public OrdensViewModel(IAssistechApi api, SessaoDesktop sessao, MarcaService marca) : base(api, sessao)
    {
        _marca = marca;
    }

    public ObservableCollection<LinhaOrdem> Ordens { get; } = new();

    public IReadOnlyList<StatusOption> Filtros { get; } = new[]
    {
        new StatusOption("", "Todos os status"),
        new StatusOption("aberta", "Abertas"),
        new StatusOption("em_diagnostico", "Em diagnostico"),
        new StatusOption("aguardando_pecas", "Aguardando pecas"),
        new StatusOption("aguardando_cliente", "Aguardando cliente"),
        new StatusOption("em_execucao", "Em execucao"),
        new StatusOption("concluida", "Concluidas"),
        new StatusOption("entregue", "Entregues"),
        new StatusOption("cancelada", "Canceladas")
    };

    public bool TemOrdens => Ordens.Count > 0;

    public string Resumo => Carregado
        ? $"{Total} ordem(ns) - pagina {Pagina} de {Math.Max(TotalPaginas, 1)}"
        : "Carregando ordens...";

    public bool PodeAvancar => Pagina < TotalPaginas;
    public bool PodeVoltar => Pagina > 1;

    partial void OnBuscaChanged(string value) => _ = BuscarAsync();

    partial void OnStatusFiltroChanged(string value)
    {
        Pagina = 1;
        _ = BuscarAsync();
    }

    partial void OnPaginaChanged(int value) => _ = BuscarAsync();

    public void Inicializar() => _ = BuscarAsync();

    [RelayCommand]
    private async Task BuscarAsync()
    {
        Ocupado = true;
        Erro = string.Empty;

        try
        {
            var resultado = await Api.ListarOrdensAsync(new OrdemServicoFiltro
            {
                Busca = string.IsNullOrWhiteSpace(Busca) ? null : Busca,
                Status = string.IsNullOrWhiteSpace(StatusFiltro) ? null : StatusFiltro,
                Pagina = Pagina,
                TamanhoPagina = 20
            });

            Ordens.Clear();
            foreach (var o in resultado.Itens)
                Ordens.Add(new LinhaOrdem { Modelo = o });

            Total = resultado.Total;
            TotalPaginas = resultado.TotalPaginas;
            Carregado = true;
        }
        catch (ApiException e)
        {
            Erro = e.MensagemFormatada;
            Carregado = true;
        }
        catch (HttpRequestException)
        {
            Erro = "Sem conexao com a API.";
            Carregado = true;
        }
        finally
        {
            Ocupado = false;
            NotificarListagem();
        }
    }

    private void NotificarListagem()
    {
        OnPropertyChanged(nameof(TemOrdens));
        OnPropertyChanged(nameof(Resumo));
        OnPropertyChanged(nameof(PodeAvancar));
        OnPropertyChanged(nameof(PodeVoltar));
    }

    [RelayCommand]
    private void ProximaPagina()
    {
        if (PodeAvancar) Pagina++;
    }

    [RelayCommand]
    private void PaginaAnterior()
    {
        if (PodeVoltar) Pagina--;
    }

    [RelayCommand]
    private void NovaOrdem()
    {
        var vm = App.Criar<OrdemEditorViewModel>();
        var janela = new OrdemEditorWindow(vm);
        if (janela.ShowDialog() == true)
            _ = BuscarAsync();
    }

    public async Task AbrirOrdemAsync(LinhaOrdem linha)
    {
        var vm = App.Criar<OrdemEditorViewModel>();
        var janela = new OrdemEditorWindow(vm);
        await vm.InicializarAsync(linha.Id);
        janela.ShowDialog();
        _ = BuscarAsync();
    }

    public async Task ImprimirTermoAsync(LinhaOrdem linha)
    {
        try
        {
            var termo = await Api.ObterTermoAsync(linha.Id);
            if (termo is null)
            {
                Erro = "Termo nao disponivel para esta ordem.";
                return;
            }

            new TermoWindow(termo).ShowDialog();
        }
        catch (ApiException e)
        {
            Erro = e.MensagemFormatada;
        }
    }

    [RelayCommand]
    private void LimparBusca()
    {
        Busca = string.Empty;
        StatusFiltro = string.Empty;
    }
}
