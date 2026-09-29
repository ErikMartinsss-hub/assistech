using System.IO;
using System.Windows.Media;
using Assistech.Desktop.Services;
using Assistech.Shared.Client;
using Assistech.Shared.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Assistech.Desktop.ViewModels;

public class PaginaBase : ObservableObject
{
    protected PaginaBase(IAssistechApi api, SessaoDesktop sessao)
    {
        Api = api;
        Sessao = sessao;
    }

    protected IAssistechApi Api { get; }
    protected SessaoDesktop Sessao { get; }
    protected Guid EmpresaId => Sessao.Atual?.EmpresaId ?? Guid.Empty;
}

public partial class LojaViewModel : PaginaBase
{
    private readonly MarcaService _marca;

    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _documento = string.Empty;
    [ObservableProperty] private string _telefone = string.Empty;
    [ObservableProperty] private string _whatsapp = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _endereco = string.Empty;
    [ObservableProperty] private string _corPrimaria = "#0d6efd";
    [ObservableProperty] private bool _temLogo;
    [ObservableProperty] private ImageSource? _logo;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _mensagem = string.Empty;

    public LojaViewModel(IAssistechApi api, SessaoDesktop sessao, MarcaService marca) : base(api, sessao)
    {
        _marca = marca;
    }

    public void Inicializar()
    {
        if (EmpresaId != Guid.Empty)
            _ = CarregarAsync();
    }

    [RelayCommand]
    private async Task CarregarAsync()
    {
        Ocupado = true;
        try
        {
            var empresa = await Api.ObterEmpresaAsync();
            Aplicar(empresa);
        }
        catch (ApiException e)
        {
            Mensagem = e.MensagemFormatada;
        }
        finally
        {
            Ocupado = false;
        }
    }

    private void Aplicar(EmpresaDto empresa)
    {
        Nome = empresa.Nome;
        Documento = empresa.Documento;
        Telefone = empresa.Telefone;
        Whatsapp = empresa.WhatsApp;
        Email = empresa.Email;
        Endereco = empresa.Endereco;
        CorPrimaria = string.IsNullOrWhiteSpace(empresa.CorPrimaria) ? "#0d6efd" : empresa.CorPrimaria;
        TemLogo = empresa.TemLogo;
        Mensagem = string.Empty;
        _ = CarregarLogoAsync();
    }

    private async Task CarregarLogoAsync()
    {
        Logo = await _marca.CarregarLogoAsync(Api, EmpresaId);
    }

    [RelayCommand]
    private async Task SalvarAsync()
    {
        Ocupado = true;
        Mensagem = string.Empty;

        try
        {
            var empresa = await Api.AtualizarEmpresaAsync(new AtualizarEmpresaRequest
            {
                Nome = Nome.Trim(),
                Documento = Documento.Trim(),
                Telefone = Telefone.Trim(),
                WhatsApp = Whatsapp.Trim(),
                Email = Email.Trim(),
                Endereco = Endereco.Trim(),
                CorPrimaria = CorPrimaria.Trim()
            });

            _marca.Aplicar(empresa.CorPrimaria);
            Aplicar(empresa);
            Mensagem = "Dados da loja atualizados.";
        }
        catch (ApiException e)
        {
            Mensagem = e.MensagemFormatada;
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private async Task SelecionarLogoAsync()
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecionar logo",
            Filter = "Imagens (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };

        if (dialogo.ShowDialog() != true) return;

        try
        {
            await using var stream = File.OpenRead(dialogo.FileName);
            await Api.EnviarLogoAsync(stream, Path.GetFileName(dialogo.FileName));

            TemLogo = true;
            await CarregarLogoAsync();
            Mensagem = "Logo atualizada.";
        }
        catch (ApiException e)
        {
            Mensagem = e.MensagemFormatada;
        }
    }

    [RelayCommand]
    private async Task RemoverLogoAsync()
    {
        try
        {
            await Api.RemoverLogoAsync();
            TemLogo = false;
            Logo = null;
            Mensagem = "Logo removida.";
        }
        catch (ApiException e)
        {
            Mensagem = e.MensagemFormatada;
        }
    }
}
