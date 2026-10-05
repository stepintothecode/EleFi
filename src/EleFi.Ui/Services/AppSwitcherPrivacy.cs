using EleFi.Application.Abstractions;

namespace EleFi.Ui.Services;

/// <summary>
/// Whether the app shows a blank card in the recent-apps view instead of the last screen.
/// </summary>
/// <remarks>
/// On by default. The recent-apps view is a screenshot of the last thing on screen, which
/// for this app is usually a balance, shown to anyone who picks the phone up and swipes.
/// The activity reads the setting each time it goes to the background, so a change here
/// applies from the next time the app is left.
/// </remarks>
/// <param name="store">Where small per-device settings live.</param>
public sealed class AppSwitcherPrivacy(ISettingsStore store)
{
    /// <summary>The settings key.</summary>
    public const string Key = "privacy.hide-in-recents";

    /// <summary>True while the recent-apps card is blanked.</summary>
    public bool Enabled => IsEnabled(store);

    /// <summary>Reads the setting from a store, for the activity, which has no container.</summary>
    /// <param name="store">The settings store.</param>
    public static bool IsEnabled(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return !string.Equals(store.Read(Key, "true"), "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Turns the blank card on or off.</summary>
    /// <param name="enabled">True to hide the app's content in the recent-apps view.</param>
    public void Set(bool enabled) => store.Write(Key, enabled ? "true" : "false");
}
