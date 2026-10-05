using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;

namespace EleFi.Ui.Tests.Services;

/// <summary>The blank card in the recent-apps view.</summary>
public class AppSwitcherPrivacyTests
{
    [Fact]
    public void It_is_on_until_turned_off_and_remembers_the_choice()
    {
        var store = new Fakes.MemorySettings();
        var privacy = new AppSwitcherPrivacy(store);

        Assert.True(privacy.Enabled);

        privacy.Set(false);

        Assert.False(new AppSwitcherPrivacy(store).Enabled);
        Assert.False(AppSwitcherPrivacy.IsEnabled(store));
    }
}
