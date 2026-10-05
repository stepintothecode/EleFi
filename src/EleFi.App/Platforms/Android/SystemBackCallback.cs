using AndroidX.Activity;
using EleFi.App.Services;

namespace EleFi.App;

/// <summary>
/// Takes the system back gesture before anything else in the activity sees it.
/// </summary>
/// <remarks>
/// <para>
/// The BlazorWebView registers its own back handler, which steps the WebView's browser
/// history back. Left alongside ours, one gesture did two things: the WebView went back a
/// page, our back navigator recorded that as a new visit and then stepped back again. From
/// a screen one step from the dashboard, that second step found nothing behind it and left
/// the app.
/// </para>
/// <para>
/// Callbacks run newest first, so this is added once the WebView exists and wins. It hands
/// the gesture to the UI's back navigator, which alone decides: close a sheet, go back a
/// screen, or leave from the dashboard.
/// </para>
/// </remarks>
/// <param name="back">Where back presses go.</param>
public sealed class SystemBackCallback(MauiSystemBack back) : OnBackPressedCallback(true)
{
    /// <summary>Puts the callback in front of every other back handler in the activity.</summary>
    /// <param name="back">Where back presses go.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Owned by the dispatcher, which drops it when the activity is destroyed.")]
    public static void Install(MauiSystemBack back)
    {
        if (Platform.CurrentActivity is ComponentActivity activity)
        {
            activity.OnBackPressedDispatcher.AddCallback(activity, new SystemBackCallback(back));
        }
    }

    /// <inheritdoc />
    public override void HandleOnBackPressed()
    {
        if (back.Raise())
        {
            return;
        }

        // Nothing listening yet, before the first render: let the platform do its default.
        Enabled = false;
        if (Platform.CurrentActivity is ComponentActivity activity)
        {
            activity.OnBackPressedDispatcher.OnBackPressed();
        }

        Enabled = true;
    }
}
