using Android.Views;
using AndroidX.Activity;
using View = Android.Views.View;

namespace EleFi.App;

/// <summary>
/// Stops the WebView handling the back key itself, and sends it to the app's one back handler.
/// </summary>
/// <remarks>
/// <para>
/// <c>SystemBackCallback</c> owns back presses that reach the activity. Some never got there:
/// the BlazorWebView overrides <c>OnKeyDown</c> and steps its own browser history back
/// whenever it has focus and can. A screen that focuses a field on arrival gives it focus, and
/// quick capture does exactly that with its amount. So back on quick did two things: the
/// WebView went back a page to the dashboard, the back navigator noted the arrival, and the
/// same press then reached our callback, which found nothing behind the dashboard and left
/// the app. Labels and Deleted have no focused field, which is why they were already fixed.
/// </para>
/// <para>
/// A key listener runs before the view's own <c>OnKeyDown</c>. This one takes both halves of
/// the back key, so the WebView never sees it, and raises one back press, on release, through
/// the activity's dispatcher, where <c>SystemBackCallback</c> handles it like any other. One
/// press, one step, whichever view has focus.
/// </para>
/// </remarks>
public sealed class WebViewBackKey : Java.Lang.Object, View.IOnKeyListener
{
    /// <summary>Puts the listener on the WebView.</summary>
    /// <param name="webView">The BlazorWebView's platform view.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Owned by the WebView, which holds it for as long as it lives.")]
    public static void Install(View webView)
    {
        ArgumentNullException.ThrowIfNull(webView);
        webView.SetOnKeyListener(new WebViewBackKey());
    }

    /// <inheritdoc />
    public bool OnKey(View? v, Keycode keyCode, KeyEvent? e)
    {
        if (keyCode != Keycode.Back || e is null)
        {
            return false;
        }

        if (e.Action == KeyEventActions.Up && !e.IsCanceled && Platform.CurrentActivity is ComponentActivity activity)
        {
            activity.OnBackPressedDispatcher.OnBackPressed();
        }

        // Down and up both consumed: the WebView must never step its own history.
        return true;
    }
}
