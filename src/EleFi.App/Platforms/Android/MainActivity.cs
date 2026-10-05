using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics.Drawables;
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
    Theme = "@style/EleFi.SplashTheme",
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
    private static MainActivity? _booting;
    private AnimatedVectorDrawable? _bootEle;

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

        // Between the system splash and the WebView's first paint, the window itself shows Ele
        // floating on the app's ground, and index.html's boot screen then takes over in place.
        Window?.SetBackgroundDrawableResource(Resource.Drawable.boot_window);
        if (Window?.DecorView.Background is LayerDrawable layers && layers.GetDrawable(1) is AnimatedVectorDrawable ele)
        {
            _bootEle = ele;
            _bootEle.Start();
            _booting = this;
        }
    }

    /// <summary>
    /// Stops Ele floating behind the WebView and leaves the plain ground, once Blazor is up.
    /// </summary>
    /// <remarks>
    /// An endless animation on the window background would keep redrawing it under the app
    /// for as long as it runs. Safe to call more than once and from any thread.
    /// </remarks>
    public static void EndBootScreen()
    {
        var activity = Interlocked.Exchange(ref _booting, null);
        activity?.RunOnUiThread(() =>
        {
            activity._bootEle?.Stop();
            activity._bootEle = null;
            activity.Window?.SetBackgroundDrawableResource(Resource.Color.colorPrimary);
        });
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
