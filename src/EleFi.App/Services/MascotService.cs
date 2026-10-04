using EleFi.Application.Abstractions;
using Microsoft.JSInterop;

namespace EleFi.App.Services;

/// <summary>
/// Drives the mascot through a JavaScript module.
/// </summary>
/// <remarks>
/// <para>
/// The only class in the app that holds <c>IJSRuntime</c> for the mascot. Components call
/// <see cref="IMascotService"/>, so replacing the canvas placeholder with a Rive state
/// machine is a change to this class and one JS file (ADR-0011).
/// </para>
/// <para>
/// Every method swallows its exceptions. Per NFR-2.7 the mascot is never on the critical
/// path of an interaction, and the fastest way to violate that is to let a JS interop
/// failure bubble into a save. A missing elephant is a cosmetic problem; a failed save is
/// not.
/// </para>
/// </remarks>
public sealed class MascotService(IJSRuntime js) : IMascotService
{
    private const string ModulePath = "./js/elefi-mascot.js";

    private IJSObjectReference? _module;
    private string? _canvasId;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsReady => _module is not null && _canvasId is not null;

    /// <inheritdoc />
    public async Task InitialiseAsync(string canvasElementId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath)
                .ConfigureAwait(false);

            _canvasId = canvasElementId;
            await _module.InvokeVoidAsync("initialise", cancellationToken, canvasElementId).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The WebView may be tearing down, or the module may be missing in a trimmed
            // build. Either way the app carries on without an elephant.
        }
    }

    /// <inheritdoc />
    public async Task SetMoodAsync(MascotMood mood, CancellationToken cancellationToken = default)
    {
        if (!IsReady || _disposed)
        {
            return;
        }

        try
        {
            await _module!.InvokeVoidAsync("setMood", cancellationToken, _canvasId, mood.ToString())
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Decoration never interrupts.
        }
    }

    /// <inheritdoc />
    public Task CheerAsync(CancellationToken cancellationToken = default) =>
        PulseAsync(MascotMood.Happy, 900, cancellationToken);

    /// <inheritdoc />
    public Task ConcernAsync(CancellationToken cancellationToken = default) =>
        PulseAsync(MascotMood.Concerned, 1200, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // A BlazorWebView leaks module handles readily, so this is real cleanup rather than
        // bookkeeping. It is also why the interface extends IAsyncDisposable at all.
        try
        {
            if (_module is not null)
            {
                if (_canvasId is not null)
                {
                    await _module.InvokeVoidAsync("dispose", _canvasId).ConfigureAwait(false);
                }

                await _module.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // The runtime is already gone. Nothing to clean up and nobody to tell.
        }
        finally
        {
            _module = null;
            _canvasId = null;
        }
    }

    private async Task PulseAsync(MascotMood mood, int milliseconds, CancellationToken cancellationToken)
    {
        if (!IsReady || _disposed)
        {
            return;
        }

        try
        {
            await _module!.InvokeVoidAsync("pulse", cancellationToken, _canvasId, mood.ToString(), milliseconds)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // As above.
        }
    }
}
