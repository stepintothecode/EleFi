using EleFi.App.Services;
using EleFi.Application.Abstractions;

namespace EleFi.App;

/// <summary>The only page: the BlazorWebView that hosts the whole UI.</summary>
public partial class MainPage : ContentPage
{
    /// <summary>Creates the page and routes the back gesture into the UI.</summary>
    public MainPage()
    {
        InitializeComponent();

        // Once the WebView exists, so our back handler is registered after its own and runs
        // first. See SystemBackCallback for why there must be only one, and WebViewBackKey for
        // the presses that a focused WebView used to keep for itself.
        blazorWebView.BlazorWebViewInitialized += (_, e) =>
        {
#if ANDROID
            if (blazorWebView.Handler?.MauiContext?.Services.GetService<ISystemBack>() is MauiSystemBack back)
            {
                SystemBackCallback.Install(back);
            }

            WebViewBackKey.Install(e.WebView);
#endif
        };
    }
}
