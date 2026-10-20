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
/// form), then return to the screen that was open before, the way a browser does, the
/// dashboard included. Only when there is nothing left to go back to does back offer to close
/// the app, and even then the first press only asks: Ele says to press back again, and a
/// second press soon after closes it. One press never closes EleFi.
/// </para>
/// <para>
/// A screen that ends itself (quick capture cancelled, a transaction deleted, the guide moving
/// on) leaves through <see cref="Leave"/> or <see cref="ReplaceTo"/>, which replace it in
/// the history rather than stacking on top of it, so back never returns to something already
/// closed or gone.
/// </para>
/// <para>
/// The history is kept here rather than borrowed from the WebView, so it means the same thing
/// whichever way a screen was reached: a tab, a link, a notification, or the widget.
/// </para>
/// </remarks>
/// <param name="navigation">The router's navigation manager.</param>
/// <param name="system">The platform's back control, used only to leave the app.</param>
/// <param name="ele">Says "press back again" before the app closes. Optional for tests.</param>
/// <param name="time">The clock for the second press. Optional for tests.</param>
public sealed class BackNavigator(
    NavigationManager navigation,
    ISystemBack system,
    EleReactions? ele = null,
    TimeProvider? time = null) : IDisposable
{
    /// <summary>The most screens remembered. Older ones are forgotten first.</summary>
    public const int MaximumDepth = 30;

    /// <summary>How long after the first press a second one closes the app.</summary>
    public static TimeSpan ExitWindow { get; } = TimeSpan.FromSeconds(2.5);

    /// <summary>What Ele says on the first back press with nothing left to go back to.</summary>
    public const string ExitPrompt = "Press back again to close EleFi.";

    private const string Root = "";

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly List<string> _history = [];
    private readonly List<Func<Task>> _interceptions = [];
    private string? _arrivingBackAt;
    private string? _replacing;
    private DateTimeOffset? _exitArmedAt;
    private bool _started;

    /// <summary>The remembered screens, oldest first, as base-relative paths.</summary>
    public IReadOnlyList<string> History => _history;

    /// <summary>True when there is a previous screen to return to.</summary>
    /// <remarks>
    /// For in-page Close buttons, which should return to wherever the user came from but
    /// must never leave the app.
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

        _history.Add(current);
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
    /// <returns>True when the app handled it; false only when it closed the app.</returns>
    public async Task<bool> GoBackAsync()
    {
        if (_interceptions.Count > 0)
        {
            var onBack = _interceptions[^1];
            _interceptions.RemoveAt(_interceptions.Count - 1);
            await onBack().ConfigureAwait(true);
            return true;
        }

        if (StepBack())
        {
            return true;
        }

        // Nothing left to go back to. The first press asks; a second one soon after leaves.
        var now = _time.GetUtcNow();
        if (_exitArmedAt is { } armed && now - armed <= ExitWindow)
        {
            _exitArmedAt = null;
            system.LeaveApp();
            return false;
        }

        _exitArmedAt = now;
        ele?.Say(ExitPrompt, EleTone.Calm);
        return true;
    }

    /// <summary>
    /// Leaves the current screen for good: back to the screen before it when there is one,
    /// otherwise replaced by <paramref name="fallback"/>. Never adds a step to the history.
    /// </summary>
    /// <remarks>For a screen that has ended itself, such as a cancelled quick capture or a
    /// deleted transaction, which back must never return to.</remarks>
    /// <param name="fallback">Where to go when nothing is behind this screen.</param>
    public void Leave(string fallback)
    {
        if (!StepBack())
        {
            ReplaceTo(fallback);
        }
    }

    /// <summary>
    /// Shows another screen in place of the current one, without adding a step to the
    /// history.
    /// </summary>
    /// <param name="path">The screen to show.</param>
    public void ReplaceTo(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        _replacing = Relative(navigation.ToAbsoluteUri(path).ToString());
        navigation.NavigateTo(path, replace: true);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_started)
        {
            navigation.LocationChanged -= OnLocationChanged;
        }
    }

    // Back one screen in the history, if there is one.
    private bool StepBack()
    {
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
            ReplaceTo(Root);
            return true;
        }

        return false;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        var path = Relative(e.Location);

        // Any move means the user is still using the app: the next back is a step, not a
        // second "close" press.
        _exitArmedAt = null;

        if (_arrivingBackAt is not null && path == _arrivingBackAt)
        {
            _arrivingBackAt = null;
            return;
        }

        _arrivingBackAt = null;

        if (_replacing is not null && path == _replacing)
        {
            _replacing = null;
            if (_history.Count > 0)
            {
                _history[^1] = path;
            }
            else
            {
                _history.Add(path);
            }

            Collapse();
            return;
        }

        _replacing = null;
        Remember(path);
    }

    private void Remember(string path)
    {
        if (_history.Count > 0 && _history[^1] == path)
        {
            return;
        }

        _history.Add(path);

        if (_history.Count > MaximumDepth)
        {
            _history.RemoveAt(0);
        }
    }

    // A replace can leave the same screen twice in a row: one step, not two.
    private void Collapse()
    {
        if (_history.Count >= 2 && _history[^1] == _history[^2])
        {
            _history.RemoveAt(_history.Count - 1);
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
