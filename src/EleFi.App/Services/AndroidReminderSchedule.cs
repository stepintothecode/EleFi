using Android.App;
using Android.Content;
using EleFi.App.Platforms.Android;
using EleFi.Application.Abstractions;
using EleFi.Domain.Planning;
using AndroidApplication = Android.App.Application;

namespace EleFi.App.Services;

/// <summary>
/// Plan reminders as Android alarms: one per plan, at the moment <see cref="PlanReminders"/>
/// worked out.
/// </summary>
/// <remarks>
/// <para>
/// Every change replaces the whole set. The plans that had an alarm are remembered in
/// Preferences, so the next replace can cancel exactly those, including one for a plan that
/// has since been deleted, done or snoozed.
/// </para>
/// <para>
/// <c>SetAndAllowWhileIdle</c>, not an exact alarm: a reminder a few minutes late is fine, and
/// exact alarms need a permission the user has to grant in system settings. The alarm carries
/// only the plan's id, title and the line to show; the receiver posts the notification.
/// </para>
/// </remarks>
public sealed class AndroidReminderSchedule : IReminderSchedule
{
    // Comfortably under Android's 500 alarms per app, and far more than anyone has due.
    private const int MaximumAlarms = 60;

    private const string ScheduledKey = "reminders.scheduled";

    /// <inheritdoc />
    public void Replace(IReadOnlyList<PlanReminder> reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        var context = AndroidApplication.Context;
        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarms)
        {
            return;
        }

        foreach (var id in Scheduled())
        {
            using var stale = Pending(context, id, null);
            alarms.Cancel(stale);
        }

        var kept = new List<Guid>();
        foreach (var reminder in reminders.OrderBy(r => r.FireAt).Take(MaximumAlarms))
        {
            using var pending = Pending(context, reminder.PlanId, reminder);
            alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, reminder.FireAt.ToUnixTimeMilliseconds(), pending);
            kept.Add(reminder.PlanId);
        }

        Preferences.Default.Set(ScheduledKey, string.Join(',', kept));
    }

    private static IEnumerable<Guid> Scheduled() =>
        Preferences.Default.Get(ScheduledKey, string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(text => Guid.TryParse(text, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty);

    // Same request code and same action for the same plan, so a later call finds and replaces
    // or cancels the earlier alarm. The extras do not take part in that match.
    private static PendingIntent Pending(Context context, Guid planId, PlanReminder? reminder)
    {
        using var intent = new Intent(context, typeof(PlanReminderReceiver));
        intent.SetAction(PlanReminderReceiver.RemindAction);
        intent.PutExtra(PlanReminderReceiver.PlanIdExtra, planId.ToString());
        if (reminder is not null)
        {
            intent.PutExtra(PlanReminderReceiver.TitleExtra, reminder.Title);
            intent.PutExtra(PlanReminderReceiver.BodyExtra, reminder.Body);
        }

        return PendingIntent.GetBroadcast(
            context, planId.GetHashCode(), intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }
}
