using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// Raises <see cref="IAppResume.Resumed"/> when the app's window comes back to the front.
/// </summary>
/// <remarks>
/// Every listener is called, each in its own try: a screen whose WebView was rebuilt may have
/// left a listener behind, and one that fails must not stop the screen on show refreshing.
/// </remarks>
public sealed class MauiAppResume : IAppResume
{
    private readonly List<Action> _listeners = [];
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public event Action? Resumed
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_gate)
            {
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

    /// <summary>Tells every listener the app is back.</summary>
    public void Raise()
    {
        Action[] listeners;
        lock (_gate)
        {
            listeners = [.. _listeners];
        }

        foreach (var listener in listeners)
        {
            try
            {
                listener();
            }
#pragma warning disable CA1031 // A stale screen failing to refresh must not stop the live one.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }
}
