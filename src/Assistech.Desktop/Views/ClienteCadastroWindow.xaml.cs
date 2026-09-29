using System.Windows;
using Assistech.Desktop.ViewModels;
using Assistech.Shared.Dtos;

namespace Assistech.Desktop.Views;

public partial class ClienteCadastroWindow : Window
{
    public ClienteCadastroWindow(ClienteCadastroViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private ClienteCadastroViewModel? Vm => DataContext as ClienteCadastroViewModel;

    private void FecharClicado(object sender, RoutedEventArgs e) => Close();

    private async void SalvarClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        await Vm.SalvarCommand.ExecuteAsync(null);
    }

    private async void SalvarEquipamentoClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is not null)
            await Vm.SalvarEquipamentoCommand.ExecuteAsync(null);
    }

    private async void ExcluirEquipamentoClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is not null && sender is FrameworkElement { DataContext: EquipamentoDto equipamento })
            await Vm.ExcluirEquipamentoCommand.ExecuteAsync(equipamento);
    }

    private void LimparEquipamentoClicado(object sender, RoutedEventArgs e)
        => Vm?.LimparEquipamentoCommand.Execute(null);
}
