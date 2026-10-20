using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// Hands the system back gesture to the UI instead of letting it close the app.
/// </summary>
/// <remarks>
/// <para>
/// <c>SystemBackCallback</c> forwards every back press here and reports it handled, so the
/// activity is never finished by back alone. The UI's back navigator then decides: close a
/// sheet, step back a screen, or, on a second press with nothing left, call
/// <see cref="LeaveApp"/>.
/// </para>
/// <para>
/// Only the newest listener hears a press. This object lives as long as the process, but
/// Android rebuilds the activity, and with it the WebView and its navigator, whenever it
/// reclaims memory in the background. The old navigator never unsubscribed, still thought it
/// was on the dashboard, and closed the app on the same press the new one used to step back:
/// back from an open transaction closed EleFi. The newest listener is the screen on show.
/// </para>
/// </remarks>
public sealed class MauiSystemBack : ISystemBack
{
    private readonly List<Action> _listeners = [];
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public event Action? Pressed
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

    /// <summary>
    /// Forwards a back press to the newest listener. False when nothing is listening yet, such
    /// as before the first render, so the caller can fall back to the platform's default.
    /// </summary>
    public bool Raise()
    {
        Action? newest;
        lock (_gate)
        {
            newest = _listeners.Count > 0 ? _listeners[^1] : null;
        }

        if (newest is null)
        {
            return false;
        }

        newest();
        return true;
    }

    /// <inheritdoc />
    public void LeaveApp()
    {
#if ANDROID
        // Backgrounded rather than finished: coming back is instant, and it is what back
        // from a launcher's root screen does anyway.
        Platform.CurrentActivity?.MoveTaskToBack(true);
#endif
    }
}
