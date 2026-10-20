using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Ui.Components;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components;

/// <summary>A page reloading when the app comes back from the background.</summary>
public class RefreshOnResumeTests : Bunit.TestContext
{
    private readonly Fakes.ManualResume _resume = new();

    public RefreshOnResumeTests() => Services.AddSingleton<IAppResume>(_resume);

    [Fact]
    public void Coming_back_to_the_app_reloads_the_page()
    {
        var reloads = 0;
        RenderComponent<RefreshOnResume>(p => p.Add(r => r.OnResumed, () => reloads++));

        _resume.Resume();
        _resume.Resume();

        Assert.Equal(2, reloads);
    }

    [Fact]
    public void A_page_that_has_closed_is_not_reloaded()
    {
        var reloads = 0;
        var component = RenderComponent<RefreshOnResume>(p => p.Add(r => r.OnResumed, () => reloads++));

        DisposeComponents();
        _resume.Resume();

        Assert.Equal(0, reloads);
        Assert.NotNull(component);
    }
}
