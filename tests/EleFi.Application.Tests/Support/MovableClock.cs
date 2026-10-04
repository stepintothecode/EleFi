using EleFi.Application.Abstractions;

namespace EleFi.Application.Tests.Support;

/// <summary>A clock that only moves when a test moves it.</summary>
internal sealed class MovableClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 1, 13, 0, 0, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    public TimeOnly TimeOfDay => TimeOnly.FromDateTime(UtcNow.UtcDateTime);
}
