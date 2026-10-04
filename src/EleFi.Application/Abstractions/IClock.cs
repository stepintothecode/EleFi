namespace EleFi.Application.Abstractions;

/// <summary>
/// The current time.
/// </summary>
/// <remarks>
/// Nothing in the application or domain calls <c>DateTimeOffset.UtcNow</c> directly. A
/// tracker whose behaviour depends on today's date has to be testable across dates, and a
/// test that waits for midnight is not a test.
/// </remarks>
public interface IClock
{
    /// <summary>The current instant, UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Today in the device's timezone, as a calendar date.</summary>
    /// <remarks>
    /// Local rather than UTC on purpose: a transaction at 1am in Chennai happened today,
    /// not yesterday, and defaulting the capture date to a UTC "today" would put it on the
    /// wrong day for half the evening.
    /// </remarks>
    DateOnly Today { get; }

    /// <summary>
    /// The current time of day in the device's timezone.
    /// </summary>
    /// <remarks>
    /// Local, and paired with <see cref="Today"/>. Capture defaults both to now, because
    /// the overwhelmingly common case is recording something that just happened.
    /// </remarks>
    TimeOnly TimeOfDay { get; }
}

/// <summary>The real clock.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <inheritdoc />
    public TimeOnly TimeOfDay => TimeOnly.FromDateTime(DateTime.Now);
}
