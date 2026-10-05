using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// Reports which screen the widget, the tile, or a Suggestion Prompt asked for.
/// </summary>
/// <remarks>
/// The only class that knows <c>MainActivity</c> exists. Everything above it asks
/// <see cref="ILaunchIntent"/>, which is what keeps the UI project free of Android and
/// therefore testable on a desktop runner.
/// </remarks>
public sealed class AndroidLaunchIntent : ILaunchIntent
{
    /// <summary>Creates the adapter and forwards the activity's event.</summary>
    public AndroidLaunchIntent() =>
        MainActivity.RouteRequested += () => RouteRequested?.Invoke();

    /// <inheritdoc />
    public event Action? RouteRequested;

    /// <inheritdoc />
    /// <remarks>
    /// The router asks once, on its first render, which is also the moment the boot screen
    /// is covered for good, so the window's floating Ele is stopped here too.
    /// </remarks>
    public string? ConsumeRequestedRoute()
    {
        MainActivity.EndBootScreen();
        return MainActivity.ConsumeRequestedRoute();
    }
}
