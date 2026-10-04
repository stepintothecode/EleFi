using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Ui.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>
/// The mascot component, rendered without a device, an emulator, or a WebView.
/// </summary>
/// <remarks>
/// This test is the reason <c>EleFi.Ui</c> is a separate <c>net10.0</c> library rather than
/// living inside the Android app project. A test runner cannot reference
/// <c>net10.0-android</c>, so components there would be permanently untestable.
/// </remarks>
public class MascotViewerTests : Bunit.TestContext
{
    [Fact]
    public void It_renders_a_canvas_with_an_accessible_name()
    {
        var mascot = new RecordingMascot();
        Services.AddSingleton<IMascotService>(mascot);

        var component = RenderComponent<MascotViewer>(p => p.Add(x => x.Mood, MascotMood.Concerned));

        var canvas = component.Find("canvas");

        // NFR-6.4: a canvas is invisible to a screen reader without this.
        Assert.Equal("img", canvas.GetAttribute("role"));
        Assert.Contains("concerned", canvas.GetAttribute("aria-label"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void It_initialises_the_runtime_once_and_applies_the_mood()
    {
        var mascot = new RecordingMascot();
        Services.AddSingleton<IMascotService>(mascot);

        var component = RenderComponent<MascotViewer>(p => p.Add(x => x.Mood, MascotMood.Happy));
        component.Render();

        Assert.Equal(1, mascot.Initialisations);
        Assert.Contains(MascotMood.Happy, mascot.Moods);
    }

    [Fact]
    public void A_failing_runtime_never_breaks_the_screen()
    {
        // NFR-2.7: the mascot is never on the critical path. If interop throws, the page
        // that shows the user's money still renders.
        Services.AddSingleton<IMascotService>(new ThrowingMascot());

        var component = RenderComponent<MascotViewer>();

        Assert.NotNull(component.Find("canvas"));
    }

    private sealed class RecordingMascot : IMascotService
    {
        public bool IsReady => true;

        public int Initialisations { get; private set; }

        public List<MascotMood> Moods { get; } = [];

        public Task InitialiseAsync(string canvasElementId, CancellationToken cancellationToken = default)
        {
            Initialisations++;
            return Task.CompletedTask;
        }

        public Task SetMoodAsync(MascotMood mood, CancellationToken cancellationToken = default)
        {
            Moods.Add(mood);
            return Task.CompletedTask;
        }

        public Task CheerAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ConcernAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingMascot : IMascotService
    {
        public bool IsReady => false;

        public Task InitialiseAsync(string canvasElementId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The WebView is gone.");

        public Task SetMoodAsync(MascotMood mood, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The WebView is gone.");

        public Task CheerAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The WebView is gone.");

        public Task ConcernAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The WebView is gone.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
