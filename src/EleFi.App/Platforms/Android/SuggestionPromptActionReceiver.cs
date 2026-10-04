using Android.App;
using Android.Content;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// Handles the Dismiss button on a Suggestion Prompt without opening the app.
/// </summary>
/// <remarks>
/// Not exported: only EleFi's own notification can reach it, through an explicit intent.
/// Dismissing destroys the suggestion (SM5); there is no "Add" here, because recording
/// money needs the inbox, where the container and labels can be checked first.
/// </remarks>
[BroadcastReceiver(Name = "com.stepintothecode.elefi.SuggestionPromptActionReceiver", Exported = false)]
public sealed class SuggestionPromptActionReceiver : BroadcastReceiver
{
    /// <summary>The intent action for Dismiss.</summary>
    public const string DismissAction = "com.stepintothecode.elefi.DISMISS_SUGGESTION";

    /// <summary>The intent extra carrying the suggestion's id.</summary>
    public const string SuggestionIdExtra = "elefi.suggestion_id";

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != DismissAction
            || !Guid.TryParse(intent.GetStringExtra(SuggestionIdExtra), out var suggestionId))
        {
            return;
        }

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await AlertIntake.RunAsync(h => h.DismissFromPromptAsync(suggestionId)).ConfigureAwait(false);
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
