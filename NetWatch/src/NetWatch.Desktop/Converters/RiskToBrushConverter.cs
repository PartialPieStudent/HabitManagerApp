using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NetWatch.Core.Analysis;

namespace NetWatch.Desktop.Converters;

/// <summary>
/// Конвертер — "переводчик" между данными и интерфейсом.
/// Здесь: уровень риска (RiskLevel) → цвет фона ячейки.
///
/// Используется в XAML так:
///   Background="{Binding Risk, Converter={StaticResource RiskToBrush}}"
///
/// Цвета полупрозрачные (первые две hex-цифры — прозрачность), поэтому текст читается
/// и в светлой, и в тёмной теме.
/// </summary>
public sealed class RiskToBrushConverter : IValueConverter
{
    private static readonly IBrush Malicious = new SolidColorBrush(Color.Parse("#A0E5484D"));
    private static readonly IBrush Suspicious = new SolidColorBrush(Color.Parse("#90F76808"));
    private static readonly IBrush Low = new SolidColorBrush(Color.Parse("#70FFC53D"));
    private static readonly IBrush Clean = new SolidColorBrush(Color.Parse("#6046A758"));
    private static readonly IBrush Unknown = new SolidColorBrush(Color.Parse("#408B8D98"));

    /// <summary>Данные → интерфейс.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RiskLevel.Malicious => Malicious,
        RiskLevel.Suspicious => Suspicious,
        RiskLevel.Low => Low,
        RiskLevel.Clean => Clean,
        RiskLevel.Unknown => Unknown,
        _ => Brushes.Transparent // null — ещё не проверялось
    };

    /// <summary>Интерфейс → данные. Нам не нужно (цвет нельзя "ввести"), поэтому не поддерживаем.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
