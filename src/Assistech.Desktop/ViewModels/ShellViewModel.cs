using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Assistech.Desktop.Services;
using Assistech.Desktop.Views;
using Assistech.Shared.Client;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Assistech.Desktop.ViewModels;

public enum PaginaAtiva
{
    Ordens,
    Clientes,
    Loja
}

public partial class ShellViewModel : ObservableObject
{
    private readonly IAssistechApi _api;
    private readonly SessaoDesktop _sessao;
    private readonly MarcaService _marca;

    [ObservableProperty] private PaginaAtiva _pagina = PaginaAtiva.Ordens;
    [ObservableProperty] private ImageSource? _logo;
    [ObservableProperty] private string _usuario = string.Empty;
    [ObservableProperty] private string _empresa = string.Empty;

    public ShellViewModel(IAssistechApi api, SessaoDesktop sessao, MarcaService marca)
    {
        _api = api;
        _sessao = sessao;
        _marca = marca;
    }

    public ObservableCollection<object> Paginas { get; } = new();

    public bool MostrarOrdens => Pagina == PaginaAtiva.Ordens;
    public bool MostrarClientes => Pagina == PaginaAtiva.Clientes;
    public bool MostrarLoja => Pagina == PaginaAtiva.Loja;

    public string PaginaTitulo => Pagina switch
    {
        PaginaAtiva.Ordens => "ORDENS DE SERVICO",
        PaginaAtiva.Clientes => "CLIENTES E EQUIPAMENTOS",
        _ => "PERFIL DA LOJA"
    };

    public string Inicial => (_sessao.Atual?.Nome ?? "?").Length > 0
        ? _sessao.Atual!.Nome[..1].ToUpperInvariant()
        : "?";

    public void Inicializar()
    {
        _marca.Aplicar(_sessao.Atual?.EmpresaCorPrimaria ?? "#0D6EFD");
        Usuario = _sessao.Atual?.Nome ?? string.Empty;
        Empresa = _sessao.Atual?.EmpresaNome ?? string.Empty;
        OnPropertyChanged(nameof(Inicial));

        Paginas.Add(new OrdensViewModel(_api, _sessao, _marca));
        Paginas.Add(new ClientesViewModel(_api, _sessao, _marca));
        Paginas.Add(new LojaViewModel(_api, _sessao, _marca));

        _ = CarregarLogoAsync();
        (Paginas[0] as OrdensViewModel)?.Inicializar();
        (Paginas[2] as LojaViewModel)?.Inicializar();
    }

    private async Task CarregarLogoAsync()
    {
        if (_sessao.Atual is not { } sessao) return;
        Logo = await _marca.CarregarLogoAsync(_api, sessao.EmpresaId);
    }

    partial void OnPaginaChanged(PaginaAtiva value)
    {
        OnPropertyChanged(nameof(MostrarOrdens));
        OnPropertyChanged(nameof(MostrarClientes));
        OnPropertyChanged(nameof(MostrarLoja));
    }

    [RelayCommand]
    private void IrParaOrdens()
    {
        Pagina = PaginaAtiva.Ordens;
        (Paginas.FirstOrDefault() as OrdensViewModel)?.Inicializar();
    }

    [RelayCommand]
    private void IrParaClientes()
    {
        Pagina = PaginaAtiva.Clientes;
        (Paginas.ElementAtOrDefault(1) as ClientesViewModel)?.Inicializar();
    }

    [RelayCommand]
    private void IrParaLoja()
    {
        Pagina = PaginaAtiva.Loja;
        (Paginas.ElementAtOrDefault(2) as LojaViewModel)?.Inicializar();
    }

    [RelayCommand]
    private async Task SairAsync()
    {
        try
        {
            await _api.LogoutAsync();
        }
        catch
        {
            // mesmo com falha de rede, encerra a sessao local
        }

        _sessao.Sair();

        var anterior = Application.Current.MainWindow;
        var login = App.Criar<LoginWindow>();
        login.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Application.Current.MainWindow = login;
        anterior?.Close();
        login.Show();
    }
}
