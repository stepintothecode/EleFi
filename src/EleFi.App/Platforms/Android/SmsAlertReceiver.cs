using Android.App;
using Android.Content;
using Android.Provider;
using EleFi.Application.Suggestions;
using EleFi.Domain.Alerts;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// Receives incoming SMS and hands each one to the alert handler (roadmap S38).
/// </summary>
/// <remarks>
/// <para>
/// Declared with <c>BROADCAST_SMS</c> as its permission, so only the system can deliver to
/// it: no other app can feed it a fake bank message.
/// </para>
/// <para>
/// The message text exists as a local here and in the closure below for as long as one
/// parse takes, then goes out of scope. It is never written anywhere (SM1). It is parsed
/// here rather than handed to WorkManager, because WorkManager would have to serialise it
/// to disk to survive a process death, and that is exactly what SM1 forbids.
/// </para>
/// <para>
/// <see cref="BroadcastReceiver.GoAsync"/> buys about ten seconds, which a parse and one
/// database write fit inside many times over.
/// </para>
/// </remarks>
[BroadcastReceiver(
    Name = "com.stepintothecode.elefi.SmsAlertReceiver",
    Enabled = true,
    Exported = true,
    Permission = "android.permission.BROADCAST_SMS")]
[IntentFilter(["android.provider.Telephony.SMS_RECEIVED"])]
public sealed class SmsAlertReceiver : BroadcastReceiver
{
    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != Telephony.Sms.Intents.SmsReceivedAction)
        {
            return;
        }

        // The in-app switch, checked before a single message is assembled (FR-11.24).
        if (!new AlertCaptureSettings(new PreferencesSettingsStore()).SmsEnabled)
        {
            return;
        }

        var parts = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
        if (parts is null || parts.Length == 0)
        {
            return;
        }

        // A long message arrives in parts, all from one sender. Joined, it is the message
        // the bank sent; parsed separately, the amount and the account can land in
        // different parts and neither would match.
        var sender = parts[0]?.OriginatingAddress;
        if (string.IsNullOrWhiteSpace(sender))
        {
            return;
        }

        var text = string.Concat(parts.Where(p => p is not null).Select(p => p!.MessageBody));
        var receivedAt = DateTimeOffset.FromUnixTimeMilliseconds(parts[0]!.TimestampMillis);

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await AlertIntake.RunAsync(h => h.HandleAsync(AlertChannel.Sms, sender, text, receivedAt))
                    .ConfigureAwait(false);
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
