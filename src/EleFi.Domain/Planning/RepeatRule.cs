namespace EleFi.Domain.Planning;

/// <summary>How often a plan comes round again.</summary>
public enum RepeatFrequency
{
    /// <summary>Once only.</summary>
    None = 0,

    /// <summary>Every N days.</summary>
    Daily = 1,

    /// <summary>Every N weeks, on the same weekday.</summary>
    Weekly = 2,

    /// <summary>Every N months, on the same day of the month.</summary>
    Monthly = 3,

    /// <summary>Every N years, on the same date.</summary>
    Yearly = 4,
}

/// <summary>
/// When a repeating plan is next due.
/// </summary>
/// <remarks>
/// Monthly and yearly repeats keep to their <b>anchor day</b>, the day of the month the plan
/// was first due on. A SIP on the 31st falls on the 30th in April and the 28th in February,
/// then returns to the 31st in March, rather than drifting to the 28th for ever after the
/// first short month.
/// </remarks>
/// <param name="Frequency">How often.</param>
/// <param name="Interval">Every how many of them. One or more.</param>
public readonly record struct RepeatRule(RepeatFrequency Frequency, int Interval = 1)
{
    /// <summary>A plan that happens once.</summary>
    public static RepeatRule Never { get; } = new(RepeatFrequency.None);

    /// <summary>True when the plan comes round again.</summary>
    public bool Repeats => Frequency != RepeatFrequency.None && Interval >= 1;

    /// <summary>The next due date after this one, or null when it does not repeat.</summary>
    /// <param name="due">The date just completed.</param>
    /// <param name="anchorDay">The day of the month the series keeps to, 1 to 31.</param>
    public DateOnly? NextAfter(DateOnly due, int anchorDay)
    {
        if (!Repeats)
        {
            return null;
        }

        return Frequency switch
        {
            RepeatFrequency.Daily => due.AddDays(Interval),
            RepeatFrequency.Weekly => due.AddDays(7 * Interval),
            RepeatFrequency.Monthly => OnAnchor(due.AddMonths(Interval), anchorDay),
            RepeatFrequency.Yearly => OnAnchor(due.AddYears(Interval), anchorDay),
            _ => null,
        };
    }

    /// <summary>Plain words for the rule: "Every month", "Every 2 weeks".</summary>
    public string Describe() => (Frequency, Interval) switch
    {
        (RepeatFrequency.None, _) => "Once",
        (RepeatFrequency.Daily, 1) => "Every day",
        (RepeatFrequency.Weekly, 1) => "Every week",
        (RepeatFrequency.Monthly, 1) => "Every month",
        (RepeatFrequency.Yearly, 1) => "Every year",
        (RepeatFrequency.Daily, var n) => $"Every {n} days",
        (RepeatFrequency.Weekly, var n) => $"Every {n} weeks",
        (RepeatFrequency.Monthly, var n) => $"Every {n} months",
        (RepeatFrequency.Yearly, var n) => $"Every {n} years",
        _ => "Once",
    };

    private static DateOnly OnAnchor(DateOnly inMonth, int anchorDay)
    {
        var day = Math.Clamp(anchorDay, 1, DateTime.DaysInMonth(inMonth.Year, inMonth.Month));
        return new DateOnly(inMonth.Year, inMonth.Month, day);
    }
}
