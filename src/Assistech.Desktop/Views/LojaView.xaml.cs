using System.Windows;
using System.Windows.Controls;
using Assistech.Desktop.ViewModels;

namespace Assistech.Desktop.Views;

public partial class LojaView : UserControl
{
    public LojaView() => InitializeComponent();

    private LojaViewModel? Vm => DataContext as LojaViewModel;

    private void SalvarClicado(object sender, RoutedEventArgs e) => Vm?.SalvarCommand.Execute(null);

    private void EnviarLogoClicado(object sender, RoutedEventArgs e) => Vm?.SelecionarLogoCommand.Execute(null);

    private void RemoverLogoClicado(object sender, RoutedEventArgs e) => Vm?.RemoverLogoCommand.Execute(null);
}
