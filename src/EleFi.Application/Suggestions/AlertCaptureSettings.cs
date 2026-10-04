using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;

namespace EleFi.Application.Suggestions;

/// <summary>
/// The in-app switches for automatic capture, one per channel (FR-11.24).
/// </summary>
/// <remarks>
/// <para>
/// Off until the user turns them on. A feature that reads messages has to be an offer, and
/// granting the permission in system settings is not the same as asking for the feature.
/// </para>
/// <para>
/// Separate from the permission on purpose: turning a switch off here stops reading at once,
/// without a trip to system settings, and turning it back on does not need the permission
/// asked for again.
/// </para>
/// </remarks>
/// <param name="store">Where small per-device settings live.</param>
public sealed class AlertCaptureSettings(ISettingsStore store)
{
    /// <summary>The setting key for reading bank SMS.</summary>
    public const string SmsKey = "alerts.sms.enabled";

    /// <summary>The setting key for reading Payment App notifications.</summary>
    public const string PaymentAppsKey = "alerts.apps.enabled";

    /// <summary>Whether bank SMS are read.</summary>
    public bool SmsEnabled
    {
        get => Read(SmsKey);
        set => store.Write(SmsKey, value ? "true" : "false");
    }

    /// <summary>Whether Payment App notifications are read.</summary>
    public bool PaymentAppsEnabled
    {
        get => Read(PaymentAppsKey);
        set => store.Write(PaymentAppsKey, value ? "true" : "false");
    }

    /// <summary>Whether alerts from this channel are read.</summary>
    /// <param name="channel">The channel.</param>
    public bool IsEnabled(AlertChannel channel) => channel switch
    {
        AlertChannel.Sms => SmsEnabled,
        AlertChannel.PaymentApp => PaymentAppsEnabled,
        _ => false,
    };

    private bool Read(string key) =>
        string.Equals(store.Read(key, "false"), "true", StringComparison.OrdinalIgnoreCase);
}
