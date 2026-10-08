namespace Habits.Domain.ValueObjects;


public enum RecurrenceFrequency
{
    Daily = 0,
    Weekly = 1
}

public sealed class RecurrenceRule : IEquatable<RecurrenceRule>
{
    public RecurrenceFrequency Frequency { get; }

    public IReadOnlyCollection<DayOfWeek> DaysOfWeek { get; }

    private RecurrenceRule(RecurrenceFrequency frequency, IReadOnlyCollection<DayOfWeek> daysOfWeek)
    {
        Frequency = frequency;
        DaysOfWeek = daysOfWeek;
    }

    public static RecurrenceRule Daily() => new(RecurrenceFrequency.Daily, Array.Empty<DayOfWeek>());

    
    public static RecurrenceRule Weekly(IEnumerable<DayOfWeek> days)
    {
        var distinctDays = (days ?? throw new ArgumentNullException(nameof(days)))
            .Distinct()
            .OrderBy(d => d)
            .ToArray();

        if (distinctDays.Length == 0)
        {
            throw new ArgumentException("At least one day of the week must be specified for a weekly recurrence.", nameof(days));
        }

        return new RecurrenceRule(RecurrenceFrequency.Weekly, distinctDays);
    }

    public bool Equals(RecurrenceRule? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Frequency == other.Frequency && DaysOfWeek.SequenceEqual(other.DaysOfWeek);
    }

    public override bool Equals(object? obj) => Equals(obj as RecurrenceRule);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Frequency);
        foreach (var day in DaysOfWeek)
        {
            hash.Add(day);
        }
        return hash.ToHashCode();
    }

    public override string ToString() => Frequency switch
    {
        RecurrenceFrequency.Daily => "Every day",
        RecurrenceFrequency.Weekly => $"Weekly on {string.Join(", ", DaysOfWeek)}",
        _ => "Unknown recurrence"
    };
}
