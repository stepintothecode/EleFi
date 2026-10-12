using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;

namespace EleFi.Domain.Tests.Alerts;

/// <summary>
/// SM18: with no last four digits to go on, the account a message names, or the bank in its
/// sender, picks the container, but only when exactly one fits.
/// </summary>
public class AccountNameMatchTests
{
    private static readonly Container Kotak = new() { Name = "Kotak Savings", Kind = ContainerKind.BankAccount };
    private static readonly Container Sbi = new() { Name = "SBI", Kind = ContainerKind.BankAccount, InstitutionName = "State Bank of India" };
    private static readonly Container HdfcCard = new() { Name = "Regalia", Kind = ContainerKind.CreditCard, InstitutionName = "HDFC Bank" };
    private static readonly Container HdfcSavings = new() { Name = "HDFC Savings", Kind = ContainerKind.BankAccount };
    private static readonly Container Cash = new() { Name = "Cash", Kind = ContainerKind.Cash };

    private static readonly Container[] Mine = [Kotak, Sbi, HdfcCard, Cash];

    [Fact]
    public void The_account_a_message_names_is_found_by_its_name()
    {
        Assert.Same(Kotak, AccountNameMatch.Find(Mine, "Kotak Bank", null));
    }

    [Fact]
    public void The_bank_a_container_is_with_counts_as_its_name()
    {
        Assert.Same(HdfcCard, AccountNameMatch.Find(Mine, "HDFC Bank Credit Card", null));
    }

    [Theory]
    [InlineData("your bank account")]
    [InlineData("A/c")]
    [InlineData("Credit Card")]
    public void Words_every_bank_uses_match_nothing(string wording)
    {
        Assert.Null(AccountNameMatch.Find(Mine, wording, null));
    }

    [Theory]
    [InlineData("VM-SBIINB")]
    [InlineData("AD-KOTAKB")]
    public void Without_a_name_the_bank_in_the_sender_picks_the_only_account_there(string sender)
    {
        var expected = sender.Contains("SBI", StringComparison.Ordinal) ? Sbi : Kotak;

        Assert.Same(expected, AccountNameMatch.Find(Mine, null, sender));
    }

    [Fact]
    public void Two_accounts_at_the_same_bank_are_a_question_not_an_answer()
    {
        Assert.Null(AccountNameMatch.Find([HdfcSavings, HdfcCard, Sbi], null, "VM-HDFCBK"));
    }

    [Fact]
    public void The_sender_narrows_a_name_that_fits_more_than_one()
    {
        var kotakCard = new Container { Name = "Kotak Card", Kind = ContainerKind.CreditCard, InstitutionName = "Kotak" };

        // "Kotak" alone fits both, and the sender says the same bank: still two, so no answer.
        Assert.Null(AccountNameMatch.Find([Kotak, kotakCard], "Kotak", "VM-KOTAKB"));

        // Naming the savings account settles it.
        Assert.Same(Kotak, AccountNameMatch.Find([Kotak, kotakCard], "Kotak Savings", "VM-KOTAKB"));
    }

    [Fact]
    public void A_payment_app_package_is_not_a_bank()
    {
        Assert.Null(AccountNameMatch.Find(Mine, null, "com.google.android.apps.nbu.paisa.user"));
    }
}
