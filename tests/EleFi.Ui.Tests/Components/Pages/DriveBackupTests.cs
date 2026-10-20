using Bunit;
using EleFi.Ui.Components.Pages;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>Google Drive backup, before it is built: the familiar shape, and honest about it.</summary>
public class DriveBackupTests : Bunit.TestContext
{
    [Fact]
    public void It_says_it_is_coming_and_nothing_on_it_can_be_used_yet()
    {
        var page = RenderComponent<DriveBackup>();

        Assert.Contains("Coming soon", page.Find(".drive-soon").TextContent, StringComparison.Ordinal);
        Assert.True(page.Find("button.drive-back-up").HasAttribute("disabled"));
        Assert.True(page.Find("input[aria-label='Back up using mobile data']").HasAttribute("disabled"));
        Assert.All(page.FindAll(".coming-soon"), card => Assert.Equal("true", card.GetAttribute("aria-disabled")));
    }

    [Fact]
    public void It_is_laid_out_like_a_familiar_backup_screen()
    {
        var page = RenderComponent<DriveBackup>();

        Assert.Equal("Last backup", page.Find(".drive-heading").TextContent);
        Assert.Equal(
            ["Google Account", "Back up to Google Drive", "Back up using mobile data", "Encrypted backup"],
            page.FindAll(".drive-row .settings-title").Select(t => t.TextContent));
    }
}
