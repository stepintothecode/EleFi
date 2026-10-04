using EleFi.Application.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace EleFi.Ui.Services;

/// <summary>
/// Decides what the system back gesture does inside the app.
/// </summary>
/// <remarks>
/// <para>
/// In order: close whatever is open on top of the screen (a sheet, a step of a multi-step
/// form), then return to the previous screen, then return to the dashboard, and only from
/// the dashboard leave the app. Before this existed, back from any screen closed EleFi
/// outright, which on a capture form meant losing what had been typed.
/// </para>
/// <para>
/// The history is kept here rather than borrowed from the WebView, because the dashboard is
/// a root, not a page: reaching it by any route clears what was behind it. Otherwise back
/// from the dashboard would reopen a quick capture that had just been cancelled.
/// </para>
/// </remarks>
/// <param name="navigation">The router's navigation manager.</param>
/// <param name="system">The platform's back control, used only to leave the app.</param>
public sealed class BackNavigator(NavigationManager navigation, ISystemBack system) : IDisposable
{
    /// <summary>The most screens remembered. Older ones are forgotten first.</summary>
    public const int MaximumDepth = 30;

    private const string Root = "";

    private readonly List<string> _history = [];
    private readonly List<Func<Task>> _interceptions = [];
    private string? _arrivingBackAt;
    private bool _started;

    /// <summary>The remembered screens, oldest first, as base-relative paths.</summary>
    public IReadOnlyList<string> History => _history;

    /// <summary>True when there is a previous screen to return to.</summary>
    /// <remarks>
    /// For in-page Close buttons, which should return to wherever the user came from but
    /// must never leave the app the way back from the dashboard does.
    /// </remarks>
    public bool CanStepBack => _history.Count >= 2;

    /// <summary>Starts listening to navigation. Safe to call more than once.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        // A cold launch onto a deep screen still has the dashboard behind it.
        var current = Relative(navigation.Uri);
        if (current != Root)
        {
            _history.Add(Root);
        }

        Remember(current);
        navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>
    /// Asks to handle the next back press instead of the router, until released.
    /// </summary>
    /// <remarks>
    /// For anything drawn over the current screen: a sheet registers its close action here
    /// when it opens and releases it when it closes. The most recent interception runs
    /// first, and each runs at most once.
    /// </remarks>
    /// <param name="onBack">What to do when back is pressed.</param>
    /// <returns>Dispose to release the interception.</returns>
    public IDisposable Intercept(Func<Task> onBack)
    {
        ArgumentNullException.ThrowIfNull(onBack);

        _interceptions.Add(onBack);
        return new Release(() => _interceptions.Remove(onBack));
    }

    /// <summary>Does whatever back should do right now.</summary>
    /// <returns>True when the app handled it; false when it left the app.</returns>
    public async Task<bool> GoBackAsync()
    {
        if (_interceptions.Count > 0)
        {
            var onBack = _interceptions[^1];
            _interceptions.RemoveAt(_interceptions.Count - 1);
            await onBack().ConfigureAwait(true);
            return true;
        }

        if (_history.Count >= 2)
        {
            _history.RemoveAt(_history.Count - 1);
            var previous = _history[^1];

            // Marked so the arrival is not recorded as a fresh visit, which would make the
            // next back bounce forward again.
            _arrivingBackAt = previous;
            navigation.NavigateTo(previous);
            return true;
        }

        if (_history.Count == 1 && _history[0] != Root)
        {
            navigation.NavigateTo(Root);
            return true;
        }

        system.LeaveApp();
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_started)
        {
            navigation.LocationChanged -= OnLocationChanged;
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var path = Relative(e.Location);

        if (_arrivingBackAt is not null && path == _arrivingBackAt)
        {
            _arrivingBackAt = null;
            return;
        }

        _arrivingBackAt = null;
        Remember(path);
    }

    private void Remember(string path)
    {
        if (path == Root)
        {
            // The dashboard is the root. Arriving there by any route forgets the way in.
            _history.Clear();
            _history.Add(Root);
            return;
        }

        if (_history.Count > 0 && _history[^1] == path)
        {
            return;
        }

        _history.Add(path);

        if (_history.Count > MaximumDepth)
        {
            // Keep the root; forget the oldest screen after it.
            _history.RemoveAt(_history[0] == Root ? 1 : 0);
        }
    }

    private string Relative(string uri) => navigation.ToBaseRelativePath(uri).TrimEnd('/');

    private sealed class Release(Action release) : IDisposable
    {
        private Action? _release = release;

        public void Dispose()
        {
            _release?.Invoke();
            _release = null;
        }
    }
}
