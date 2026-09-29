using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Assistech.Shared.Client;
using Assistech.Shared.Dtos;

namespace Assistech.Desktop.Services;

public sealed class DesktopOptions
{
    public string ApiBaseUrl { get; init; } = "http://localhost:5099";
}

/// <summary>Sessao do usuario logado no app desktop.</summary>
public sealed class SessaoDesktop
{
    public static SessaoDesktop Instance { get; } = new();

    public SessaoDto? Atual { get; private set; }
    public bool Autenticado => Atual is not null;
    public event Action? Alterada;

    public void Entrar(SessaoDto sessao)
    {
        Atual = sessao;
        Alterada?.Invoke();
    }

    public void Sair()
    {
        Atual = null;
        Alterada?.Invoke();
    }
}

/// <summary>Aplica logo e cor da loja na interface do desktop.</summary>
public sealed class MarcaService
{
    private readonly ResourceDictionary _recursos = (ResourceDictionary)Application.Current.Resources;

    public void Aplicar(string corPrimaria)
    {
        if (!string.IsNullOrWhiteSpace(corPrimaria) && ColorConverter.ConvertFromString(corPrimaria) is Color cor)
        {
            _recursos["CorPrimaria"] = cor;
            _recursos["BrushPrimaria"] = new SolidColorBrush(cor);
            _recursos["BrushPrimariaEscura"] = new SolidColorBrush(Ajustar(cor, -0.2));
            _recursos["BrushPrimariaClara"] = new SolidColorBrush(Ajustar(cor, 0.85));
        }
    }

    public async Task<ImageSource?> CarregarLogoAsync(IAssistechApi api, Guid empresaId, CancellationToken ct = default)
    {
        try
        {
            var empresa = await api.ObterEmpresaAsync(ct);
            if (!empresa.TemLogo) return null;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var url = new Uri(new Uri(ApiUrl()), $"api/config/logo/{empresaId}");
            var bytes = await http.GetByteArrayAsync(url, ct);

            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.StreamSource = new MemoryStream(bytes);
            imagem.EndInit();
            imagem.Freeze();

            return imagem;
        }
        catch
        {
            return null;
        }
    }

    public static string ApiUrl()
    {
        try
        {
            return ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5099";
        }
        catch
        {
            return "http://localhost:5099";
        }
    }

    private static Color Ajustar(Color cor, double fator) => Color.FromRgb(
        (byte)Math.Clamp(cor.R + 255 * fator, 0, 255),
        (byte)Math.Clamp(cor.G + 255 * fator, 0, 255),
        (byte)Math.Clamp(cor.B + 255 * fator, 0, 255));
}
