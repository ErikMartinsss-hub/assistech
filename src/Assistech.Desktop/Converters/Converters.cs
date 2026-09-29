using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Assistech.Desktop.ViewModels;

namespace Assistech.Desktop.Converters;

public sealed class IgualConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && Equals(value.ToString(), parameter?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class BooleanoVisibilidadeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;

        if (parameter is string p && p.Contains("invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class NaoVazioVisibilidadeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tem = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            System.Collections.ICollection c => c.Count > 0,
            _ => true
        };

        if (parameter is string p && p.Contains("invert", StringComparison.OrdinalIgnoreCase))
            tem = !tem;

        return tem ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class StatusFundoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => StatusTexto.Fundo(value?.ToString() ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class StatusCorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => StatusTexto.Cor(value?.ToString() ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public static class Paleta
{
    public static Brush De(string hex, byte deslocamento = 0)
    {
        if (ColorConverter.ConvertFromString(hex) is not Color cor) cor = Colors.SlateBlue;

        return new SolidColorBrush(Color.FromRgb(
            (byte)Math.Clamp(cor.R + deslocamento, 0, 255),
            (byte)Math.Clamp(cor.G + deslocamento, 0, 255),
            (byte)Math.Clamp(cor.B + deslocamento, 0, 255)));
    }
}
