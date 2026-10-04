using System.Globalization;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Filters;

/// <summary>
/// A composable set of criteria selecting a subset of transactions.
/// </summary>
/// <remarks>
/// <para>
/// <b>One filter serves both the list and the export.</b> The export is the filtered view
/// serialised (X1), so there is no second notion of "what to export" that could drift from
/// what is on screen.
/// </para>
/// <para>
/// Dimensions compose with AND. Multi-select within one dimension is OR: two containers
/// means either container, but a container and a label means both must hold.
/// </para>
/// <para>
/// The empty filter means everything (X2), so an unfiltered export contains every
/// transaction the user could reach by scrolling.
/// </para>
/// </remarks>
public sealed record TransactionFilter
{
    /// <summary>The filter that excludes nothing.</summary>
    public static TransactionFilter Empty { get; } = new();

    /// <summary>Earliest date to include, inclusive.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Latest date to include, inclusive.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Kinds to include. Empty means all.</summary>
    public IReadOnlyList<TransactionKind> Kinds { get; init; } = [];

    /// <summary>
    /// Containers to include, matching <b>either end</b> of the transaction.
    /// </summary>
    /// <remarks>
    /// Matching either end is what makes this an account statement rather than a list of
    /// spending: money that arrived in the account belongs in its statement just as much
    /// as money that left.
    /// </remarks>
    public IReadOnlyList<Guid> ContainerIds { get; init; } = [];

    /// <summary>
    /// Labels to include. A transaction matches when it carries <b>any</b> of them.
    /// </summary>
    /// <remarks>
    /// Any rather than all, because a transaction now carries several labels and the
    /// question people ask is "show me anything to do with Food", not "show me things that
    /// are simultaneously Food and Household".
    /// </remarks>
    public IReadOnlyList<Guid> LabelIds { get; init; } = [];

    /// <summary>When true, include only transactions carrying no labels at all.</summary>
    /// <remarks>
    /// Labels are optional, so unlabelled is a real and common state. Without this the only
    /// way to find those transactions is to notice their absence from every label filter.
    /// </remarks>
    public bool UnlabelledOnly { get; init; }

    /// <summary>Parties to include, at either end.</summary>
    public IReadOnlyList<Guid> PartyIds { get; init; } = [];

    /// <summary>Apps to include, in either role.</summary>
    public IReadOnlyList<Guid> AppIds { get; init; } = [];

    /// <summary>Smallest amount to include, in minor units.</summary>
    public long? MinAmountMinor { get; init; }

    /// <summary>Largest amount to include, in minor units.</summary>
    public long? MaxAmountMinor { get; init; }

    /// <summary>Currency code to restrict to.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>When set, include only transactions matching this review state.</summary>
    public bool? NeedsReview { get; init; }

    /// <summary>Free text matched against description, party, app, and tag names.</summary>
    public string? Search { get; init; }

    /// <summary>True when nothing is restricted and this selects every transaction.</summary>
    public bool IsEmpty =>
        From is null && To is null
        && Kinds.Count == 0 && ContainerIds.Count == 0 && LabelIds.Count == 0
        && !UnlabelledOnly && PartyIds.Count == 0 && AppIds.Count == 0
        && MinAmountMinor is null && MaxAmountMinor is null
        && CurrencyCode is null && NeedsReview is null
        && string.IsNullOrWhiteSpace(Search);

    /// <summary>
    /// A short slug describing the filter, for the export filename (FR-7.11, FR-7.22).
    /// </summary>
    /// <remarks>
    /// Reads "all" when nothing is applied, so a file called <c>elefi-all-2026-08-30.csv</c>
    /// is unambiguous about containing everything.
    /// </remarks>
    /// <param name="containerNames">Names of the selected containers, for a readable slug.</param>
    public string ToFilenameSlug(IReadOnlyList<string>? containerNames = null)
    {
        if (IsEmpty)
        {
            return "all";
        }

        var parts = new List<string>();

        if (containerNames is { Count: > 0 })
        {
            parts.AddRange(containerNames.Take(2).Select(Slug));
        }

        if (Kinds.Count == 1)
        {
            parts.Add(Slug(Kinds[0].ToString()));
        }

        if (NeedsReview == true)
        {
            parts.Add("needs-review");
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            parts.Add(Slug(Search));
        }

        if (From is { } from)
        {
            parts.Add(from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (To is { } to)
        {
            parts.Add(to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "filtered" : string.Join("-", parts);
    }

    private static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')
            .ToArray();

        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
