using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace EleFi.App;

/// <summary>
/// The single activity hosting the WebView.
/// </summary>
/// <remarks>
/// Also the landing point for the widget, the Quick Settings tile, and Suggestion Prompts,
/// each of which asks for a particular screen rather than the dashboard.
/// </remarks>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>Intent extra set by the widget and the tile to open quick capture.</summary>
    public const string QuickCaptureExtra = "elefi.quick_capture";

    /// <summary>Intent extra naming a base-relative route to open, such as <c>suggestions</c>.</summary>
    public const string RouteExtra = "elefi.route";

    private static readonly object Gate = new();
    private static string? _requestedRoute;

    /// <summary>Raised when a running app is asked for a screen.</summary>
    public static event Action? RouteRequested;

    /// <summary>
    /// Reads the requested route and clears it, so one launch produces one navigation.
    /// </summary>
    /// <remarks>
    /// Static because the Blazor side has no handle on the activity, and the window is
    /// short: it is read once during startup routing, or once per <see cref="OnNewIntent"/>.
    /// <see cref="LaunchMode.SingleTask"/> means a second tap reuses this activity.
    /// </remarks>
    public static string? ConsumeRequestedRoute()
    {
        lock (Gate)
        {
            var route = _requestedRoute;
            _requestedRoute = null;
            return route;
        }
    }

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Remember(Intent);
        base.OnCreate(savedInstanceState);
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // The app was already running, so OnCreate will not fire again. Without this, the
        // second tap on the widget would just bring the dashboard forward.
        if (Remember(intent))
        {
            RouteRequested?.Invoke();
        }
    }

    private static bool Remember(Intent? intent)
    {
        var route = intent?.GetBooleanExtra(QuickCaptureExtra, false) == true
            ? "quick"
            : intent?.GetStringExtra(RouteExtra);

        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        lock (Gate)
        {
            _requestedRoute = route;
        }

        return true;
    }
}
