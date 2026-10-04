using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Application.Backup;
using EleFi.Application.Labels;
using EleFi.Application.Suggestions;
using EleFi.Ui.Components.Pages;
using EleFi.Ui.Services;
using EleFi.Ui.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>
/// Settings: the label list, the local backup cards, and the About links.
/// </summary>
public class SettingsTests : Bunit.TestContext
{
    [Fact]
    public void Creating_and_renaming_a_label_happen_in_a_sheet_not_inline()
    {
        Register();

        var component = RenderComponent<Settings>();

        // Nothing is open to begin with. The form used to be appended below the list, so on
        // a long list you scrolled past every label to reach it.
        Assert.Empty(component.FindAll("[role=dialog]"));

        component.Find(".section-head button").Click();

        var dialog = component.Find("[role=dialog]");
        Assert.Equal("New label", dialog.GetAttribute("aria-label"));
    }

    [Fact]
    public void Deleting_a_label_asks_first_and_says_what_it_will_touch()
    {
        Register();

        var component = RenderComponent<Settings>();
        component.Find("button[aria-label='Delete Food']").Click();

        var dialog = component.Find("[role=dialog]");

        Assert.Equal("Delete this label?", dialog.GetAttribute("aria-label"));

        // The transactions themselves are untouched, which is the fact that decides whether
        // the user goes ahead.
        Assert.Contains("stay exactly as they are", dialog.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_export_card_says_the_file_is_not_encrypted()
    {
        Register();

        var component = RenderComponent<Settings>();

        // FR-10.15. A plaintext financial history is fine as long as the user knows that is
        // what they are about to put in their Downloads folder.
        Assert.Contains("not encrypted", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_restore_card_says_it_replaces_rather_than_merges()
    {
        Register();

        var component = RenderComponent<Settings>();

        Assert.Contains("Replaces everything", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_about_link_opens_outside_the_app()
    {
        var links = new RecordingLinks();
        Register(links);

        var component = RenderComponent<Settings>();

        foreach (var button in component.FindAll(".about-link"))
        {
            button.Click();
        }

        await Task.Yield();

        // FR-12.4: never an <a href>. Under Blazor Hybrid that navigates the host WebView and
        // strands the user inside the app with no chrome and no way back.
        Assert.Equal(3, links.Opened.Count);
        Assert.All(links.Opened, url => Assert.StartsWith("https://", url, StringComparison.Ordinal));
        Assert.Contains(links.Opened, u => u.Contains("support", StringComparison.Ordinal));
        Assert.Contains(links.Opened, u => u.Contains("github.com", StringComparison.Ordinal));
        Assert.Contains(links.Opened, u => u.Contains("youtube.com", StringComparison.Ordinal));
    }

    [Fact]
    public void The_support_link_promises_nothing_in_return()
    {
        Register();

        var component = RenderComponent<Settings>();

        // ADR-0012. The moment a tip buys anything, the app has a paid tier.
        Assert.Contains("buys nothing", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void NFR_10_7_with_no_way_to_read_alerts_there_are_no_switches_for_it()
    {
        Register(access: new FakeAccess { IsSupported = false });

        var component = RenderComponent<Settings>();

        Assert.Empty(component.FindAll("input[aria-label='Read bank SMS']"));
        Assert.Contains("Paste a bank message", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_capture_switches_start_off()
    {
        Register();

        var component = RenderComponent<Settings>();

        Assert.False(component.Find("input[aria-label='Read bank SMS']").HasAttribute("checked"));
        Assert.False(component.Find("input[aria-label='Read payment app notifications']").HasAttribute("checked"));
    }

    [Fact]
    public void SM10_the_sms_permission_is_asked_for_only_when_the_switch_is_turned_on()
    {
        var access = new FakeAccess { GrantSms = true };
        Register(access: access);

        var component = RenderComponent<Settings>();
        Assert.Equal(0, access.SmsRequests);

        component.Find("input[aria-label='Read bank SMS']").Change(true);

        Assert.Equal(1, access.SmsRequests);
        Assert.Equal("true", _store.Read(AlertCaptureSettings.SmsKey, "false"));
    }

    [Fact]
    public void A_refused_sms_permission_leaves_the_switch_off()
    {
        Register(access: new FakeAccess { GrantSms = false });

        var component = RenderComponent<Settings>();
        component.Find("input[aria-label='Read bank SMS']").Change(true);

        Assert.Equal("false", _store.Read(AlertCaptureSettings.SmsKey, "false"));
        Assert.False(component.Find("input[aria-label='Read bank SMS']").HasAttribute("checked"));
    }

    [Fact]
    public void Turning_on_payment_apps_opens_the_system_screen_where_access_is_granted()
    {
        var access = new FakeAccess();
        Register(access: access);

        var component = RenderComponent<Settings>();
        component.Find("input[aria-label='Read payment app notifications']").Change(true);

        // Android has no runtime prompt for notification access, only its own settings list.
        Assert.Equal(1, access.SettingsOpened);
        Assert.Contains("Allow notification access", component.Markup, StringComparison.Ordinal);
    }

    private readonly MemorySettings _store = new();

    private void Register(ILinkOpener? links = null, IAlertAccess? access = null)
    {
        var clock = new Fakes.StoppedClock();
        var labels = new Fakes.SeededLabels();

        Services.AddSingleton(new AlertCaptureSettings(_store));
        Services.AddSingleton(access ?? new FakeAccess());
        Services.AddSingleton<IClock>(clock);
        Services.AddSingleton(new LabelService(labels, clock));
        Services.AddSingleton<IDataWipe>(new NoWipe());
        Services.AddSingleton<ILocalBackup>(new NoBackup());
        Services.AddSingleton<IFileShare>(new NoShare());
        Services.AddSingleton<IFilePick>(new NoPick());
        Services.AddSingleton(links ?? new RecordingLinks());
        Services.AddSingleton<IMascotService>(new Fakes.SilentMascot());
        Services.AddSingleton(new ToastService());
    }

    private sealed class RecordingLinks : ILinkOpener
    {
        public List<string> Opened { get; } = [];

        public Task OpenAsync(string url, CancellationToken cancellationToken = default)
        {
            Opened.Add(url);
            return Task.CompletedTask;
        }
    }

    private sealed class NoWipe : IDataWipe
    {
        public Task WipeEverythingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoBackup : ILocalBackup
    {
        public Task<BackupFile> ExportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackupFile());

        public Task<int> RestoreAsync(BackupFile file, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class NoShare : IFileShare
    {
        public Task<string> ShareAsync(
            string fileName,
            ReadOnlyMemory<byte> contents,
            string title,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(fileName);
    }

    private sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string Read(string key, string fallback) => _values.GetValueOrDefault(key, fallback);

        public void Write(string key, string value) => _values[key] = value;
    }

    private sealed class FakeAccess : IAlertAccess
    {
        public bool IsSupported { get; init; } = true;

        public bool GrantSms { get; init; }

        public bool CanReadSms { get; private set; }

        public bool CanReadNotifications => false;

        public int SmsRequests { get; private set; }

        public int SettingsOpened { get; private set; }

        public Task<bool> RequestSmsAsync()
        {
            SmsRequests++;
            CanReadSms = GrantSms;
            return Task.FromResult(GrantSms);
        }

        public Task<bool> RequestPromptsAsync() => Task.FromResult(true);

        public void OpenNotificationAccessSettings() => SettingsOpened++;
    }

    private sealed class NoPick : IFilePick
    {
        public Task<string?> PickTextFileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
