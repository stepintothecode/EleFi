using Android.Content;
using Android.Content.PM;
using EleFi.Application.Abstractions;
using AndroidApplication = Android.App.Application;
using AndroidSettings = Android.Provider.Settings;

namespace EleFi.App.Services;

/// <summary>
/// The SMS permission, the notification permission, and notification access, on Android.
/// </summary>
/// <remarks>
/// <para>
/// Notification access has no runtime prompt. Android only lets the user grant it from a
/// system screen, so the best the app can do is open that screen and check afterwards.
/// </para>
/// <para>
/// On Android 13 and later, a sideloaded app's notification access (and, on some builds, its
/// SMS permission) is a "restricted setting" until the user allows it from the app's info
/// screen. The UI says so; nothing here can bypass it, and nothing should.
/// </para>
/// </remarks>
public sealed class AndroidAlertAccess : IAlertAccess
{
    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public bool CanReadSms =>
        AndroidApplication.Context.CheckSelfPermission(global::Android.Manifest.Permission.ReceiveSms) == Permission.Granted;

    /// <inheritdoc />
    public bool CanReadNotifications
    {
        get
        {
            var context = AndroidApplication.Context;
            var enabled = AndroidSettings.Secure.GetString(context.ContentResolver, "enabled_notification_listeners");
            // A colon-separated list of "package/ComponentClass". Compared by exact package,
            // so a sibling build such as ".debug" is not mistaken for this one.
            return (enabled ?? string.Empty)
                .Split(':', StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Split('/')[0])
                .Contains(context.PackageName, StringComparer.Ordinal);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RequestSmsAsync()
    {
        var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<ReceiveSms>).ConfigureAwait(false);

        // Asked together: an SMS read with no way to say so is a feature that looks dead.
        await RequestPromptsAsync().ConfigureAwait(false);
        return status == PermissionStatus.Granted;
    }

    /// <inheritdoc />
    public async Task<bool> RequestPromptsAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return true;
        }

        var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<PostNotifications>).ConfigureAwait(false);
        return status == PermissionStatus.Granted;
    }

    /// <inheritdoc />
    public void OpenNotificationAccessSettings()
    {
        using var intent = new Intent(AndroidSettings.ActionNotificationListenerSettings);
        intent.SetFlags(ActivityFlags.NewTask);
        AndroidApplication.Context.StartActivity(intent);
    }

    /// <summary>RECEIVE_SMS alone. MAUI's own Sms permission also asks to send.</summary>
    private sealed class ReceiveSms : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [(global::Android.Manifest.Permission.ReceiveSms, true)];
    }

    /// <summary>POST_NOTIFICATIONS, a runtime permission from Android 13.</summary>
    private sealed class PostNotifications : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [("android.permission.POST_NOTIFICATIONS", true)];
    }
}
