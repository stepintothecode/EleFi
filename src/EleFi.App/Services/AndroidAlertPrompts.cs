using Android.App;
using Android.Content;
using Android.Content.PM;
using EleFi.App.Platforms.Android;
using EleFi.Application.Abstractions;
using AndroidApplication = Android.App.Application;

namespace EleFi.App.Services;

/// <summary>
/// Posts Alert Prompts as Android notifications: "₹450 to Zomato, recorded, needs review".
/// </summary>
/// <remarks>
/// <para>
/// One notification per transaction, tagged with its id, so a second alert completing the
/// same transaction updates the prompt already showing rather than adding another.
/// </para>
/// <para>
/// Tapping opens the transaction to check it (ADR-0015). Delete soft-deletes it, for an alert
/// that was not really a payment; it stays restorable from Settings.
/// </para>
/// </remarks>
public sealed class AndroidAlertPrompts : IAlertPromptSurface
{
    /// <summary>The notification channel prompts are posted on.</summary>
    public const string ChannelId = "elefi.recorded";

    // The channel's name from when alerts waited for confirmation (ADR-0010), removed so it
    // does not linger in system settings after ADR-0015.
    private const string RetiredChannelId = "elefi.suggestions";

    // Tags carry the identity, so every prompt can share one numeric id.
    private const int NotificationId = 4417;

    /// <inheritdoc />
    public void Show(AlertPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var context = AndroidApplication.Context;
        if (!CanPost(context) || context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        EnsureChannel(manager);

        var requestCode = prompt.TransactionId.GetHashCode();

        // Every Java peer here is disposed once the notification is posted: Android keeps
        // its own copies, and the managed wrappers are just handles.
        using var open = new Intent(context, typeof(MainActivity));
        open.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        open.PutExtra(MainActivity.RouteExtra, $"transactions/{prompt.TransactionId}");
        using var openPending = PendingIntent.GetActivity(
            context, requestCode, open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        using var dismiss = new Intent(context, typeof(AlertPromptActionReceiver));
        dismiss.SetAction(AlertPromptActionReceiver.DeleteAction);
        dismiss.PutExtra(AlertPromptActionReceiver.TransactionIdExtra, prompt.TransactionId.ToString());
        using var dismissPending = PendingIntent.GetBroadcast(
            context, requestCode, dismiss, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        using var style = new Notification.BigTextStyle();
        style.BigText(prompt.Body);

        using var dismissAction = new Notification.Action.Builder(
            (global::Android.Graphics.Drawables.Icon?)null, "Delete", dismissPending);
        using var reviewAction = new Notification.Action.Builder(
            (global::Android.Graphics.Drawables.Icon?)null, "Open", openPending);

        using var builder = new Notification.Builder(context, ChannelId);
        builder
            .SetSmallIcon(Resource.Mipmap.appicon)!
            .SetContentTitle(prompt.Title)!
            .SetContentText(prompt.Body)!
            .SetStyle(style)!
            .SetContentIntent(openPending)!
            .SetAutoCancel(true)!
            .SetOnlyAlertOnce(true)!
            .AddAction(dismissAction.Build())!
            .AddAction(reviewAction.Build());

        using var notification = builder.Build();
        manager.Notify(prompt.TransactionId.ToString(), NotificationId, notification);
    }

    /// <inheritdoc />
    public void Withdraw(Guid transactionId)
    {
        if (AndroidApplication.Context.GetSystemService(Context.NotificationService) is NotificationManager manager)
        {
            manager.Cancel(transactionId.ToString(), NotificationId);
        }
    }

    private static bool CanPost(Context context) =>
        !OperatingSystem.IsAndroidVersionAtLeast(33)
        || context.CheckSelfPermission("android.permission.POST_NOTIFICATIONS") == Permission.Granted;

    private static void EnsureChannel(NotificationManager manager)
    {
        if (manager.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        // Default importance: it should sound like a normal notification, not interrupt
        // like a call. Nothing is lost if it is ignored: the payment is already recorded.
        using var channel = new NotificationChannel(ChannelId, "Recorded payments", NotificationImportance.Default)
        {
            Description = "Payments EleFi recorded from a bank SMS or a payment app, flagged for review.",
        };

        manager.CreateNotificationChannel(channel);
        manager.DeleteNotificationChannel(RetiredChannelId);
    }
}
