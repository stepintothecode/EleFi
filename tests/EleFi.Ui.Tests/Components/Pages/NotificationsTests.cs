using Bunit;
using EleFi.Application.Abstractions;
using EleFi.Domain.Notices;
using EleFi.Ui.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EleFi.Ui.Tests.Components.Pages;

/// <summary>Everything EleFi has told the user, in full, read and unread.</summary>
public class NotificationsTests : Bunit.TestContext
{
    private readonly List<Notice> _rows = [];

    public NotificationsTests()
    {
        var notices = Substitute.For<INoticeRepository>();
        notices.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => _rows.ToList());
        notices.When(n => n.SetReadAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(call => _rows.Find(r => r.Id == call.ArgAt<Guid>(0))!.ReadAt = call.ArgAt<bool>(1) ? DateTimeOffset.UnixEpoch : null);
        notices.When(n => n.SetAllReadAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(call => _rows.ForEach(r => r.ReadAt = call.ArgAt<bool>(0) ? DateTimeOffset.UnixEpoch : null));
        Services.AddSingleton(notices);
    }

    [Fact]
    public void Each_notification_is_shown_in_full()
    {
        _rows.Add(Make("₹450 to Zomato", "From HDFC Card via GPay. Plan \"Lunch\" done. Recorded, needs review. Tap to check it."));

        var page = RenderComponent<Notifications>();

        Assert.Contains("Plan \"Lunch\" done. Recorded, needs review", page.Find(".notice-full").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Tapping_one_toggles_it_read_and_unread()
    {
        _rows.Add(Make("₹450 to Zomato", "Body"));
        var page = RenderComponent<Notifications>();

        page.Find(".notice-body").Click();
        Assert.True(_rows[0].IsRead);
        Assert.Contains("read", page.Find(".notice-row").ClassName, StringComparison.Ordinal);

        page.Find(".notice-body").Click();
        Assert.False(_rows[0].IsRead);
    }

    [Fact]
    public void Mark_all_read_then_becomes_mark_all_unread()
    {
        _rows.Add(Make("One", "Body"));
        _rows.Add(Make("Two", "Body"));
        var page = RenderComponent<Notifications>();

        page.Find(".section-head button").Click();

        Assert.All(_rows, r => Assert.True(r.IsRead));
        Assert.Equal("Mark all unread", page.Find(".section-head button").TextContent);

        page.Find(".section-head button").Click();
        Assert.All(_rows, r => Assert.False(r.IsRead));
    }

    [Fact]
    public void Open_goes_to_what_it_is_about_and_marks_it_read()
    {
        var notice = Make("₹450 to Zomato", "Body", "transactions/abc");
        _rows.Add(notice);
        var page = RenderComponent<Notifications>();

        page.Find(".notice-row .btn-icon").Click();

        Assert.True(notice.IsRead);
        Assert.EndsWith("/transactions/abc", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void With_nothing_to_show_it_says_so()
    {
        var page = RenderComponent<Notifications>();

        Assert.Contains("All quiet", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".section-head button"));
    }

    private static Notice Make(string title, string body, string? route = null) =>
        new() { Title = title, Body = body, Route = route, CreatedAt = DateTimeOffset.UnixEpoch };
}
