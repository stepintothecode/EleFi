namespace EleFi.Domain.Planning;

/// <summary>How often a plan comes round again.</summary>
public enum RepeatFrequency
{
    /// <summary>Once only.</summary>
    None = 0,

    /// <summary>Every N days.</summary>
    Daily = 1,

    /// <summary>Every N weeks, on the same weekday or on chosen days.</summary>
    Weekly = 2,

    /// <summary>Every N months, on the same day of the month or the last day.</summary>
    Monthly = 3,

    /// <summary>Every N years, on the same date.</summary>
    Yearly = 4,
}

/// <summary>The days of the week a weekly plan falls on. None means "the day it was first due".</summary>
[Flags]
public enum RepeatDays
{
    /// <summary>No days chosen: keep to the weekday the plan was first due on.</summary>
    None = 0,

    /// <summary>Monday.</summary>
    Monday = 1,

    /// <summary>Tuesday.</summary>
    Tuesday = 2,

    /// <summary>Wednesday.</summary>
    Wednesday = 4,

    /// <summary>Thursday.</summary>
    Thursday = 8,

    /// <summary>Friday.</summary>
    Friday = 16,

    /// <summary>Saturday.</summary>
    Saturday = 32,

    /// <summary>Sunday.</summary>
    Sunday = 64,
}

/// <summary>
/// When a repeating plan is next due.
/// </summary>
/// <remarks>
/// <para>
/// Monthly and yearly repeats keep to their <b>anchor day</b>, the day of the month the plan
/// was first due on. A SIP on the 31st falls on the 30th in April and the 28th in February,
/// then returns to the 31st in March, rather than drifting to the 28th for ever after the
/// first short month. An anchor of <see cref="LastDay"/> means "the last day of every month".
/// </para>
/// <para>
/// A weekly repeat can fall on chosen days ("Mondays and Thursdays"). Every N weeks then
/// means every chosen day of one week, then N - 1 weeks off.
/// </para>
/// </remarks>
/// <param name="Frequency">How often.</param>
/// <param name="Interval">Every how many of them. One or more.</param>
/// <param name="Days">For weekly: the days it falls on, or none for the day it was first due.</param>
public readonly record struct RepeatRule(RepeatFrequency Frequency, int Interval = 1, RepeatDays Days = RepeatDays.None)
{
    /// <summary>The anchor day that means "the last day of the month".</summary>
    public const int LastDay = 31;

    private static readonly (RepeatDays Day, string Name)[] Names =
    [
        (RepeatDays.Monday, "Mon"), (RepeatDays.Tuesday, "Tue"), (RepeatDays.Wednesday, "Wed"),
        (RepeatDays.Thursday, "Thu"), (RepeatDays.Friday, "Fri"), (RepeatDays.Saturday, "Sat"),
        (RepeatDays.Sunday, "Sun"),
    ];

    /// <summary>A plan that happens once.</summary>
    public static RepeatRule Never { get; } = new(RepeatFrequency.None);

    /// <summary>True when the plan comes round again.</summary>
    public bool Repeats => Frequency != RepeatFrequency.None && Interval >= 1;

    /// <summary>The flag for one day of the week.</summary>
    /// <param name="day">The day.</param>
    public static RepeatDays DayOf(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => RepeatDays.Monday,
        DayOfWeek.Tuesday => RepeatDays.Tuesday,
        DayOfWeek.Wednesday => RepeatDays.Wednesday,
        DayOfWeek.Thursday => RepeatDays.Thursday,
        DayOfWeek.Friday => RepeatDays.Friday,
        DayOfWeek.Saturday => RepeatDays.Saturday,
        _ => RepeatDays.Sunday,
    };

    /// <summary>The next due date after this one, or null when it does not repeat.</summary>
    /// <param name="due">The date just completed.</param>
    /// <param name="anchorDay">The day of the month the series keeps to, 1 to 31; 31 is the last day.</param>
    public DateOnly? NextAfter(DateOnly due, int anchorDay)
    {
        if (!Repeats)
        {
            return null;
        }

        return Frequency switch
        {
            RepeatFrequency.Daily => due.AddDays(Interval),
            RepeatFrequency.Weekly when Days != RepeatDays.None => NextChosenDay(due),
            RepeatFrequency.Weekly => due.AddDays(7 * Interval),
            RepeatFrequency.Monthly => OnAnchor(due.AddMonths(Interval), anchorDay),
            RepeatFrequency.Yearly => OnAnchor(due.AddYears(Interval), anchorDay),
            _ => null,
        };
    }

    /// <summary>Plain words for the rule: "Every month", "Every week on Mon, Thu".</summary>
    /// <param name="anchorDay">The series' anchor day, so the last day of the month can be said.</param>
    public string Describe(int anchorDay = 1)
    {
        var every = (Frequency, Interval) switch
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

        if (Frequency == RepeatFrequency.Weekly && Days != RepeatDays.None)
        {
            var days = this.Days;
            return $"{every} on {string.Join(", ", Names.Where(n => days.HasFlag(n.Day)).Select(n => n.Name))}";
        }

        return Frequency == RepeatFrequency.Monthly && anchorDay >= LastDay ? $"{every} on the last day" : every;
    }

    private DateOnly NextChosenDay(DateOnly due)
    {
        var monday = due.AddDays(-(((int)due.DayOfWeek + 6) % 7));

        // Later this week, if another chosen day is left in it.
        for (var day = due.AddDays(1); day < monday.AddDays(7); day = day.AddDays(1))
        {
            if (Days.HasFlag(DayOf(day.DayOfWeek)))
            {
                return day;
            }
        }

        // Otherwise the first chosen day of the next week it is due in.
        var week = monday.AddDays(7 * Interval);
        for (var i = 0; i < 7; i++)
        {
            if (Days.HasFlag(DayOf(week.AddDays(i).DayOfWeek)))
            {
                return week.AddDays(i);
            }
        }

        return due.AddDays(7 * Interval);
    }

    private static DateOnly OnAnchor(DateOnly inMonth, int anchorDay)
    {
        var day = Math.Clamp(anchorDay, 1, DateTime.DaysInMonth(inMonth.Year, inMonth.Month));
        return new DateOnly(inMonth.Year, inMonth.Month, day);
    }
}
