using System.Collections.ObjectModel;
using System.Windows;
using Assistech.Desktop.Services;
using Assistech.Desktop.Views;
using Assistech.Shared.Client;
using Assistech.Shared.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Assistech.Desktop.ViewModels;

public sealed class LinhaCliente
{
    public required ClienteDto Modelo { get; init; }
    public Guid Id => Modelo.Id;
    public string Nome => Modelo.Nome;
    public string Documento => string.IsNullOrWhiteSpace(Modelo.Documento) ? "-" : Modelo.Documento;
    public string Telefone => string.IsNullOrWhiteSpace(Modelo.Telefone) ? "-" : Modelo.Telefone;
    public string Email => string.IsNullOrWhiteSpace(Modelo.Email) ? "-" : Modelo.Email;
    public int Equipamentos => Modelo.TotalEquipamentos;
    public int Ordens => Modelo.TotalOrdens;
}

public partial class ClientesViewModel : PaginaBase
{
    private readonly MarcaService _marca;

    [ObservableProperty] private string _busca = string.Empty;
    [ObservableProperty] private int _pagina = 1;
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _totalPaginas;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private bool _carregado;
    [ObservableProperty] private string _erro = string.Empty;
    [ObservableProperty] private LinhaCliente? _clienteSelecionado;

    public ClientesViewModel(IAssistechApi api, SessaoDesktop sessao, MarcaService marca) : base(api, sessao)
    {
        _marca = marca;
    }

    public ObservableCollection<LinhaCliente> Clientes { get; } = new();

    public bool TemClientes => Clientes.Count > 0;
    public bool PodeAvancar => Pagina < TotalPaginas;
    public bool PodeVoltar => Pagina > 1;

    public string Resumo => Carregado
        ? $"{Total} cliente(s) - pagina {Pagina} de {Math.Max(TotalPaginas, 1)}"
        : "Carregando clientes...";

    partial void OnBuscaChanged(string value) => _ = BuscarAsync();
    partial void OnPaginaChanged(int value) => _ = BuscarAsync();

    public void Inicializar() => _ = BuscarAsync();

    [RelayCommand]
    private async Task BuscarAsync()
    {
        Ocupado = true;
        Erro = string.Empty;

        try
        {
            var resultado = await Api.ListarClientesAsync(new ClienteFiltro
            {
                Busca = string.IsNullOrWhiteSpace(Busca) ? null : Busca,
                Pagina = Pagina,
                TamanhoPagina = 20
            });

            Clientes.Clear();
            foreach (var c in resultado.Itens)
                Clientes.Add(new LinhaCliente { Modelo = c });

            Total = resultado.Total;
            TotalPaginas = resultado.TotalPaginas;
            Carregado = true;
        }
        catch (ApiException e)
        {
            Erro = e.MensagemFormatada;
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
        OnPropertyChanged(nameof(TemClientes));
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
    private void NovoCliente()
    {
        var vm = App.Criar<ClienteCadastroViewModel>();
        var janela = new ClienteCadastroWindow(vm);
        if (janela.ShowDialog() == true)
            _ = BuscarAsync();
    }

    public void AbrirCliente(LinhaCliente linha)
    {
        var vm = App.Criar<ClienteCadastroViewModel>();
        var janela = new ClienteCadastroWindow(vm);
        _ = vm.InicializarAsync(linha.Id);
        janela.ShowDialog();
        _ = BuscarAsync();
    }

    public async Task ExcluirClienteAsync(LinhaCliente linha)
    {
        var resposta = MessageBox.Show(
            $"Excluir {linha.Nome}? As ordens de servico vinculadas serao mantidas, mas ficarao sem cliente.",
            "Confirmar exclusao", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (resposta != MessageBoxResult.Yes) return;

        try
        {
            await Api.ExcluirClienteAsync(linha.Id);
            _ = BuscarAsync();
        }
        catch (ApiException e)
        {
            MessageBox.Show(e.MensagemFormatada, "Assistech", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
