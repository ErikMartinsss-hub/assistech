using System.Windows;
using Assistech.Desktop.Services;
using Assistech.Desktop.ViewModels;
using Assistech.Desktop.Views;
using Assistech.Shared.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Assistech.Desktop;

/// <summary>Bootstrap do aplicativo desktop.</summary>
public partial class App : Application
{
    public static IServiceProvider Servicos { get; private set; } = null!;

    public static T Criar<T>() where T : notnull => ActivatorUtilities.CreateInstance<T>(Servicos);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            var detalhe = args.Exception is ApiException api ? api.MensagemFormatada : args.Exception.Message;
            MessageBox.Show(detalhe, "Assistech", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var servicos = new ServiceCollection();
        servicos.AddSingleton(SessaoDesktop.Instance);
        servicos.AddSingleton<MarcaService>();
        servicos.AddTransient<LoginViewModel>();
        servicos.AddTransient<ShellViewModel>();
        servicos.AddTransient<OrdemEditorViewModel>();
        servicos.AddTransient<ClienteCadastroViewModel>();
        servicos.AddTransient<LoginWindow>();
        servicos.AddTransient<ShellWindow>();
        servicos.AddSingleton(sp =>
        {
            var sessao = sp.GetRequiredService<SessaoDesktop>();
            var http = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(MarcaService.ApiUrl().TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(60)
            };
            http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            return new HttpAssistechApi(http, nova =>
            {
                if (nova is null) sessao.Sair();
                else sessao.Entrar(nova);
            });
        });
        servicos.AddSingleton<IAssistechApi>(sp => sp.GetRequiredService<HttpAssistechApi>());

        Servicos = servicos.BuildServiceProvider();

        var login = Servicos.GetRequiredService<LoginWindow>();
        MainWindow = login;
        login.Show();
    }
}
