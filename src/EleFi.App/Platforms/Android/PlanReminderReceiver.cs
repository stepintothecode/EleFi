using Android.App;
using Android.Content;
using Android.Content.PM;
using EleFi.Application.Planning;
using Microsoft.Extensions.Logging;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// Posts a plan's reminder when its alarm fires, and handles the reminder's Snooze button.
/// </summary>
/// <remarks>
/// <para>
/// "Mark done" opens the app on the plan, ready to tick off, because ticking off may mean
/// picking or recording its transaction. "Snooze to tomorrow" needs nothing from the user, so
/// it happens here without opening the app: the plan's reminder moves to tomorrow and its due
/// date stays as it was.
/// </para>
/// <para>
/// Not exported: only EleFi's own alarms and notification can reach it, by explicit intent.
/// </para>
/// </remarks>
[BroadcastReceiver(Name = "com.stepintothecode.elefi.PlanReminderReceiver", Exported = false)]
public sealed class PlanReminderReceiver : BroadcastReceiver
{
    /// <summary>The alarm's action: post the reminder.</summary>
    public const string RemindAction = "com.stepintothecode.elefi.PLAN_REMIND";

    /// <summary>The notification's Snooze to tomorrow.</summary>
    public const string SnoozeAction = "com.stepintothecode.elefi.PLAN_SNOOZE";

    /// <summary>The plan's id.</summary>
    public const string PlanIdExtra = "elefi.plan_id";

    /// <summary>The notification's title.</summary>
    public const string TitleExtra = "elefi.plan_title";

    /// <summary>The notification's line.</summary>
    public const string BodyExtra = "elefi.plan_body";

    /// <summary>The channel reminders are posted on.</summary>
    public const string ChannelId = "elefi.plans";

    // Tags carry the plan, so every reminder can share one numeric id.
    private const int NotificationId = 5115;

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || !Guid.TryParse(intent?.GetStringExtra(PlanIdExtra), out var planId))
        {
            return;
        }

        switch (intent!.Action)
        {
            case RemindAction:
                Post(context, planId, intent.GetStringExtra(TitleExtra) ?? "A plan is due", intent.GetStringExtra(BodyExtra) ?? string.Empty);
                break;
            case SnoozeAction:
                if (context.GetSystemService(Context.NotificationService) is NotificationManager manager)
                {
                    manager.Cancel(planId.ToString(), NotificationId);
                }

                var pending = GoAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RunAsync(planner => planner.SnoozeAsync(planId)).ConfigureAwait(false);
                    }
                    finally
                    {
                        pending?.Finish();
                    }
                });
                break;
        }
    }

    /// <summary>Runs work against the planner in its own scope, as the alert receivers do.</summary>
    /// <param name="work">What to do.</param>
    internal static async Task RunAsync(Func<PlanService, Task> work)
    {
        var services = IPlatformApplication.Current?.Services;
        if (services is null)
        {
            return;
        }

        try
        {
            await using var scope = services.CreateAsyncScope();
            await work(scope.ServiceProvider.GetRequiredService<PlanService>()).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A receiver must never crash the process over one reminder.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            services.GetService<ILoggerFactory>()
                ?.CreateLogger("EleFi.Plans")
                .LogError("Plan reminder failed with {ExceptionType}.", ex.GetType().Name);
        }
    }

    private static void Post(Context context, Guid planId, string title, string body)
    {
        if ((OperatingSystem.IsAndroidVersionAtLeast(33)
                && context.CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
            || context.GetSystemService(Context.NotificationService) is not NotificationManager manager)
        {
            return;
        }

        EnsureChannel(manager);
        var requestCode = planId.GetHashCode();

        using var open = new Intent(context, typeof(MainActivity));
        open.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        open.PutExtra(MainActivity.RouteExtra, "plans");
        using var openPending = PendingIntent.GetActivity(
            context, requestCode, open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        // A different request code from the plain open, or Android would hand back that one.
        using var done = new Intent(context, typeof(MainActivity));
        done.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        done.PutExtra(MainActivity.RouteExtra, $"plans?tick={planId}");
        using var donePending = PendingIntent.GetActivity(
            context, requestCode + 1, done, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        using var snooze = new Intent(context, typeof(PlanReminderReceiver));
        snooze.SetAction(SnoozeAction);
        snooze.PutExtra(PlanIdExtra, planId.ToString());
        using var snoozePending = PendingIntent.GetBroadcast(
            context, requestCode, snooze, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        using var style = new Notification.BigTextStyle();
        style.BigText(body);

        using var doneAction = new Notification.Action.Builder(
            (global::Android.Graphics.Drawables.Icon?)null, "Mark done", donePending);
        using var snoozeAction = new Notification.Action.Builder(
            (global::Android.Graphics.Drawables.Icon?)null, "Snooze to tomorrow", snoozePending);

        using var builder = new Notification.Builder(context, ChannelId);
        builder
            .SetSmallIcon(Resource.Mipmap.appicon)!
            .SetContentTitle(title)!
            .SetContentText(body)!
            .SetStyle(style)!
            .SetContentIntent(openPending)!
            .SetAutoCancel(true)!
            .AddAction(doneAction.Build())!
            .AddAction(snoozeAction.Build());

        using var notification = builder.Build();
        manager.Notify(planId.ToString(), NotificationId, notification);
    }

    private static void EnsureChannel(NotificationManager manager)
    {
        if (manager.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        using var channel = new NotificationChannel(ChannelId, "Plan reminders", NotificationImportance.Default)
        {
            Description = "A reminder on the day a plan is due, with Mark done and Snooze to tomorrow.",
        };

        manager.CreateNotificationChannel(channel);
    }
}

/// <summary>Puts plan reminders back after the phone restarts, which clears every alarm.</summary>
[BroadcastReceiver(Name = "com.stepintothecode.elefi.PlanReminderBootReceiver", Exported = true)]
[IntentFilter([Intent.ActionBootCompleted])]
public sealed class PlanReminderBootReceiver : BroadcastReceiver
{
    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != Intent.ActionBootCompleted)
        {
            return;
        }

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await PlanReminderReceiver.RunAsync(planner => planner.RescheduleAsync()).ConfigureAwait(false);
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
