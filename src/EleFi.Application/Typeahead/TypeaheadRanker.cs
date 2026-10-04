namespace EleFi.Application.Typeahead;

/// <summary>Something that can be offered while the user types a name.</summary>
/// <param name="Name">The name as it should be shown and filled in.</param>
/// <param name="UsageCount">How many times it has been used.</param>
/// <param name="LastUsedAt">When it was last used, or null if never.</param>
public readonly record struct TypeaheadCandidate(string Name, int UsageCount, DateTimeOffset? LastUsedAt);

/// <summary>
/// Ranks names for a type-to-find field such as "Paid to".
/// </summary>
/// <remarks>
/// <para>
/// With nothing typed, it answers "who do I usually pay?": most used first, recency breaking
/// ties. Once something is typed it answers "which one did I mean?": how closely the name
/// matches decides first, and usage only orders names that match equally well. Without that
/// split, a merchant used forty times would outrank the exact name being typed.
/// </para>
/// <para>
/// Pure and in memory on purpose. The whole history is a few hundred names, so ranking it
/// on every keystroke costs microseconds, where a database round trip per keystroke would
/// make the field lag behind the keyboard.
/// </para>
/// </remarks>
public static class TypeaheadRanker
{
    /// <summary>Queries shorter than this never fall back to letters-in-order matching.</summary>
    private const int MinimumScatteredQuery = 3;

    private static readonly char[] WordBreaks = [' ', '-', '.', '_', '@', '/', '&', '(', ')'];

    private enum Closeness
    {
        Exact = 0,
        Prefix = 1,
        WordPrefix = 2,
        Contains = 3,
        Scattered = 4,
        None = 5,
    }

    /// <summary>The best names for what has been typed so far.</summary>
    /// <param name="candidates">Everything that could be offered.</param>
    /// <param name="query">What has been typed, or null or blank for nothing yet.</param>
    /// <param name="take">The most names to return.</param>
    /// <returns>Names, best first. Never more than <paramref name="take"/>.</returns>
    public static IReadOnlyList<string> Rank(IEnumerable<TypeaheadCandidate> candidates, string? query, int take)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (take <= 0)
        {
            return [];
        }

        var term = (query ?? string.Empty).Trim();

        // One entry per name regardless of case, keeping the one with the most history.
        var distinct = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.UsageCount).ThenByDescending(c => c.LastUsedAt).First());

        if (term.Length == 0)
        {
            return distinct
                .OrderByDescending(c => c.UsageCount)
                .ThenByDescending(c => c.LastUsedAt ?? DateTimeOffset.MinValue)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Take(take)
                .Select(c => c.Name.Trim())
                .ToList();
        }

        return distinct
            .Select(c => (Candidate: c, Closeness: Measure(c.Name.Trim(), term)))
            .Where(x => x.Closeness != Closeness.None)
            .OrderBy(x => x.Closeness)
            .ThenByDescending(x => x.Candidate.UsageCount)
            .ThenByDescending(x => x.Candidate.LastUsedAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.Candidate.Name.Length)
            .ThenBy(x => x.Candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(x => x.Candidate.Name.Trim())
            .ToList();
    }

    private static Closeness Measure(string name, string term)
    {
        if (name.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            return Closeness.Exact;
        }

        if (name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return Closeness.Prefix;
        }

        var words = name.Split(WordBreaks, StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(term, StringComparison.OrdinalIgnoreCase)))
        {
            return Closeness.WordPrefix;
        }

        if (name.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return Closeness.Contains;
        }

        return term.Length >= MinimumScatteredQuery && IsScatteredMatch(name, term)
            ? Closeness.Scattered
            : Closeness.None;
    }

    // Every letter of the term appears in the name, in order, not necessarily together.
    private static bool IsScatteredMatch(string name, string term)
    {
        var next = 0;
        foreach (var ch in name)
        {
            if (char.ToUpperInvariant(ch) == char.ToUpperInvariant(term[next]) && ++next == term.Length)
            {
                return true;
            }
        }

        return false;
    }
}
