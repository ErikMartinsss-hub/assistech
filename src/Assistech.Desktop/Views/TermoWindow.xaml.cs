using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Assistech.Shared.Dtos;
using Forms = System.Windows.Forms;
using Gdi = System.Drawing;
using Impressao = System.Drawing.Printing;

namespace Assistech.Desktop.Views;

public partial class TermoWindow : Window
{
    private const double DpiImpressao = 300;

    private readonly TermoOrdemServicoDto _termo;

    public TermoWindow(TermoOrdemServicoDto termo)
    {
        _termo = termo;
        InitializeComponent();
        Preencher();
    }

    private void Preencher()
    {
        var destaque = Pincel(_termo.EmpresaCorPrimaria);

        Divisor.Background = destaque;
        LinhaTecnico.BorderBrush = destaque;
        LinhaTecnico.BorderThickness = new Thickness(0, 0, 0, 1);
        LinhaCliente.BorderBrush = destaque;
        LinhaCliente.BorderThickness = new Thickness(0, 0, 0, 1);

        EmpresaNome.Text = Vazio(_termo.EmpresaNome);
        EmpresaNome.Foreground = destaque;
        EmpresaDocumento.Text = string.IsNullOrWhiteSpace(_termo.EmpresaDocumento)
            ? "-"
            : $"CNPJ/CPF: {_termo.EmpresaDocumento}";
        EmpresaEndereco.Text = Vazio(_termo.EmpresaEndereco);
        EmpresaContato.Text = $"Tel. {Vazio(_termo.EmpresaTelefone)}   WhatsApp {Vazio(_termo.Whatsapp)}";

        TituloTermo.Text = $"TERMO DE ORDEM DE SERVICO  #{_termo.Numero:0000}";
        TituloTermo.Foreground = destaque;
        Subtitulo.Text = $"OS #{_termo.Numero:0000} - emitida em {_termo.EmitidoEm:dd/MM/yyyy HH:mm}";

        ClienteNome.Text = Vazio(_termo.ClienteNome);
        ClienteDocumento.Text = $"Doc: {Vazio(_termo.ClienteDocumento)}";
        ClienteTelefone.Text = $"Tel: {Vazio(_termo.ClienteTelefone)}";
        ClienteEndereco.Text = Vazio(_termo.ClienteEndereco);

        EquipamentoDescricao.Text = Vazio(($"{_termo.EquipamentoTipo} {_termo.EquipamentoMarca} {_termo.EquipamentoModelo}").Trim());
        EquipamentoCor.Text = $"Cor: {Vazio(_termo.EquipamentoCor)}";
        EquipamentoSerie.Text = $"Serie: {Vazio(_termo.EquipamentoNumeroSerie)}";

        DataEntrada.Text = $"Entrada: {_termo.DataEntrada:dd/MM/yyyy}";
        DataPrevisao.Text = $"Previsao: {(_termo.DataPrevisao is { } p ? p.ToString("dd/MM/yyyy") : "-")}";
        StatusTermo.Text = $"Status: {Capitalizar(_termo.Status)}";
        Garantia.Text = $"Garantia: {_termo.GarantiaDias} dias";

        TipoServico.Text = Vazio(_termo.TipoServico);
        RelatoCliente.Text = string.IsNullOrWhiteSpace(_termo.RelatoCliente)
            ? "Sem relato adicional do cliente."
            : _termo.RelatoCliente;

        PreencherItens();

        SubtotalTexto.Text = $"Subtotal {Moeda(_termo.Subtotal)}";
        DescontoTexto.Text = $"Desconto {Moeda(_termo.Desconto)}";
        TotalTexto.Text = $"TOTAL {Moeda(_termo.ValorTotal)}";
        TotalTexto.Foreground = destaque;

        ExibirBloco(BlocoDiagnostico, DiagnosticoTexto, "DIAGNOSTICO", _termo.Diagnostico);
        ExibirBloco(BlocoLaudo, LaudoTexto, "LAUDO DE CONCLUSAO", _termo.LaudoConclusao);
        ExibirBloco(BlocoAcessorios, AcessoriosTexto, "ACESSORIOS RECEBIDOS COM O EQUIPAMENTO", _termo.Acessorios);

        NomeTecnico.Text = Vazio(_termo.Responsavel);
        NomeCliente.Text = Vazio(_termo.ClienteNome);

        Rodape.Text = $"Emitido em {_termo.EmitidoEm.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}  -  Assistech";

        if (_termo.EmpresaTemLogo && !string.IsNullOrWhiteSpace(_termo.EmpresaUrlLogo))
            LogoImage.Source = CarregarLogo(_termo.EmpresaUrlLogo);
    }

    private void PreencherItens()
    {
        ListaItens.Children.Clear();

        if (_termo.Itens.Count == 0)
        {
            ListaItens.Children.Add(new TextBlock
            {
                Text = "Nenhum item lancado.",
                FontSize = 9.5,
                Foreground = Pincel("#94A3B8"),
                Margin = new Thickness(6, 6, 0, 0)
            });
            return;
        }

        var indice = 1;
        foreach (var item in _termo.Itens)
        {
            var grade = new Grid();
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });

            grade.Children.Add(Celula(indice.ToString(CultureInfo.InvariantCulture), 0, TextAlignment.Center,
                margem: new Thickness(6, 3, 0, 3)));
            grade.Children.Add(Celula(Vazio(item.Descricao), 1, margem: new Thickness(0, 3, 0, 3)));
            grade.Children.Add(Celula(item.Quantidade.ToString("0.##", CultureInfo.InvariantCulture), 2, TextAlignment.Center,
                margem: new Thickness(0, 3, 0, 3)));
            grade.Children.Add(Celula(item.ValorUnitario.ToString("N2", CultureInfo.InvariantCulture), 3, TextAlignment.Right,
                margem: new Thickness(0, 3, 0, 3)));
            grade.Children.Add(Celula(item.Subtotal.ToString("N2", CultureInfo.InvariantCulture), 4, TextAlignment.Right,
                negrito: true, margem: new Thickness(0, 3, 6, 3)));

            ListaItens.Children.Add(grade);
            indice++;
        }
    }

    private static TextBlock Celula(string texto, int coluna, TextAlignment alinhamento = TextAlignment.Left,
        bool negrito = false, Thickness? margem = null)
    {
        var bloco = new TextBlock
        {
            Text = texto,
            FontSize = 9.5,
            FontWeight = negrito ? FontWeights.SemiBold : FontWeights.Normal,
            TextAlignment = alinhamento,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = margem ?? new Thickness(0)
        };

        Grid.SetColumn(bloco, coluna);
        return bloco;
    }

    private static void ExibirBloco(UIElement bloco, TextBlock destino, string titulo, string? conteudo)
    {
        var possui = !string.IsNullOrWhiteSpace(conteudo);
        bloco.Visibility = possui ? Visibility.Visible : Visibility.Collapsed;
        if (possui) destino.Text = conteudo;
    }

    private static ImageSource? CarregarLogo(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var bytes = http.GetByteArrayAsync(new Uri(url)).GetAwaiter().GetResult();

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private RenderTargetBitmap? Renderizar()
    {
        try
        {
            Folha.UpdateLayout();

            var largura = Math.Max(Folha.ActualWidth, Folha.Width);
            var altura = Math.Max(Folha.ActualHeight, Folha.DesiredSize.Height);
            if (largura <= 0 || altura <= 0) return null;

            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(largura * DpiImpressao / 96),
                (int)Math.Ceiling(altura * DpiImpressao / 96),
                DpiImpressao, DpiImpressao, PixelFormats.Pbgra32);

            bitmap.Render(Folha);
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static void Desenhar(Impressao.PrintPageEventArgs args, Stream png)
    {
        var area = args.MarginBounds;
        if (args.Graphics is not { } grafico)
        {
            args.HasMorePages = false;
            return;
        }

        png.Position = 0;
        using var imagem = Gdi.Image.FromStream(png);

        var margem = 20f;
        var larguraUtil = imagem.Width * 96f / 300f;
        var alturaUtil = imagem.Height * 96f / 300f;

        var escala = Math.Min((area.Width - margem * 2) / larguraUtil, (area.Height - margem * 2) / alturaUtil);
        if (escala <= 0) escala = 1f;

        var largura = larguraUtil * escala;
        var altura = alturaUtil * escala;

        grafico.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        grafico.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        grafico.DrawImage(imagem,
            area.Left + (area.Width - largura) / 2f,
            area.Top + (area.Height - altura) / 2f,
            largura, altura);

        args.HasMorePages = false;
    }

    private void ImprimirClicado(object sender, RoutedEventArgs e)
    {
        var bitmap = Renderizar();
        if (bitmap is null)
        {
            MessageBox.Show("Nao foi possivel preparar o termo para impressao.",
                "Assistech", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var png = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(png);
        png.Position = 0;

        try
        {
            using var dialogo = new Forms.PrintDialog();
            if (dialogo.ShowDialog() != Forms.DialogResult.OK) return;

            using var documento = new Impressao.PrintDocument { DocumentName = $"OS {_termo.Numero:0000}" };
            documento.PrintPage += (_, args) => Desenhar(args, png);
            documento.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao imprimir: {ex.Message}",
                "Assistech", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void FecharClicado(object sender, RoutedEventArgs e) => Close();

    private static Brush Pincel(string? hex)
        => ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(hex) ? "#0d6efd" : hex) is Color cor
            ? new SolidColorBrush(cor)
            : Brushes.RoyalBlue;

    private static string Vazio(string? valor) => string.IsNullOrWhiteSpace(valor) ? "-" : valor;

    private static string Moeda(decimal valor) => valor.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));

    private static string Capitalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "-";
        return char.ToUpperInvariant(texto[0]) + texto[1..].Replace('_', ' ');
    }
}
