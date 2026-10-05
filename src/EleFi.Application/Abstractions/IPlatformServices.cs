namespace EleFi.Application.Abstractions;

/// <summary>
/// Opens a URL outside the app.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of a Blazor Hybrid trap with real consequences: a plain
/// <c>&lt;a href&gt;</c> navigates the host <c>BlazorWebView</c>, so the user ends up
/// inside the app with no browser chrome and no way back. Every outbound link goes through
/// here instead (FR-12.4).
/// </para>
/// <para>
/// It is also what keeps the support link honest. A payment page opened inside an app's own
/// web view reads, to a store reviewer, as billing that bypassed the store.
/// </para>
/// </remarks>
public interface ILinkOpener
{
    /// <summary>Opens a URL in the system browser.</summary>
    /// <param name="url">The absolute URL.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task OpenAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>Hands a generated file to the OS so the user can save or send it.</summary>
public interface IFileShare
{
    /// <summary>
    /// Writes bytes to a temporary file and offers it through the share sheet.
    /// </summary>
    /// <param name="fileName">The suggested filename, including extension.</param>
    /// <param name="contents">The file contents.</param>
    /// <param name="title">The share-sheet title.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The path the file was written to.</returns>
    Task<string> ShareAsync(
        string fileName,
        ReadOnlyMemory<byte> contents,
        string title,
        CancellationToken cancellationToken = default);
}

/// <summary>How the mascot is feeling. Drives its state machine.</summary>
public enum MascotMood
{
    /// <summary>Resting. Breathing, blinking, nothing more.</summary>
    Idle = 0,

    /// <summary>Something good happened: a transaction saved, a goal reached.</summary>
    Happy = 1,

    /// <summary>Spending is running high, or a card bill is due.</summary>
    Concerned = 2,

    /// <summary>Working: a long export, a backup.</summary>
    Thinking = 3,

    /// <summary>Something failed.</summary>
    Sad = 4,

    /// <summary>Ears folded over its eyes: amounts are hidden.</summary>
    Hiding = 5,
}

/// <summary>
/// The elephant mascot's animation surface.
/// </summary>
/// <remarks>
/// <para>
/// The UI never talks to JavaScript or to a specific animation runtime. It calls this, and
/// the implementation owns <c>IJSRuntime</c>, the Rive state machine, and the fallback
/// renderer. Swapping Rive for Lottie is then one class, not a search through components.
/// </para>
/// <para>
/// Per NFR-2.7 the mascot reacts to events but is never on the critical path of an
/// interaction. Every method here is safe to ignore, safe to fail, and safe to call before
/// the runtime has loaded: a mascot that blocks a save is a bug, not a delight.
/// </para>
/// </remarks>
public interface IMascotService : IAsyncDisposable
{
    /// <summary>True once the runtime has loaded and can accept state changes.</summary>
    bool IsReady { get; }

    /// <summary>
    /// Loads the animation runtime into a canvas.
    /// </summary>
    /// <param name="canvasElementId">The DOM id of the canvas to draw into.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task InitialiseAsync(string canvasElementId, CancellationToken cancellationToken = default);

    /// <summary>Settles the mascot into a mood.</summary>
    /// <param name="mood">The mood.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task SetMoodAsync(MascotMood mood, CancellationToken cancellationToken = default);

    /// <summary>Plays a one-off celebration, then returns to the current mood.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task CheerAsync(CancellationToken cancellationToken = default);

    /// <summary>Plays a one-off concerned reaction, then returns to the current mood.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task ConcernAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Which screen the app was asked to open, when it was opened by something other than the
/// launcher icon.
/// </summary>
/// <remarks>
/// The widget and the Quick Settings tile want quick capture; a Suggestion Prompt wants the
/// suggestion inbox. This is the seam that lets the UI find that out without knowing an
/// Android activity exists.
/// </remarks>
public interface ILaunchIntent
{
    /// <summary>
    /// The base-relative route this launch asked for, or null. Reading it clears it.
    /// </summary>
    /// <remarks>
    /// Consuming rather than peeking, so one tap produces exactly one navigation. A request
    /// left set would send the user back to that screen the next time anything re-rendered
    /// the router.
    /// </remarks>
    string? ConsumeRequestedRoute();

    /// <summary>Raised when an already-running app is asked for a screen.</summary>
    event Action? RouteRequested;
}

/// <summary>What an Alert Prompt says.</summary>
/// <param name="TransactionId">The transaction it recorded. Also its notification identity.</param>
/// <param name="Title">The headline: "₹450 to Zomato".</param>
/// <param name="Body">The detail: which container, which app, and that it needs review.</param>
public sealed record AlertPrompt(Guid TransactionId, string Title, string Body);

/// <summary>
/// Raises and withdraws Alert Prompts: the notification that says a payment was just
/// recorded from an SMS or a Payment App, flagged for review.
/// </summary>
/// <remarks>
/// Tapping one opens the transaction. Its Delete button soft-deletes it, restorable from
/// Settings, for the alert that was not really a payment.
/// </remarks>
public interface IAlertPromptSurface
{
    /// <summary>Shows a prompt, or replaces the one already showing for that transaction.</summary>
    /// <param name="prompt">What it says.</param>
    void Show(AlertPrompt prompt);

    /// <summary>Removes a transaction's prompt.</summary>
    /// <param name="transactionId">The transaction.</param>
    void Withdraw(Guid transactionId);
}

/// <summary>
/// The platform permissions behind automatic capture, and their state.
/// </summary>
/// <remarks>
/// NFR-10.7: on a platform that cannot read SMS or notifications, <see cref="IsSupported"/>
/// is false and the UI shows nothing about the feature at all, rather than a broken switch.
/// </remarks>
public interface IAlertAccess
{
    /// <summary>True on a platform where alerts can be read at all.</summary>
    bool IsSupported { get; }

    /// <summary>True when the app may receive SMS.</summary>
    bool CanReadSms { get; }

    /// <summary>True when the user has granted notification access in system settings.</summary>
    bool CanReadNotifications { get; }

    /// <summary>Asks for the SMS permission, along with permission to post prompts.</summary>
    /// <returns>True when SMS can now be read.</returns>
    Task<bool> RequestSmsAsync();

    /// <summary>Asks for permission to post Suggestion Prompts.</summary>
    /// <returns>True when prompts can be posted.</returns>
    Task<bool> RequestPromptsAsync();

    /// <summary>
    /// Opens the system screen where notification access is granted. There is no runtime
    /// prompt for it: Android only lets the user grant it there.
    /// </summary>
    void OpenNotificationAccessSettings();
}

/// <summary>
/// The device's own back control: the back gesture, or the back button on older phones.
/// </summary>
/// <remarks>
/// Left alone, Android's back closes the activity, which in a single-activity app means the
/// whole app disappears from any screen. This seam lets the UI decide what back means
/// instead: close the open sheet, return to the previous screen, and only leave the app
/// from the dashboard.
/// </remarks>
public interface ISystemBack
{
    /// <summary>Raised when the user presses back. The app has already been stopped from closing.</summary>
    event Action? Pressed;

    /// <summary>
    /// Sends the app to the background, as back from the root screen normally would.
    /// </summary>
    /// <remarks>
    /// Backgrounded rather than finished, so returning to it is instant and nothing
    /// half-typed on another screen is thrown away.
    /// </remarks>
    void LeaveApp();
}

/// <summary>
/// Destroys everything on the device.
/// </summary>
/// <remarks>
/// The one place in the app that hard-deletes user-entered data. Soft delete exists so
/// nothing is lost by accident; this is the deliberate exception, and it is why the UI
/// makes the user type the word out.
/// </remarks>
public interface IDataWipe
{
    /// <summary>
    /// Removes every container, transaction, label, suggestion, and audit row.
    /// </summary>
    /// <remarks>
    /// Re-seeds afterwards, so the app is left in the state a fresh install would be in:
    /// the starter labels and the parse rules. An empty label table is legal, but dropping
    /// the user on a blank Settings page is not what "start again" means.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    Task WipeEverythingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes and reads the whole database as one JSON file the user keeps.
/// </summary>
/// <remarks>
/// The stopgap until encrypted Drive backup exists. Deliberately the simplest thing that
/// makes the data recoverable: plain JSON, no format of our own, readable by anything.
/// </remarks>
public interface ILocalBackup
{
    /// <summary>Serialises everything to a JSON document.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Backup.BackupFile> ExportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces everything on the device with the contents of a backup.
    /// </summary>
    /// <remarks>
    /// Replace, not merge. Merging two ledgers needs a conflict rule for every field and
    /// produces a third state that matches neither, which is the last thing anyone wants
    /// from a restore. The UI says plainly what will be lost first.
    /// </remarks>
    /// <param name="file">The parsed backup.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<int> RestoreAsync(Backup.BackupFile file, CancellationToken cancellationToken = default);
}

/// <summary>Lets the user hand a file to the app.</summary>
public interface IFilePick
{
    /// <summary>
    /// Asks the user for a file and returns its text, or null if they backed out.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<string?> PickTextFileAsync(CancellationToken cancellationToken = default);
}

/// <summary>Small values that outlive a session but are not user data.</summary>
/// <remarks>
/// Home currency, cycle start day, theme, and the remembered export columns. Never money,
/// never a balance: those are derived from the database, and a cached copy is a second
/// source of truth waiting to disagree.
/// </remarks>
public interface ISettingsStore
{
    /// <summary>Reads a setting, or returns the fallback.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">Value to return when the key is absent.</param>
    string Read(string key, string fallback);

    /// <summary>Writes a setting.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    void Write(string key, string value);
}
