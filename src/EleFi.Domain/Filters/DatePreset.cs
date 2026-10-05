namespace EleFi.Domain.Filters;

/// <summary>A named date range offered as one tap, on the list's filters and the dashboard.</summary>
public enum DatePreset
{
    /// <summary>No date restriction.</summary>
    AllTime = 0,

    /// <summary>Just today.</summary>
    Today = 1,

    /// <summary>Monday of this week to today.</summary>
    ThisWeek = 2,

    /// <summary>The first of this month to today.</summary>
    ThisMonth = 3,

    /// <summary>The whole of last month.</summary>
    LastMonth = 4,

    /// <summary>1 April to today: the Indian financial year an accountant asks for.</summary>
    ThisFinancialYear = 5,

    /// <summary>Dates and times the user chose.</summary>
    Custom = 6,
}

/// <summary>Turns a preset into actual dates.</summary>
public static class DatePresets
{
    /// <summary>The inclusive date range a preset covers, as of a given day.</summary>
    /// <remarks>
    /// Weeks start on Monday, the convention in India and in ISO 8601. Every range ends
    /// today rather than at the end of its period, because nothing after today exists yet.
    /// <see cref="DatePreset.Custom"/> and <see cref="DatePreset.AllTime"/> have no dates.
    /// </remarks>
    /// <param name="preset">The preset.</param>
    /// <param name="today">Today, from the device clock.</param>
    public static (DateOnly? From, DateOnly? To) Range(this DatePreset preset, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        return preset switch
        {
            DatePreset.Today => (today, today),
            DatePreset.ThisWeek => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today),
            DatePreset.ThisMonth => (monthStart, today),
            DatePreset.LastMonth => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
            DatePreset.ThisFinancialYear => (
                today.Month >= 4 ? new DateOnly(today.Year, 4, 1) : new DateOnly(today.Year - 1, 4, 1),
                today),
            _ => (null, null),
        };
    }

    /// <summary>What the preset is called on screen.</summary>
    /// <param name="preset">The preset.</param>
    public static string DisplayName(this DatePreset preset) => preset switch
    {
        DatePreset.Today => "Today",
        DatePreset.ThisWeek => "This week",
        DatePreset.ThisMonth => "This month",
        DatePreset.LastMonth => "Last month",
        DatePreset.ThisFinancialYear => "This FY",
        DatePreset.Custom => "Custom range",
        _ => "All time",
    };
}
