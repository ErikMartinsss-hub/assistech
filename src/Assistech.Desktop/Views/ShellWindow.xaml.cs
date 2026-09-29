using System.Windows;
using System.Windows.Input;
using Assistech.Desktop.ViewModels;

namespace Assistech.Desktop.Views;

public partial class ShellWindow : Window
{
    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.Inicializar();
    }

    private void SairClicado(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ShellViewModel vm && vm.SairCommand.CanExecute(null))
            vm.SairCommand.Execute(null);
    }
}
