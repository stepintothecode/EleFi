using Android.App;
using Android.Content;
using Android.Service.QuickSettings;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// The Quick Settings tile: pull down, tap, record.
/// </summary>
/// <remarks>
/// <para>
/// The tile does not capture anything itself. It opens the app on the quick-capture route,
/// because the three-step flow needs a real window: a tile is a button and a label, and the
/// animated stepper cannot live there.
/// </para>
/// <para>
/// This is the cheapest quick-capture surface on Android. It needs no permission, no
/// background service, and nothing for an OEM battery manager to kill.
/// </para>
/// </remarks>
[Service(
    Name = "com.stepintothecode.elefi.QuickCaptureTileService",
    Label = "Record spend",
    Icon = "@mipmap/appicon",
    Permission = "android.permission.BIND_QUICK_SETTINGS_TILE",
    Exported = true)]
[IntentFilter(["android.service.quicksettings.action.QS_TILE"])]
public sealed class QuickCaptureTileService : TileService
{
    /// <inheritdoc />
    public override void OnStartListening()
    {
        base.OnStartListening();

        if (QsTile is not null)
        {
            QsTile.State = TileState.Inactive;
            QsTile.Label = "Record spend";
            QsTile.UpdateTile();
        }
    }

    /// <inheritdoc />
    public override void OnClick()
    {
        base.OnClick();

        // ClearTop rather than a new task, so tapping the tile while EleFi is already open
        // reuses the running app instead of stacking a second copy of it.
        using var intent = new Intent(this, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        intent.PutExtra(MainActivity.QuickCaptureExtra, true);

        // Android 34 replaced the Intent overload with a PendingIntent one and deprecated
        // the old shape. Both are needed: the app supports API 28 and up, and calling the
        // 34-only method on anything older throws at runtime rather than at build.
        //
        // Either way this collapses the shade as it launches. Without that the app opens
        // behind a still-open Quick Settings panel.
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            using var pending = PendingIntent.GetActivity(
                this,
                0,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            StartActivityAndCollapse(pending!);
        }
        else
        {
#pragma warning disable CA1422 // Deprecated in 34, and the only option below it.
            StartActivityAndCollapse(intent);
#pragma warning restore CA1422
        }
    }
}
