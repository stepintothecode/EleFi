using EleFi.Application.Abstractions;

namespace EleFi.App.Services;

/// <summary>
/// Asks the user for a file through the system picker.
/// </summary>
/// <remarks>
/// The system picker rather than a path the app browses: it means EleFi never needs storage
/// permission, and the user chooses exactly one file rather than granting access to all of
/// them.
/// </remarks>
public sealed class FilePick : IFilePick
{
    /// <inheritdoc />
    public async Task<string?> PickTextFileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Android reports JSON inconsistently across file providers: some say
            // application/json, Drive says octet-stream, and a file from a messaging app can
            // arrive as text/plain. Accepting all three beats a picker that greys out the
            // file the user is looking straight at.
            var types = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = ["application/json", "text/plain", "application/octet-stream"],
                });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose an EleFi backup",
                FileTypes = types,
            }).ConfigureAwait(false);

            if (result is null)
            {
                return null;
            }

            await using var stream = await result.OpenReadAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A cancelled or unavailable picker is not an error worth a crash. The caller
            // treats null as "the user changed their mind".
            return null;
        }
    }
}
