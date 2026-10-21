using System.Text;

namespace EleFi.Domain.Receipts;

/// <summary>
/// Whether two ways of writing a payee are the same payee.
/// </summary>
/// <remarks>
/// A receipt says "GNANI MART PVT LTD", the transaction says "Gnani Mart", and a UPI screen
/// says "gnanimart@okhdfc". Comparing them as typed would flag every one as a discrepancy.
/// Names are compared on their letters and digits only, without legal suffixes, and with the
/// part of a UPI address before the @.
/// </remarks>
public static class PayeeNames
{
    private static readonly HashSet<string> Suffixes = new(StringComparer.Ordinal)
    {
        "pvt", "private", "ltd", "limited", "llp", "inc", "co", "the", "and",
    };

    /// <summary>The words of a name that matter, lower case.</summary>
    /// <param name="name">A name as written anywhere.</param>
    public static IReadOnlyList<string> Words(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var at = name.IndexOf('@', StringComparison.Ordinal);
        var text = at > 0 ? name[..at] : name;

        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            cleaned.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return [.. cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !Suffixes.Contains(w))];
    }

    /// <summary>True when two names are the same payee written two ways.</summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other.</param>
    public static bool Same(string first, string second)
    {
        var a = Words(first);
        var b = Words(second);
        if (a.Count == 0 || b.Count == 0)
        {
            return false;
        }

        // Joined, so "gnanimart" (a UPI handle) and "gnani mart" agree.
        var joinedA = string.Concat(a);
        var joinedB = string.Concat(b);
        if (joinedA == joinedB)
        {
            return true;
        }

        var (shorter, longer) = joinedA.Length <= joinedB.Length ? (joinedA, joinedB) : (joinedB, joinedA);
        if (shorter.Length >= 4 && longer.Contains(shorter, StringComparison.Ordinal))
        {
            return true;
        }

        var shared = a.Intersect(b, StringComparer.Ordinal).Count();
        return shared * 2 >= Math.Max(a.Count, b.Count);
    }
}
