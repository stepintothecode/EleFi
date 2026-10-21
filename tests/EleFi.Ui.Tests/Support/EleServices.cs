using EleFi.Application.Abstractions;
using EleFi.Ui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EleFi.Ui.Tests.Support;

/// <summary>
/// What every page now needs to say anything: Ele, its sound, and the undo history.
/// </summary>
/// <remarks>
/// Try-added, so a test that registered its own Ele first, to read what Ele said, keeps it.
/// Ele's guide is registered as already finished, so it does not open over pages whose tests
/// are about something else; the guide's own tests start it themselves.
/// </remarks>
internal static class EleServices
{
    /// <summary>Registers Ele, a silent sound, and an empty undo history.</summary>
    /// <param name="services">The test's services.</param>
    public static IServiceCollection AddEleAndUndo(this IServiceCollection services)
    {
        services.TryAddSingleton(new EleReactions(new Fakes.MemorySettings(), new ManualTime()));
        services.TryAddSingleton<ISoundCue>(new Fakes.RecordingSound());
        services.TryAddSingleton(new UndoHistory());
        services.TryAddSingleton<IAppResume>(new Fakes.ManualResume());
        services.TryAddSingleton<IFileShare>(new Fakes.NoShare());

        // Ele is on every empty and error screen now, so every page may draw the mascot.
        services.TryAddTransient<IMascotService, Fakes.SilentMascot>();

        var seen = new Fakes.MemorySettings();
        seen.Write(EleGuide.DoneKey, "true");
        services.TryAddSingleton(new EleGuide(seen));
        return services;
    }
}
