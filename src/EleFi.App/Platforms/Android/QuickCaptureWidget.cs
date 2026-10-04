using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// The home-screen widget: one tap into quick capture.
/// </summary>
/// <remarks>
/// <para>
/// A widget draws through <c>RemoteViews</c>, which supports a fixed set of stock views and
/// no custom drawing, no canvas, and no animation. The three-step arc therefore cannot live
/// here. The widget is a launcher for it, and the flow itself runs in the app where it can
/// actually animate.
/// </para>
/// <para>
/// Deliberately not showing a balance yet. Reading one means opening the encrypted database
/// from a broadcast receiver on every widget update, and a widget is not worth waking
/// SQLCipher for. When the balance does arrive it will come from a cached value written on
/// app exit, not from a query here.
/// </para>
/// </remarks>
[BroadcastReceiver(Label = "EleFi quick capture", Exported = true)]
[IntentFilter(["android.appwidget.action.APPWIDGET_UPDATE"])]
[MetaData("android.appwidget.provider", Resource = "@xml/quick_capture_widget")]
public sealed class QuickCaptureWidget : AppWidgetProvider
{
    /// <inheritdoc />
    public override void OnUpdate(
        Context? context,
        AppWidgetManager? appWidgetManager,
        int[]? appWidgetIds)
    {
        // Nullable to match the base signature. Android really can hand a null context to a
        // receiver being torn down, and crashing there would show the user a system dialog
        // about an app they were not even using.
        if (context is null || appWidgetManager is null || appWidgetIds is null)
        {
            return;
        }

        foreach (var id in appWidgetIds)
        {
            using var views = new RemoteViews(context.PackageName, Resource.Layout.quick_capture_widget);

            using var intent = new Intent(context, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            intent.PutExtra(MainActivity.QuickCaptureExtra, true);

            using var pending = PendingIntent.GetActivity(
                context,
                0,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            views.SetOnClickPendingIntent(Resource.Id.widget_root, pending);
            appWidgetManager.UpdateAppWidget(id, views);
        }
    }
}
