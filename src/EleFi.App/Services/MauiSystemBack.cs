using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// Hands the system back gesture to the UI instead of letting it close the app.
/// </summary>
/// <remarks>
/// <see cref="MainPage"/> forwards every back press here and reports it handled, so the
/// activity is never finished by back alone. The UI's back navigator then decides: close a
/// sheet, step back a screen, or call <see cref="LeaveApp"/> from the dashboard.
/// </remarks>
public sealed class MauiSystemBack : ISystemBack
{
    /// <inheritdoc />
    public event Action? Pressed;

    /// <summary>
    /// Forwards a back press. False when nothing is listening yet, such as before the
    /// first render, so the caller can fall back to the platform's default.
    /// </summary>
    public bool Raise()
    {
        var handler = Pressed;
        if (handler is null)
        {
            return false;
        }

        handler();
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
