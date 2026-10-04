using Android.App;
using Android.Content;
using Android.Content.PM;
using EleFi.App.Platforms.Android;
using EleFi.Application.Abstractions;
using AndroidApplication = Android.App.Application;

namespace EleFi.App.Services;

/// <summary>
/// Posts Suggestion Prompts as Android notifications.
/// </summary>
/// <remarks>
/// <para>
/// One notification per suggestion, tagged with its id, so a second alert merged into the
/// same suggestion updates the prompt already showing rather than adding another.
/// </para>
/// <para>
/// Tapping opens the suggestion inbox. The only button is Dismiss; recording money is done
/// in the inbox, where the container and labels can be checked first (SM2).
/// </para>
/// </remarks>
public sealed class AndroidSuggestionPrompts : ISuggestionPromptSurface
{
    /// <summary>The notification channel prompts are posted on.</summary>
    public const string ChannelId = "elefi.suggestions";

    // Tags carry the identity, so every prompt can share one numeric id.
    private const int NotificationId = 4417;

    /// <inheritdoc />
    public void Show(SuggestionPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var context = AndroidApplication.Context;
        if (!CanPost(context) || context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        EnsureChannel(manager);

        var requestCode = prompt.SuggestionId.GetHashCode();

        // Every Java peer here is disposed once the notification is posted: Android keeps
        // its own copies, and the managed wrappers are just handles.
        using var open = new Intent(context, typeof(MainActivity));
        open.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        open.PutExtra(MainActivity.RouteExtra, "suggestions");
        using var openPending = PendingIntent.GetActivity(
            context, requestCode, open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        using var dismiss = new Intent(context, typeof(SuggestionPromptActionReceiver));
        dismiss.SetAction(SuggestionPromptActionReceiver.DismissAction);
        dismiss.PutExtra(SuggestionPromptActionReceiver.SuggestionIdExtra, prompt.SuggestionId.ToString());
        using var dismissPending = PendingIntent.GetBroadcast(
            context, requestCode, dismiss, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        using var style = new Notification.BigTextStyle();
        style.BigText(prompt.Body);

        using var dismissAction = new Notification.Action.Builder(
            (global::Android.Graphics.Drawables.Icon?)null, "Dismiss", dismissPending);
        using var reviewAction = new Notification.Action.Builder(
            (global::Android.Graphics.Drawables.Icon?)null, "Review", openPending);

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
        manager.Notify(prompt.SuggestionId.ToString(), NotificationId, notification);
    }

    /// <inheritdoc />
    public void Withdraw(Guid suggestionId)
    {
        if (AndroidApplication.Context.GetSystemService(Context.NotificationService) is NotificationManager manager)
        {
            manager.Cancel(suggestionId.ToString(), NotificationId);
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
        // like a call. Nothing is lost if it is ignored; the inbox keeps it for a week.
        using var channel = new NotificationChannel(ChannelId, "Payments to confirm", NotificationImportance.Default)
        {
            Description = "Payments EleFi spotted in a bank SMS or a payment app, waiting for you to confirm.",
        };

        manager.CreateNotificationChannel(channel);
    }
}
