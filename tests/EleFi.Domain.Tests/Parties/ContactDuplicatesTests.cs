using EleFi.Domain.Parties;

namespace EleFi.Domain.Tests.Parties;

/// <summary>
/// Possible duplicates among contacts: the same name written differently, one name inside
/// another, or one letter out.
/// </summary>
public class ContactDuplicatesTests
{
    [Theory]
    [InlineData("Zomato", "ZOMATO LTD")]
    [InlineData("Big Bazaar", "BigBazaar")]
    [InlineData("Zomato", "zomato@hdfcbank")]
    [InlineData("Zomato", "VPA zomato@hdfcbank")]
    [InlineData("Swiggy", "Swiggy Pvt. Ltd.")]
    public void The_same_name_written_differently_is_found(string a, string b)
    {
        var pair = Assert.Single(Find(a, b));

        Assert.Equal(DuplicateReason.SameName, pair.Reason);
    }

    [Theory]
    [InlineData("Rahul", "Rahul Kumar")]
    [InlineData("Amazon", "Amazon Pay")]
    public void One_name_inside_another_is_offered_as_possible(string a, string b)
    {
        Assert.Equal(DuplicateReason.OneInsideTheOther, Assert.Single(Find(a, b)).Reason);
    }

    [Theory]
    [InlineData("Swiggy", "Swigy")]
    [InlineData("Landlord", "Landlrod")]
    public void One_letter_out_is_offered_as_a_likely_typo(string a, string b)
    {
        Assert.Equal(DuplicateReason.SimilarSpelling, Assert.Single(Find(a, b)).Reason);
    }

    [Theory]
    [InlineData("Rahul", "Priya")]
    [InlineData("Al", "Ali")]
    [InlineData("Cab", "Cafe")]
    public void Different_names_are_left_alone(string a, string b)
    {
        Assert.Empty(Find(a, b));
    }

    [Fact]
    public void A_pair_already_linked_or_marked_not_the_same_is_not_suggested_again()
    {
        var zomato = (Guid.CreateVersion7(), "Zomato");
        var ltd = (Guid.CreateVersion7(), "Zomato Ltd");

        var found = ContactDuplicates.Find([zomato, ltd], [(zomato.Item1, ltd.Item1)]);

        Assert.Empty(found);
    }

    [Fact]
    public void Each_pair_once_and_the_surest_first()
    {
        var found = ContactDuplicates.Find(
        [
            (Guid.CreateVersion7(), "Rahul"),
            (Guid.CreateVersion7(), "Rahul Kumar"),
            (Guid.CreateVersion7(), "Swiggy"),
            (Guid.CreateVersion7(), "SWIGGY"),
        ], []);

        Assert.Equal(2, found.Count);
        Assert.Equal(DuplicateReason.SameName, found[0].Reason);
        Assert.Equal(DuplicateReason.OneInsideTheOther, found[1].Reason);
    }

    private static IReadOnlyList<DuplicatePair> Find(string a, string b) =>
        ContactDuplicates.Find([(Guid.CreateVersion7(), a), (Guid.CreateVersion7(), b)], []);
}
