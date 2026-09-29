using System.Net.Http;
using Assistech.Desktop.Services;
using Assistech.Shared.Client;
using Assistech.Shared.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Assistech.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAssistechApi _api;
    private readonly SessaoDesktop _sessao;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _senha = string.Empty;
    [ObservableProperty] private string _erro = string.Empty;
    [ObservableProperty] private bool _ocupado;

    public event Action? LoginConcluido;
    public event Action? LoginFalhou;

    public LoginViewModel(IAssistechApi api, SessaoDesktop sessao)
    {
        _api = api;
        _sessao = sessao;
    }

    public string Servidor => MarcaService.ApiUrl();

    public bool ErroVisivel => !string.IsNullOrEmpty(Erro);

    public bool PodeEntrar => !Ocupado
                               && !string.IsNullOrWhiteSpace(Email)
                               && !string.IsNullOrWhiteSpace(Senha);

    public string BotaoEntrar => Ocupado ? "Entrando..." : "Entrar";

    partial void OnEmailChanged(string value) => NotificarEstado();
    partial void OnSenhaChanged(string value) => NotificarEstado();
    partial void OnErroChanged(string value) => OnPropertyChanged(nameof(ErroVisivel));
    partial void OnOcupadoChanged(bool value) => NotificarEstado();

    private void NotificarEstado()
    {
        OnPropertyChanged(nameof(PodeEntrar));
        OnPropertyChanged(nameof(BotaoEntrar));
    }

    [RelayCommand(CanExecute = nameof(PodeEntrar))]
    private async Task EntrarAsync()
    {
        Ocupado = true;
        Erro = string.Empty;

        try
        {
            var sessao = await _api.LoginAsync(new LoginRequest(Email.Trim(), Senha));
            _sessao.Entrar(sessao);
            LoginConcluido?.Invoke();
        }
        catch (ApiException e)
        {
            Erro = e.MensagemFormatada;
            LoginFalhou?.Invoke();
        }
        catch (HttpRequestException)
        {
            Erro = $"Nao foi possivel falar com a API em {Servidor}.";
        }
        catch (TaskCanceledException)
        {
            Erro = "O servidor demorou para responder. Tente novamente.";
        }
        finally
        {
            Ocupado = false;
            EntrarCommand.NotifyCanExecuteChanged();
        }
    }
}
