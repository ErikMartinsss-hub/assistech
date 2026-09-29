using System.Windows;
using System.Windows.Input;
using Assistech.Desktop.ViewModels;

namespace Assistech.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.LoginConcluido += () =>
        {
            var principal = App.Criar<ShellWindow>();
            Application.Current.MainWindow = principal;
            principal.Show();
            Close();
        };
        viewModel.LoginFalhou += () => SenhaBox.Clear();
    }

    private void Entrar(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && vm.EntrarCommand.CanExecute(null))
            vm.EntrarCommand.Execute(null);
    }

    private void EntrarDireto(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (DataContext is LoginViewModel vm && vm.EntrarCommand.CanExecute(null))
            vm.EntrarCommand.Execute(null);

        e.Handled = true;
    }

    private void FocarSenha(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        SenhaBox.Focus();
        e.Handled = true;
    }

    private void FecharJanela(object sender, RoutedEventArgs e) => Close();

    private void SenhaAlterada(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Senha = SenhaBox.Password;
    }
}
