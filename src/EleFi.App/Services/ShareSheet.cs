using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// Writes a generated file to cache and offers it through the OS share sheet.
/// Named ShareSheet rather than FileShare because the latter collides with System.IO.FileShare.
/// </summary>
/// <remarks>
/// The cache directory, not app data: an export is a copy the user is taking away, not
/// state EleFi keeps. It is also what makes the file reachable by the app the user shares
/// it into.
/// </remarks>
public sealed class ShareSheet : IFileShare
{
    /// <inheritdoc />
    public async Task<string> ShareAsync(
        string fileName,
        ReadOnlyMemory<byte> contents,
        string title,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);

        await File.WriteAllBytesAsync(path, contents.ToArray(), cancellationToken).ConfigureAwait(false);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path),
        }).ConfigureAwait(false);

        return path;
    }
}
