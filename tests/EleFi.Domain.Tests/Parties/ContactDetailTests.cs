using EleFi.Domain.Parties;

namespace EleFi.Domain.Tests.Parties;

/// <summary>CT2: a contact's phone numbers, emails and addresses are checked before they are kept.</summary>
public class ContactDetailTests
{
    private static readonly Guid Rahul = Guid.CreateVersion7();

    [Theory]
    [InlineData("+91 98765 43210", "+91 98765 43210")]
    [InlineData("  98765   43210 ", "98765 43210")]
    [InlineData("(022) 2345-6789", "(022) 2345-6789")]
    public void A_phone_number_is_kept_tidied(string typed, string kept)
    {
        var detail = Create(ContactDetailKind.Phone, typed);

        Assert.Equal(kept, detail.Value);
    }

    [Theory]
    [InlineData("call me")]
    [InlineData("12345")]
    [InlineData("+91 98765 43210 98765 43210")]
    public void Something_that_is_not_a_phone_number_is_refused(string typed)
    {
        Assert.NotNull(ContactDetail.Problem(ContactDetailKind.Phone, typed));
    }

    [Theory]
    [InlineData("rahul@example.com")]
    [InlineData("Rahul.K+bills@mail.co.in")]
    public void An_email_address_is_accepted(string typed)
    {
        Assert.Null(ContactDetail.Problem(ContactDetailKind.Email, typed));
    }

    [Theory]
    [InlineData("rahul")]
    [InlineData("rahul@")]
    [InlineData("@example.com")]
    [InlineData("rahul @example.com")]
    public void Something_that_is_not_an_email_address_is_refused(string typed)
    {
        Assert.NotNull(ContactDetail.Problem(ContactDetailKind.Email, typed));
    }

    [Fact]
    public void An_address_keeps_its_lines()
    {
        var detail = Create(ContactDetailKind.Address, "12 MG Road\nBengaluru 560001");

        Assert.Equal("12 MG Road\nBengaluru 560001", detail.Value);
    }

    [Theory]
    [InlineData(ContactDetailKind.Address, "")]
    [InlineData(ContactDetailKind.Other, "   ")]
    public void An_empty_detail_is_refused(ContactDetailKind kind, string typed)
    {
        Assert.NotNull(ContactDetail.Problem(kind, typed));
    }

    [Fact]
    public void A_label_is_optional_and_trimmed()
    {
        var detail = ContactDetail.Create(Rahul, ContactDetailKind.Phone, "98765 43210", "  Work ", DateTimeOffset.UnixEpoch);

        Assert.Equal("Work", detail.Label);
        Assert.Null(ContactDetail.Create(Rahul, ContactDetailKind.Phone, "98765 43210", "  ", DateTimeOffset.UnixEpoch).Label);
    }

    [Fact]
    public void Creating_a_detail_that_fails_its_check_throws_rather_than_storing_it()
    {
        Assert.Throws<ArgumentException>(() =>
            ContactDetail.Create(Rahul, ContactDetailKind.Email, "nope", null, DateTimeOffset.UnixEpoch));
    }

    private static ContactDetail Create(ContactDetailKind kind, string value) =>
        ContactDetail.Create(Rahul, kind, value, null, DateTimeOffset.UnixEpoch);
}
