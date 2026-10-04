namespace EleFi.Domain.Transactions;

/// <summary>
/// Where a transaction came from. Recorded on every one, and shown in its audit trail.
/// </summary>
/// <remarks>
/// Kept so the trail can say "created via widget", and so it is possible to ask whether
/// widget-captured transactions are more often wrong. That question is what tells you
/// whether the defaulting logic is any good.
/// </remarks>
public enum CaptureSource
{
    /// <summary>Typed into the app itself.</summary>
    App = 0,

    /// <summary>A home-screen widget.</summary>
    Widget = 1,

    /// <summary>A notification with inline reply.</summary>
    Notification = 2,

    /// <summary>The floating bubble.</summary>
    Bubble = 3,

    /// <summary>A Quick Settings tile.</summary>
    QuickTile = 4,

    /// <summary>Shared in from another app.</summary>
    ShareSheet = 5,

    /// <summary>
    /// The user confirmed a Capture Suggestion parsed from a bank message.
    /// </summary>
    /// <remarks>
    /// This never means the app created the transaction by itself. A suggestion becomes a
    /// transaction only when a human agrees (SM2).
    /// </remarks>
    Sms = 6,

    /// <summary>A bulk import.</summary>
    Import = 7,

    /// <summary>Restored from a backup.</summary>
    Restore = 8,

    /// <summary>
    /// The user confirmed a Capture Suggestion that a Payment App notification raised, with
    /// no bank SMS behind it.
    /// </summary>
    /// <remarks>
    /// Like <see cref="Sms"/>, this never means the app created it alone (SM2). A suggestion
    /// both channels described is recorded as <see cref="Sms"/>, because the bank's message
    /// is the one that names the account.
    /// </remarks>
    PaymentApp = 9,
}
