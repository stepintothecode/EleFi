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
