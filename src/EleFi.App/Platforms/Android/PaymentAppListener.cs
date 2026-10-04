using Android.App;
using Android.Content;
using Android.Service.Notification;
using EleFi.Application.Suggestions;
using EleFi.Domain.Alerts;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// Reads payment notifications from the allow-listed Payment Apps (ADR-0014, roadmap S40).
/// </summary>
/// <remarks>
/// <para>
/// Android delivers every notification on the device to a listener, which is why this is
/// written as a gate first and a reader second. The posting package is compared with
/// <see cref="PaymentApps"/> before the notification's title or text is touched, and
/// everything else returns on the first line: chats, email, OTPs from any app.
/// </para>
/// <para>
/// Bound by the system only, through <c>BIND_NOTIFICATION_LISTENER_SERVICE</c>, and only
/// once the user grants notification access in system settings. Nothing here can ask for it.
/// </para>
/// </remarks>
[Service(
    Name = "com.stepintothecode.elefi.PaymentAppListener",
    Label = "EleFi payment capture",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
    Exported = true)]
[IntentFilter(["android.service.notification.NotificationListenerService"])]
public sealed class PaymentAppListener : NotificationListenerService
{
    /// <inheritdoc />
    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        // Gate one: who posted it. Nothing else is read for an app not on the list.
        var package = sbn?.PackageName;
        if (sbn is null || !PaymentApps.IsAllowListed(package))
        {
            return;
        }

        // Gate two: the in-app switch (FR-11.24).
        if (!new AlertCaptureSettings(new PreferencesSettingsStore()).PaymentAppsEnabled)
        {
            return;
        }

        var notification = sbn.Notification;
        if (notification is null || (notification.Flags & NotificationFlags.GroupSummary) != 0)
        {
            // A group summary repeats its children ("2 payments") and names no payee.
            return;
        }

        var extras = notification.Extras;
        var title = extras?.GetCharSequence(Notification.ExtraTitle);
        var text = extras?.GetCharSequence(Notification.ExtraBigText) ?? extras?.GetCharSequence(Notification.ExtraText);

        var combined = string.Join('\n', new[] { title, text }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (combined.Length == 0)
        {
            return;
        }

        // When, not PostTime. An app that updates a notification reposts it with a new
        // PostTime but the same When, and the fingerprint must not change with the repost
        // or the same payment would be offered twice (SM6).
        var raisedAt = notification.When > 0 ? notification.When : sbn.PostTime;
        var postedAt = DateTimeOffset.FromUnixTimeMilliseconds(raisedAt);
        _ = Task.Run(() => AlertIntake.RunAsync(h => h.HandleAsync(AlertChannel.PaymentApp, package!, combined, postedAt)));
    }
}
