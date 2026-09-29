using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Assistech.Desktop.ViewModels;

namespace Assistech.Desktop.Views;

public partial class OrdensView : UserControl
{
    public OrdensView() => InitializeComponent();

    private OrdensViewModel? Vm => DataContext as OrdensViewModel;

    private LinhaOrdem? Selecionada => (Grid.SelectedItem as LinhaOrdem)
                                      ?? Vm?.OrdemSelecionada;

    private void NovaOrdemClicado(object sender, RoutedEventArgs e) => Vm?.NovaOrdemCommand.Execute(null);

    private void AbrirClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && Selecionada is { } linha)
            _ = vm.AbrirOrdemAsync(linha);
    }

    private void TermoClicado(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && Selecionada is { } linha)
            _ = vm.ImprimirTermoAsync(linha);
    }

    private void ProximaPaginaClicado(object sender, RoutedEventArgs e) => Vm?.ProximaPaginaCommand.Execute(null);

    private void PaginaAnteriorClicado(object sender, RoutedEventArgs e) => Vm?.PaginaAnteriorCommand.Execute(null);

    private void LinhaDuploClique(object sender, MouseButtonEventArgs e)
    {
        if (Vm is { } vm && Selecionada is { } linha)
            _ = vm.AbrirOrdemAsync(linha);
    }
}
