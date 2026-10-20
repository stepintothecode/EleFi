using EleFi.Domain.Apps;
using EleFi.Domain.Parties;

namespace EleFi.Application.Typeahead;

/// <summary>Turns the shared pools into things a type-to-find field can offer.</summary>
public static class TypeaheadCandidates
{
    /// <summary>External parties, with their usage. Containers and nameless rows are skipped.</summary>
    /// <param name="parties">The parties.</param>
    public static IReadOnlyList<TypeaheadCandidate> From(IEnumerable<Party> parties)
    {
        ArgumentNullException.ThrowIfNull(parties);

        return parties
            .Where(p => p.Kind == PartyKind.External && !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new TypeaheadCandidate(p.Name!, p.UsageCount, p.LastUsedAt))
            .ToList();
    }

    /// <summary>How far apart the tiers of <see cref="InTiers"/> are, in uses.</summary>
    public const int TierStep = 1_000_000;

    /// <summary>
    /// Merges lists in priority order: everything in the first list ranks above everything in
    /// the second, and so on, while within a list usage still decides. A name in several lists
    /// keeps its place in the earliest.
    /// </summary>
    /// <remarks>
    /// The ranker orders by usage, so the priority is carried as usage: each earlier tier is
    /// lifted a whole <see cref="TierStep"/> above the next. While typing, how closely a name
    /// matches still comes first, and the tiers only order names that match equally well.
    /// </remarks>
    /// <param name="tiers">The lists, most preferred first.</param>
    public static IReadOnlyList<TypeaheadCandidate> InTiers(params IEnumerable<TypeaheadCandidate>[] tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<TypeaheadCandidate>();
        for (var i = 0; i < tiers.Length; i++)
        {
            var lift = (tiers.Length - 1 - i) * TierStep;
            foreach (var candidate in tiers[i] ?? [])
            {
                if (string.IsNullOrWhiteSpace(candidate.Name) || !seen.Add(candidate.Name.Trim()))
                {
                    continue;
                }

                merged.Add(candidate with { UsageCount = candidate.UsageCount + lift });
            }
        }

        return merged;
    }

    /// <summary>Apps, with their usage.</summary>
    /// <param name="apps">The apps.</param>
    public static IReadOnlyList<TypeaheadCandidate> From(IEnumerable<App> apps)
    {
        ArgumentNullException.ThrowIfNull(apps);

        return apps
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => new TypeaheadCandidate(a.Name, a.UsageCount, a.LastUsedAt))
            .ToList();
    }
}
