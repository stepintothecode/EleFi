namespace EleFi.Ui.Services;

/// <summary>Shows stored JPEG bytes in an image tag.</summary>
/// <remarks>
/// A data URI rather than a file or a URL: the photo lives in the encrypted database and never
/// touches app storage, so there is no file to point at.
/// </remarks>
public static class ImageUri
{
    /// <summary>A data URI for a JPEG.</summary>
    /// <param name="jpeg">The image bytes.</param>
    public static string Of(byte[] jpeg)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        return "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
    }
}
