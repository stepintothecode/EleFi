using EleFi.Application.Abstractions;
using Microsoft.JSInterop;

namespace EleFi.App.Services;

/// <summary>
/// Plays Ele's cues through a JavaScript module that synthesises them with Web Audio.
/// </summary>
/// <remarks>
/// The only class holding <c>IJSRuntime</c> for sound. Every failure is swallowed: a cue is
/// decoration, and a missing one must never surface as an error (NFR-2.7).
/// </remarks>
/// <param name="js">The WebView's JavaScript runtime.</param>
public sealed class WebAudioSoundCue(IJSRuntime js) : ISoundCue
{
    private const string ModulePath = "./js/elefi-sound.js";

    private IJSObjectReference? _module;
    private bool _disposed;

    /// <inheritdoc />
    public async Task PlayAsync(SoundCue cue, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath).ConfigureAwait(false);
            await _module.InvokeVoidAsync("play", cancellationToken, cue.ToString()).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // No sound is no problem.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose").ConfigureAwait(false);
                await _module.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // The WebView is already gone.
        }
        finally
        {
            _module = null;
        }
    }
}
