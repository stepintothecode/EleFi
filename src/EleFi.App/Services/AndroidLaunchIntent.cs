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
    private readonly List<Action> _listeners = [];
    private readonly Lock _gate = new();

    /// <summary>Creates the adapter and forwards the activity's event.</summary>
    public AndroidLaunchIntent() =>
        MainActivity.RouteRequested += Forward;

    /// <inheritdoc />
    /// <remarks>
    /// Only the newest listener hears it, for the same reason as the back button
    /// (<see cref="MauiSystemBack"/>): a rebuilt activity leaves the old router subscribed.
    /// </remarks>
    public event Action? RouteRequested
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_gate)
            {
                _listeners.Remove(value);
                _listeners.Add(value);
            }
        }

        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_gate)
            {
                _listeners.Remove(value);
            }
        }
    }

    private void Forward()
    {
        Action? newest;
        lock (_gate)
        {
            newest = _listeners.Count > 0 ? _listeners[^1] : null;
        }

        newest?.Invoke();
    }

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
