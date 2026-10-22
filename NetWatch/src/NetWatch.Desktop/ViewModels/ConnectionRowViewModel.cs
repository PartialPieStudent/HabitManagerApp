using CommunityToolkit.Mvvm.ComponentModel;
using NetWatch.Core.Analysis;
using NetWatch.Core.Models;

namespace NetWatch.Desktop.ViewModels;

/// <summary>
/// Модель представления для ОДНОЙ строки таблицы.
///
/// Зачем отдельный класс, если есть ConnectionInfo? Интерфейс должен узнавать об изменениях,
/// чтобы перерисовать ячейку. Для этого объект реализует INotifyPropertyChanged — "сообщает",
/// какое свойство изменилось. ObservableObject из MVVM Toolkit уже реализует этот интерфейс,
/// нам остаётся вызывать OnPropertyChanged(...).
///
/// Ядро (NetWatch.Core) про интерфейс ничего не знает и знать не должно — поэтому "обёртка" живёт здесь.
/// </summary>
public sealed class ConnectionRowViewModel : ObservableObject
{
    public ConnectionInfo Info { get; }

    public ConnectionRowViewModel(ConnectionInfo info) => Info = info;

    // ---- Свойства, которые не меняются ----

    public string Protocol => Info.Protocol.ToString().ToUpperInvariant();
    public string LocalEndPoint => Info.LocalEndPoint;
    public string RemoteEndPoint => Info.RemoteEndPoint;
    public string DirectionText => Info.Direction.ToDisplayString();

    /// <summary>"≤" показывает, что соединение уже было на момент запуска и настоящее время открытия неизвестно.</summary>
    public string FirstSeenText => Info.ExistedAtStart
        ? $"≤ {Info.FirstSeen:HH:mm:ss}"
        : Info.FirstSeen.ToString("HH:mm:ss");

    // ---- Свойства, которые меняются со временем (обновляются методом Refresh) ----

    public int Pid => Info.Pid;
    public string ProcessName => Info.ProcessName;
    public string ProcessPath => Info.ProcessPath ?? "";
    public string StateText => Info.State.ToDisplayString();
    public string RemoteHost => Info.RemoteHost ?? "";
    public string DurationText => Formatting.Duration(Info.Duration);

    /// <summary>Для подсветки ячейки цветом (через RiskToBrushConverter).</summary>
    public RiskLevel? Risk => Info.Risk;
    public string RiskText => Formatting.Risk(Info.Risk);
    public string Verdict => Info.Verdict ?? "";

    /// <summary>
    /// Сообщает интерфейсу, что "живые" свойства могли измениться.
    /// Вызывается после каждого опроса и после анализа.
    /// </summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Pid));
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(ProcessPath));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(RemoteHost));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(Risk));
        OnPropertyChanged(nameof(RiskText));
        OnPropertyChanged(nameof(Verdict));
    }
}
