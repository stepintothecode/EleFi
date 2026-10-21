using Android.Content;

namespace EleFi.App;

/// <summary>
/// Holds what another app last shared into EleFi until the receipt screen reads it.
/// </summary>
/// <remarks>
/// <para>
/// The image is copied out of the share at once, while the sending app's permission to read
/// it is certain to hold, and kept only in memory: it reaches the database only if the user
/// attaches it, and never touches app storage (ADR-0018).
/// </para>
/// <para>
/// One at a time. A second share replaces the first, which matches what the user sees: the
/// receipt screen shows the latest thing shared.
/// </para>
/// </remarks>
internal static class SharedReceiptIntake
{
    /// <summary>The route the receipt screen lives at.</summary>
    public const string Route = "receipt";

    private static readonly Lock Gate = new();
    private static bool _waiting;
    private static byte[]? _image;
    private static string? _text;

    /// <summary>Takes a share intent's image or text, if it is one; true when it was.</summary>
    /// <param name="context">To read the shared image through.</param>
    /// <param name="intent">The intent the activity received.</param>
    public static bool Accept(Context context, Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend)
        {
            return false;
        }

        var isImage = intent.Type?.StartsWith("image/", StringComparison.Ordinal) == true;
        var image = isImage && SharedUri(intent) is { } uri ? Read(context, uri) : null;
        var text = intent.GetStringExtra(Intent.ExtraText);

        // An image that could not be read still opens the receipt screen, which says it could
        // not find anything on it, rather than the share silently doing nothing.
        if (!isImage && string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        lock (Gate)
        {
            (_waiting, _image, _text) = (true, image, text);
        }

        return true;
    }

    // A share whose permission was revoked, or a link to something gone, must never take the
    // app down: it was crashing here, on the main thread, before anything was on screen.
    private static byte[]? Read(Context context, Android.Net.Uri uri)
    {
        try
        {
            using var stream = context.ContentResolver?.OpenInputStream(uri);
            using var copy = new MemoryStream();
            stream?.CopyTo(copy);
            return copy.Length > 0 ? copy.ToArray() : null;
        }
        catch (Exception e) when (e is Java.Lang.SecurityException or Java.IO.IOException or IOException)
        {
            return null;
        }
    }

    // Android 13 typed the lookup and deprecated the untyped one.
    private static Android.Net.Uri? SharedUri(Intent intent) =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) as Android.Net.Uri
            : intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;

    /// <summary>
    /// Whether anything was shared since the last take, with its image and text, and forgets
    /// them. Waiting with neither is a share that could not be read.
    /// </summary>
    public static (bool Waiting, byte[]? Image, string? Text) Take()
    {
        lock (Gate)
        {
            var taken = (_waiting, _image, _text);
            (_waiting, _image, _text) = (false, null, null);
            return taken;
        }
    }
}
