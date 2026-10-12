using System.Text.RegularExpressions;

namespace EleFi.Domain.Parties;

/// <summary>Why two contacts look like the same one.</summary>
public enum DuplicateReason
{
    /// <summary>The same name, written differently: case, spacing, "Ltd", a UPI handle.</summary>
    SameName = 0,

    /// <summary>A name one letter or two off the other: a likely typo.</summary>
    SimilarSpelling = 1,

    /// <summary>Every word of one name is in the other: "Rahul" and "Rahul Kumar". Possibly different people.</summary>
    OneInsideTheOther = 2,
}

/// <summary>Two contacts that might be one.</summary>
/// <param name="First">One of them.</param>
/// <param name="Second">The other.</param>
/// <param name="Reason">Why they look alike.</param>
public sealed record DuplicatePair(Guid First, Guid Second, DuplicateReason Reason);

/// <summary>
/// Finds contacts that are probably the same person or business, for the user to merge,
/// link, or mark as not the same.
/// </summary>
/// <remarks>
/// <para>
/// Duplicates arrive mostly from alerts: a bank writes "ZOMATO LTD", a payment app writes
/// "Zomato", a UPI alert writes "zomato@hdfcbank", and P4's case-insensitive match keeps
/// those three apart. So names are compared after taking off what varies between those
/// sources: case, punctuation and spacing, a UPI handle's bank, and company suffixes.
/// </para>
/// <para>
/// Only ever a suggestion. Nothing is merged without the user choosing it, and a pair they
/// linked or marked "not the same" is not suggested again.
/// </para>
/// </remarks>
public static class ContactDuplicates
{
    // What a business's name carries in one source and not another.
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "vpa", "upi", "pvt", "private", "ltd", "limited", "llp", "inc", "co", "the", "m/s", "ms",
    };

    private static readonly Regex Word = new(@"[a-z0-9]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    /// <summary>Every likely duplicate pair, surest first.</summary>
    /// <param name="contacts">Every live contact, by id and name.</param>
    /// <param name="settled">Pairs the user has already linked or marked not the same, either way round.</param>
    public static IReadOnlyList<DuplicatePair> Find(
        IReadOnlyList<(Guid Id, string Name)> contacts,
        IReadOnlyCollection<(Guid, Guid)> settled)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(settled);

        var known = new HashSet<(Guid, Guid)>(settled.SelectMany(p => new[] { p, (p.Item2, p.Item1) }));
        var words = contacts.Select(c => (c.Id, c.Name, Words: Words(c.Name))).Where(c => c.Words.Count > 0).ToList();
        var found = new List<(DuplicatePair Pair, string Order)>();

        for (var i = 0; i < words.Count; i++)
        {
            for (var j = i + 1; j < words.Count; j++)
            {
                var (a, b) = (words[i], words[j]);
                if (a.Id == b.Id || known.Contains((a.Id, b.Id)))
                {
                    continue;
                }

                if (ReasonFor(a.Words, b.Words) is { } reason)
                {
                    found.Add((new DuplicatePair(a.Id, b.Id, reason), a.Name + "\u0000" + b.Name));
                }
            }
        }

        return [.. found
            .OrderBy(f => f.Pair.Reason)
            .ThenBy(f => f.Order, StringComparer.OrdinalIgnoreCase)
            .Select(f => f.Pair)];
    }

    private static DuplicateReason? ReasonFor(List<string> a, List<string> b)
    {
        var (joinedA, joinedB) = (string.Concat(a), string.Concat(b));

        if (string.Equals(joinedA, joinedB, StringComparison.Ordinal))
        {
            return DuplicateReason.SameName;
        }

        var shorter = Math.Min(joinedA.Length, joinedB.Length);
        var allowed = shorter >= 9 ? 2 : shorter >= 5 ? 1 : 0;
        if (allowed > 0 && Distance(joinedA, joinedB, allowed) <= allowed)
        {
            return DuplicateReason.SimilarSpelling;
        }

        // Every word of the shorter name appears in the longer, and it is a real word rather
        // than an initial, so "Rahul" finds "Rahul Kumar" and "A" finds nothing.
        var (few, many) = a.Count <= b.Count ? (a, b) : (b, a);
        return few.Count < many.Count && few.Sum(w => w.Length) >= 3 && few.All(many.Contains)
            ? DuplicateReason.OneInsideTheOther
            : null;
    }

    // Lower-case words with the noise taken out. A UPI handle keeps only its name part:
    // "zomato@hdfcbank" is Zomato at HDFC's UPI, not a company called Zomato HDFC Bank.
    private static List<string> Words(string name)
    {
        var lower = name.ToLowerInvariant();
        var at = lower.IndexOf('@', StringComparison.Ordinal);
        if (at > 0)
        {
            var end = lower.IndexOfAny([' ', '\t'], at);
            lower = lower[..at] + (end < 0 ? string.Empty : lower[end..]);
        }

        return [.. Word.Matches(lower).Select(m => m.Value).Where(w => !Noise.Contains(w))];
    }

    // Edit distance where swapping two neighbouring letters counts as one edit, the commonest
    // typo on a phone keyboard. Gives up early once it passes what is allowed.
    private static int Distance(string a, string b, int limit)
    {
        if (Math.Abs(a.Length - b.Length) > limit)
        {
            return limit + 1;
        }

        var twoBack = new int[b.Length + 1];
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var best = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    current[j] = Math.Min(current[j], twoBack[j - 2] + 1);
                }

                best = Math.Min(best, current[j]);
            }

            if (best > limit)
            {
                return limit + 1;
            }

            (twoBack, previous, current) = (previous, current, twoBack);
        }

        return previous[b.Length];
    }
}
