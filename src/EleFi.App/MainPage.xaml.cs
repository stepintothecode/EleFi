using EleFi.App.Services;
using EleFi.Application.Abstractions;

namespace EleFi.App;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Routes the system back gesture into the app rather than letting it close the page.
    /// </summary>
    /// <remarks>
    /// This page is the only page, so the default behaviour finished the activity from any
    /// screen. Returning true keeps it open; the UI decides what back means.
    /// </remarks>
    protected override bool OnBackButtonPressed()
    {
        var back = Handler?.MauiContext?.Services.GetService<ISystemBack>() as MauiSystemBack;
        return back?.Raise() == true || base.OnBackButtonPressed();
    }
}
