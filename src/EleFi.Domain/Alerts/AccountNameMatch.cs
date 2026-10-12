using System.Text.RegularExpressions;
using EleFi.Domain.Containers;

namespace EleFi.Domain.Alerts;

/// <summary>
/// Finds the container an alert means when it carries no last four digits: by the account
/// it names in words, or by the bank in its sender (SM18).
/// </summary>
/// <remarks>
/// <para>
/// Plenty of messages say "your Kotak Bank account" or come from <c>AD-SBIINB</c> without a
/// single digit of the account number. Recording those against the first card in picker order
/// was a guess the user then had to correct every time.
/// </para>
/// <para>
/// The rule is the same as SM11's for digits: <b>exactly one</b> container must fit, or there
/// is no answer. Words every bank uses ("bank", "account", "card") never establish a match on
/// their own; they only help choose between containers that a real name already matched. Two
/// accounts at the same bank are a question for the user, not a coin toss.
/// </para>
/// <para>
/// Pure: names in, a container or nothing out.
/// </para>
/// </remarks>
public static class AccountNameMatch
{
    // Words that carry no identity at all: never compared.
    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
    {
        "the", "your", "you", "our", "from", "of", "to", "at", "in", "on", "by", "for", "with",
        "a", "an", "ac", "acct", "account", "accounts", "no", "number", "xx", "is", "has", "been",
    };

    // Words many banks share: they never make a match, but they break a tie.
    private static readonly HashSet<string> Weak = new(StringComparer.Ordinal)
    {
        "bank", "card", "credit", "debit", "savings", "saving", "current", "wallet", "payments",
        "payment", "india", "ltd", "limited", "upi", "bk", "cc", "salary",
    };

    private static readonly Regex Word = new(@"[A-Za-z0-9]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    private static readonly Regex TraiHeader = new(
        @"^(?:[A-Z]{2}-)?(?<core>[A-Z0-9]{3,8})(?:-[A-Z])?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    /// <summary>The one container the alert names, or null when none or several fit.</summary>
    /// <param name="candidates">The containers it could be: usually every selectable one.</param>
    /// <param name="accountName">The account as the message names it, if a rule captured it.</param>
    /// <param name="sender">The SMS sender, such as <c>VM-HDFCBK</c>. A Payment App's package never matches.</param>
    public static Container? Find(IReadOnlyList<Container> candidates, string? accountName, string? sender)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var named = ByName(candidates, accountName);
        if (named.Count == 1)
        {
            return named[0];
        }

        // A name that fitted several is narrowed by the sender; with no name, the sender
        // chooses among everything.
        var bySender = BySender(named.Count > 1 ? named : candidates, sender);
        return bySender.Count == 1 ? bySender[0] : null;
    }

    private static List<Container> ByName(IReadOnlyList<Container> candidates, string? accountName)
    {
        var said = Words(accountName);
        if (!said.Exists(w => !Weak.Contains(w)))
        {
            return [];
        }

        var scored = candidates
            .Select(c => (Container: c, Shared: Words($"{c.Name} {c.InstitutionName}").Where(w => said.Exists(s => Alike(s, w))).Distinct().ToList()))
            .Where(x => x.Shared.Exists(w => !Weak.Contains(w)))
            .Select(x => (x.Container, Score: x.Shared.Count))
            .ToList();

        if (scored.Count == 0)
        {
            return [];
        }

        var best = scored.Max(x => x.Score);
        return [.. scored.Where(x => x.Score == best).Select(x => x.Container)];
    }

    private static List<Container> BySender(IReadOnlyList<Container> candidates, string? sender)
    {
        if (string.IsNullOrWhiteSpace(sender) || TraiHeader.Match(sender.Trim()) is not { Success: true } header)
        {
            return [];
        }

        var core = header.Groups["core"].Value.ToUpperInvariant();

        return [.. candidates.Where(c => Words($"{c.Name} {c.InstitutionName}")
            .Exists(w => !Weak.Contains(w) && w.Length >= 3 && core.StartsWith(w.ToUpperInvariant(), StringComparison.Ordinal)))];
    }

    // The same word, or one the start of the other ("Kotak" and "Kotak811"), as long as the
    // shorter is a real word rather than two letters that happen to begin something.
    private static bool Alike(string a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal)
        || (Math.Min(a.Length, b.Length) >= 3
            && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)));

    // Lower case, digits-only tokens dropped: a run of digits is an account number, which
    // SM11 already handles and a name never should.
    private static List<string> Words(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : [.. Word.Matches(text)
                .Select(m => m.Value.ToLowerInvariant())
                .Where(w => !w.All(char.IsAsciiDigit) && !Ignored.Contains(w))];
}
