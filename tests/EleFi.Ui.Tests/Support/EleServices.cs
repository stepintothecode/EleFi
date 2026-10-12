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
        return services;
    }
}
