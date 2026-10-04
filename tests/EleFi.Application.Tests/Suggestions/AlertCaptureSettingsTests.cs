using EleFi.Application.Abstractions;
using EleFi.Application.Suggestions;
using EleFi.Domain.Alerts;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>The in-app switches for automatic capture.</summary>
public class AlertCaptureSettingsTests
{
    [Fact]
    public void Both_switches_start_off()
    {
        // Reading messages has to be an offer the user takes up, not a default.
        var settings = new AlertCaptureSettings(new MemorySettings());

        Assert.False(settings.IsEnabled(AlertChannel.Sms));
        Assert.False(settings.IsEnabled(AlertChannel.PaymentApp));
    }

    [Fact]
    public void Each_switch_is_remembered_independently()
    {
        var store = new MemorySettings();
        new AlertCaptureSettings(store).PaymentAppsEnabled = true;

        var reread = new AlertCaptureSettings(store);

        Assert.True(reread.IsEnabled(AlertChannel.PaymentApp));
        Assert.False(reread.IsEnabled(AlertChannel.Sms));
    }

    private sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string Read(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

        public void Write(string key, string value) => _values[key] = value;
    }
}
