using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Assistech.Desktop.ViewModels;
using Assistech.Shared.Dtos;

namespace Assistech.Desktop.Views;

public partial class OrdemEditorWindow : Window
{
    public OrdemEditorWindow(OrdemEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private OrdemEditorViewModel? Vm => DataContext as OrdemEditorViewModel;

    private void FecharClicado(object sender, RoutedEventArgs e) => Close();

    private async void SalvarClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        await Vm.SalvarCommand.ExecuteAsync(null);
    }

    private void AdicionarItemClicado(object sender, RoutedEventArgs e) => Vm?.AdicionarItemCommand.Execute(null);

    private void RemoverItemClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is not null && sender is FrameworkElement { DataContext: ItemOrdem item })
            Vm.RemoverItemCommand.Execute(item);
    }

    private void SelecionarCliente(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null) return;
        if (sender is FrameworkElement { DataContext: ClienteDto cliente })
            Vm.ClienteSelecionado = cliente;
    }

    private void SelecionarEquipamentoClicado(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null) return;
        if (sender is FrameworkElement { DataContext: ItemEquipamento equipamento })
            Vm.EquipamentoSelecionado = equipamento;
    }

    private void LimparClienteClicado(object sender, RoutedEventArgs e) => Vm?.LimparClienteCommand.Execute(null);
}
