using EleFi.Application.Abstractions;
using EleFi.Ui.Services;

namespace EleFi.Ui.Tests.Services;

public class PrivacyModeTests
{
    private readonly MemorySettings _store = new();

    [Fact]
    public void Amounts_are_shown_until_the_user_hides_them()
    {
        var privacy = new PrivacyMode(_store);

        Assert.False(privacy.AmountsHidden);
        Assert.Equal(string.Empty, privacy.CssClass);
    }

    [Fact]
    public void Toggling_hides_raises_changed_and_is_remembered_next_launch()
    {
        var privacy = new PrivacyMode(_store);
        var changes = 0;
        privacy.Changed += () => changes++;

        privacy.Toggle();

        Assert.True(privacy.AmountsHidden);
        Assert.Equal("amounts-hidden", privacy.CssClass);
        Assert.Equal(1, changes);
        Assert.True(new PrivacyMode(_store).AmountsHidden);
    }

    [Fact]
    public void Setting_the_state_it_is_already_in_changes_nothing()
    {
        var privacy = new PrivacyMode(_store);
        var changes = 0;
        privacy.Changed += () => changes++;

        privacy.Set(false);

        Assert.Equal(0, changes);
    }

    private sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string Read(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

        public void Write(string key, string value) => _values[key] = value;
    }
}
