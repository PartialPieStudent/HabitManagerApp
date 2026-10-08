using Habits.Domain.ValueObjects;

namespace Habits.Domain.Entities;

public sealed class Habit
{
    public const int MaxTitleLength = 200;

    public Guid Id { get; }

    public string UserId { get; }

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public RecurrenceRule RecurrenceRule { get; private set; }

    private readonly List<HabitCompletion> _completions = new();

    /// <summary>
    /// Read-only view of every completion recorded for this habit.
    /// </summary>
    public IReadOnlyCollection<HabitCompletion> Completions => _completions.AsReadOnly();

    private Habit(
        Guid id,
        string userId,
        string title,
        string? description,
        RecurrenceRule recurrenceRule,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Description = description;
        IsActive = true;
        RecurrenceRule = recurrenceRule;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Habit Create(string userId, string title, string? description, RecurrenceRule? recurrenceRule = null)
    {
        var normalizedUserId = ValidateUserId(userId);
        var normalizedTitle = ValidateTitle(title);

        return new Habit(
            Guid.NewGuid(),
            normalizedUserId,
            normalizedTitle,
            NormalizeDescription(description),
            recurrenceRule ?? RecurrenceRule.Daily(),
            DateTimeOffset.UtcNow);
    }

    public void UpdateDetails(string title, string? description, RecurrenceRule? recurrenceRule = null)
    {
        Title = ValidateTitle(title);
        Description = NormalizeDescription(description);

        if (recurrenceRule is not null)
        {
            RecurrenceRule = recurrenceRule;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        if (!IsActive) return;

        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        if (IsActive) return;

        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public HabitCompletion MarkCompleted(DateOnly date)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException($"Habit '{Title}' is archived and can no longer be marked as completed.");
        }

        if (IsCompletedOn(date))
        {
            throw new InvalidOperationException($"Habit '{Title}' already has a completion record for {date:yyyy-MM-dd}.");
        }

        var completion = HabitCompletion.Create(Id, date);
        _completions.Add(completion);
        UpdatedAt = DateTimeOffset.UtcNow;

        return completion;
    }

    
    public bool UnmarkCompleted(DateOnly date)
    {
        var removed = _completions.RemoveAll(c => c.Date == date) > 0;
        if (removed)
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }
        return removed;
    }

    public bool IsCompletedOn(DateOnly date) => _completions.Any(c => c.Date == date);

    public bool IsCompletedToday() => IsCompletedOn(DateOnly.FromDateTime(DateTime.UtcNow));

    private static string ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("A habit must be assigned to a user (UserId cannot be null or empty).", nameof(userId));
        }

        return userId.Trim();
    }

    private static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Habit title cannot be empty or whitespace.", nameof(title));
        }

        var trimmed = title.Trim();

        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Habit title must not exceed {MaxTitleLength} characters.", nameof(title));
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
