using System.Collections.ObjectModel;
using Assistech.Desktop.Services;
using Assistech.Shared.Client;
using Assistech.Shared.Dtos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Assistech.Desktop.ViewModels;

public partial class ClienteCadastroViewModel : PaginaBase
{
    private readonly MarcaService _marca;

    [ObservableProperty] private Guid? _clienteId;
    [ObservableProperty] private string _titulo = "Novo cliente";
    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string _documento = string.Empty;
    [ObservableProperty] private string _telefone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _endereco = string.Empty;
    [ObservableProperty] private string _observacoes = string.Empty;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string _erro = string.Empty;
    [ObservableProperty] private bool _salvo;

    [ObservableProperty] private string _eqTipo = "celular";
    [ObservableProperty] private string _eqMarca = string.Empty;
    [ObservableProperty] private string _eqModelo = string.Empty;
    [ObservableProperty] private string _eqSerie = string.Empty;
    [ObservableProperty] private string _eqCor = string.Empty;
    [ObservableProperty] private string _eqAno = string.Empty;
    [ObservableProperty] private string _eqObservacoes = string.Empty;

    public ClienteCadastroViewModel(IAssistechApi api, SessaoDesktop sessao, MarcaService marca) : base(api, sessao)
    {
        _marca = marca;
    }

    public ObservableCollection<EquipamentoDto> Equipamentos { get; } = new();

    public IReadOnlyList<string> TiposEquipamento { get; } = new[]
    {
        "celular", "tablet", "notebook", "desktop", "monitor", "impressora", "periferico", "outro"
    };

    public bool EhEdicao => ClienteId is not null;
    public bool SemEquipamentos => Equipamentos.Count == 0;

    partial void OnNomeChanged(string value) => SalvarCommand.NotifyCanExecuteChanged();
    partial void OnOcupadoChanged(bool value) => SalvarCommand.NotifyCanExecuteChanged();

    public async Task InicializarAsync(Guid clienteId)
    {
        ClienteId = clienteId;
        Titulo = "Editar cliente";
        Ocupado = true;

        try
        {
            var cliente = await Api.ObterClienteAsync(clienteId);
            if (cliente is not null)
            {
                Nome = cliente.Nome;
                Documento = cliente.Documento;
                Telefone = cliente.Telefone;
                Email = cliente.Email;
                Endereco = cliente.Endereco;
                Observacoes = cliente.Observacoes;
            }

            await CarregarEquipamentosAsync();
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

    [RelayCommand(CanExecute = nameof(PodeSalvar))]
    private async Task SalvarAsync()
    {
        Ocupado = true;
        Erro = string.Empty;

        try
        {
            var cliente = await Api.SalvarClienteAsync(new ClienteInput
            {
                Id = ClienteId,
                Nome = Nome.Trim(),
                Documento = Documento.Trim(),
                Telefone = Telefone.Trim(),
                Email = Email.Trim(),
                Endereco = Endereco.Trim(),
                Observacoes = Observacoes.Trim()
            });

            ClienteId = cliente.Id;
            Titulo = "Editar cliente";
            Salvo = true;

            if (!string.IsNullOrWhiteSpace(EqMarca) || !string.IsNullOrWhiteSpace(EqModelo))
                await SalvarEquipamentoAsync();

            await CarregarEquipamentosAsync();
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

    public bool PodeSalvar => !Ocupado && !string.IsNullOrWhiteSpace(Nome);

    private async Task CarregarEquipamentosAsync()
    {
        if (ClienteId is not { } id) return;

        try
        {
            var lista = await Api.ListarEquipamentosDoClienteAsync(id);
            Equipamentos.Clear();
            foreach (var e in lista)
                Equipamentos.Add(e);

            OnPropertyChanged(nameof(SemEquipamentos));
        }
        catch (ApiException)
        {
            Equipamentos.Clear();
            OnPropertyChanged(nameof(SemEquipamentos));
        }
    }

    [RelayCommand]
    private async Task SalvarEquipamentoAsync()
    {
        if (ClienteId is not { } clienteId) return;

        if (string.IsNullOrWhiteSpace(EqMarca) || string.IsNullOrWhiteSpace(EqModelo))
        {
            Erro = "Informe marca e modelo do equipamento.";
            return;
        }

        try
        {
            await Api.SalvarEquipamentoAsync(new EquipamentoInput
            {
                ClienteId = clienteId,
                Tipo = EqTipo,
                Marca = EqMarca.Trim(),
                Modelo = EqModelo.Trim(),
                NumeroSerie = EqSerie.Trim(),
                Cor = EqCor.Trim(),
                Ano = int.TryParse(EqAno, out var ano) ? ano : null,
                Observacoes = EqObservacoes.Trim()
            });

            EqMarca = string.Empty;
            EqModelo = string.Empty;
            EqSerie = string.Empty;
            EqCor = string.Empty;
            EqAno = string.Empty;
            EqObservacoes = string.Empty;
            Erro = string.Empty;

            await CarregarEquipamentosAsync();
        }
        catch (ApiException e)
        {
            Erro = e.MensagemFormatada;
        }
    }

    [RelayCommand]
    private async Task ExcluirEquipamentoAsync(EquipamentoDto? equipamento)
    {
        if (equipamento is null) return;

        try
        {
            await Api.ExcluirEquipamentoAsync(equipamento.Id);
            await CarregarEquipamentosAsync();
        }
        catch (ApiException e)
        {
            Erro = e.MensagemFormatada;
        }
    }

    [RelayCommand]
    private void LimparEquipamento()
    {
        EqMarca = string.Empty;
        EqModelo = string.Empty;
        EqSerie = string.Empty;
        EqCor = string.Empty;
        EqAno = string.Empty;
        EqObservacoes = string.Empty;
    }
}
