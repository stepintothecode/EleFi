using Android.App;
using Android.Content;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// Handles the Delete button on an Alert Prompt without opening the app.
/// </summary>
/// <remarks>
/// For an alert that was not really a payment: a promotional message that parsed, or a
/// payment that later failed. Not exported, so only EleFi's own notification can reach it,
/// through an explicit intent. The delete is soft and restorable from Settings.
/// </remarks>
[BroadcastReceiver(Name = "com.stepintothecode.elefi.AlertPromptActionReceiver", Exported = false)]
public sealed class AlertPromptActionReceiver : BroadcastReceiver
{
    /// <summary>The intent action for Delete.</summary>
    public const string DeleteAction = "com.stepintothecode.elefi.DELETE_RECORDED";

    /// <summary>The intent extra carrying the transaction's id.</summary>
    public const string TransactionIdExtra = "elefi.transaction_id";

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != DeleteAction
            || !Guid.TryParse(intent.GetStringExtra(TransactionIdExtra), out var transactionId))
        {
            return;
        }

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await AlertIntake.RunAsync(h => h.DeleteFromPromptAsync(transactionId)).ConfigureAwait(false);
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
