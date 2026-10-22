using NetWatch.Core.Analysis;

namespace NetWatch.Core.Models;

/// <summary>
/// Общие функции форматирования, которые нужны и консоли, и GUI.
/// static class — класс, у которого нельзя создать экземпляр; просто набор функций.
/// </summary>
public static class Formatting
{
    /// <summary>Длительность в читабельном виде: "45с", "3м 12с", "2ч 05м", "1д 4ч".</summary>
    public static string Duration(TimeSpan d)
    {
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        if (d.TotalDays >= 1) return $"{(int)d.TotalDays}д {d.Hours}ч";
        if (d.TotalHours >= 1) return $"{(int)d.TotalHours}ч {d.Minutes:00}м";
        if (d.TotalMinutes >= 1) return $"{(int)d.TotalMinutes}м {d.Seconds:00}с";
        return $"{d.Seconds}с";
    }

    /// <summary>Уровень риска по-русски. null — "ещё не проверялось" (пустая строка).</summary>
    public static string Risk(RiskLevel? risk) => risk switch
    {
        RiskLevel.Clean => "Чисто",
        RiskLevel.Unknown => "Нет данных",
        RiskLevel.Low => "Низкий",
        RiskLevel.Suspicious => "Подозрительно",
        RiskLevel.Malicious => "ОПАСНО",
        _ => ""
    };

    /// <summary>Обрезает строку до нужной длины, добавляя "…" (для таблиц в консоли).</summary>
    public static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
