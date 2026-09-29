using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Assistech.Desktop.ViewModels;

namespace Assistech.Desktop.Views;

public partial class ClientesView : UserControl
{
    public ClientesView() => InitializeComponent();

    private ClientesViewModel? Vm => DataContext as ClientesViewModel;

    private LinhaCliente? Selecionado => (Grid.SelectedItem as LinhaCliente) ?? Vm?.ClienteSelecionado;

    private void NovoClienteClicado(object sender, RoutedEventArgs e) => Vm?.NovoClienteCommand.Execute(null);

    private void AbrirClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && Selecionado is { } linha)
            vm.AbrirCliente(linha);
    }

    private void ExcluirClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && Selecionado is { } linha)
            _ = vm.ExcluirClienteAsync(linha);
    }

    private void ProximaPaginaClicado(object sender, RoutedEventArgs e) => Vm?.ProximaPaginaCommand.Execute(null);

    private void PaginaAnteriorClicado(object sender, RoutedEventArgs e) => Vm?.PaginaAnteriorCommand.Execute(null);

    private void LinhaDuploClique(object sender, MouseButtonEventArgs e)
    {
        if (Vm is { } vm && Selecionado is { } linha)
            vm.AbrirCliente(linha);
    }
}
