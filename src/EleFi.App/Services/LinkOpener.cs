using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// Opens URLs in the system browser.
/// </summary>
/// <remarks>
/// <see cref="BrowserLaunchMode.External"/> is the whole point. The default would open a
/// custom tab inside the app, and under Blazor Hybrid a plain anchor would navigate the
/// host WebView itself, stranding the user with no browser chrome and no way back
/// (FR-12.4).
/// <para>
/// Android also needs the https intent query declared in the manifest, or this call fails
/// silently and the button appears simply not to work.
/// </para>
/// </remarks>
public sealed class LinkOpener : ILinkOpener
{
    /// <inheritdoc />
    public async Task OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        try
        {
            await Browser.Default.OpenAsync(new Uri(url), BrowserLaunchMode.External);
        }
        catch (Exception)
        {
            // No browser, or the intent query is missing. Nothing useful to show the user
            // beyond the button not working, and a crash would be worse.
        }
    }
}
