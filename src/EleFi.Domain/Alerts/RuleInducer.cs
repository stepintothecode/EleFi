using System.Text;
using System.Text.RegularExpressions;
using EleFi.Domain.Money;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Alerts;

/// <summary>
/// A message EleFi could not read, and the user's answer to what it says.
/// </summary>
/// <remarks>
/// Every value is copied out of the message exactly as it appears. That is what lets a rule
/// be built from it: the values mark where the variable parts sit, and everything around them
/// is the bank's fixed wording.
/// </remarks>
/// <param name="Sender">Who sent it: "VM-HDFCBK".</param>
/// <param name="Message">The whole message.</param>
/// <param name="Direction">Whether money left (Debit) or arrived (Credit).</param>
/// <param name="Amount">The amount as written: "450.00".</param>
/// <param name="Counterparty">Who was paid, or who paid, as written. Optional.</param>
/// <param name="Last4">The account or card's last four digits as written. Optional.</param>
/// <param name="OccurredOn">The date as written: "30-08-26". Optional.</param>
/// <param name="Note">A note or remark, as written. Optional.</param>
public sealed record TeachingExample(
    string Sender,
    string Message,
    TransactionKind Direction,
    string Amount,
    string? Counterparty = null,
    string? Last4 = null,
    string? OccurredOn = null,
    string? Note = null);

/// <summary>The rule built from an example, or why one could not be.</summary>
/// <param name="Rule">The rule, ready to save. Null when <paramref name="Error"/> is set.</param>
/// <param name="Error">What to tell the user, in words they can act on.</param>
public sealed record TaughtRule(ParseRule? Rule, string? Error)
{
    /// <summary>True when a rule was built.</summary>
    public bool Succeeded => Rule is not null;
}

/// <summary>
/// Builds a Parse Rule from one example message the user has annotated.
/// </summary>
/// <remarks>
/// <para>
/// The idea: a bank sends the same sentence every time with different numbers in it. Mark
/// the numbers in one copy and the rest is the template. The wording between the marked
/// values is kept, with its digits and spacing loosened so a different balance or reference
/// number still matches. Text after the last value (balances, references, helpline numbers)
/// is dropped, apart from the one token that tells a payee's name where to stop.
/// </para>
/// <para>
/// The rule is checked before it is offered: it must read the example back and recover the
/// same values, or nothing is saved. A rule that reads its own example wrongly would read
/// every later message wrongly too.
/// </para>
/// <para>
/// Rules stay data (NFR-8.11): this produces a row, never code, and the user's rule runs
/// under the same gates as a built-in one, including the OTP check (SM4).
/// </para>
/// </remarks>
public static class RuleInducer
{
    /// <summary>A literal stretch longer than this keeps only its ends.</summary>
    private const int LongSegment = 60;

    private const int SegmentEnd = 24;

    private static readonly Regex TraiHeader = new(
        @"^[A-Z]{2}-(?<core>[A-Z0-9]{3,})(-[A-Z])?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private enum Field
    {
        Amount,
        Counterparty,
        Last4,
        OccurredOn,
        Note,
    }

    /// <summary>Builds and checks a rule from an example.</summary>
    /// <param name="example">The message and the user's answers.</param>
    /// <param name="now">The creation instant for the rule.</param>
    public static TaughtRule Induce(TeachingExample example, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(example);

        if (string.IsNullOrWhiteSpace(example.Sender))
        {
            return Fail("Say who sent the message, such as VM-HDFCBK. A rule only ever reads one sender.");
        }

        if (string.IsNullOrWhiteSpace(example.Message))
        {
            return Fail("Paste the whole message first.");
        }

        if (AlertParser.LooksLikeOtp(new SmsBody(example.Message)))
        {
            return Fail("That looks like a one-time passcode. EleFi never reads those, and will not learn to.");
        }

        if (example.Direction is not (TransactionKind.Debit or TransactionKind.Credit))
        {
            return Fail("Say whether money left or arrived.");
        }

        if (!MoneyText.TryParse(example.Amount, Currency.Inr, out var amount) || amount.Minor <= 0)
        {
            return Fail("The amount must be a number greater than zero, copied as it appears.");
        }

        if (example.Last4 is { Length: > 0 } last4 && !Regex.IsMatch(last4.Trim(), @"^\d{4}$", RegexOptions.None, TimeSpan.FromMilliseconds(50)))
        {
            return Fail("For the account, give just its last four digits.");
        }

        var marks = new List<(Field Field, int Start, int Length)>();
        foreach (var (field, value) in Values(example))
        {
            var index = example.Message.IndexOf(value, StringComparison.Ordinal);
            if (index < 0)
            {
                return Fail($"\"{value}\" is not in the message. Copy it exactly as it appears there.");
            }

            marks.Add((field, index, value.Length));
        }

        marks.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 1; i < marks.Count; i++)
        {
            if (marks[i].Start < marks[i - 1].Start + marks[i - 1].Length)
            {
                return Fail("Two of the values overlap in the message. Each must be a separate part of it.");
            }
        }

        var body = BuildBody(example, marks);
        var rule = new ParseRule
        {
            Name = $"Taught: {SenderCore(example.Sender)} {(example.Direction == TransactionKind.Debit ? "debit" : "credit")}",
            Channel = AlertChannel.Sms,
            SenderPattern = SenderPattern(example.Sender),
            BodyPattern = body,
            Direction = example.Direction,

            // Ahead of the built-ins: the user taught this because the built-ins got it wrong.
            Priority = 1,
            IsBuiltIn = false,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        return ReadsBack(rule, example, amount.Minor)
            ? new TaughtRule(rule, null)
            : Fail("EleFi could not build a pattern that reads this message back the same way. "
                   + "Check each value is copied exactly, then try again.");
    }

    private static IEnumerable<(Field Field, string Value)> Values(TeachingExample example)
    {
        yield return (Field.Amount, example.Amount.Trim());

        if (!string.IsNullOrWhiteSpace(example.Counterparty))
        {
            yield return (Field.Counterparty, example.Counterparty.Trim());
        }

        if (!string.IsNullOrWhiteSpace(example.Last4))
        {
            yield return (Field.Last4, example.Last4.Trim());
        }

        if (!string.IsNullOrWhiteSpace(example.OccurredOn))
        {
            yield return (Field.OccurredOn, example.OccurredOn.Trim());
        }

        if (!string.IsNullOrWhiteSpace(example.Note))
        {
            yield return (Field.Note, example.Note.Trim());
        }
    }

    private static string BuildBody(TeachingExample example, List<(Field Field, int Start, int Length)> marks)
    {
        var message = example.Message;
        var pattern = new StringBuilder();

        // A few words before the first value anchor it, without demanding the greeting that
        // some banks vary ("Dear Customer," against nothing at all).
        pattern.Append(Loosen(LastWords(message[..marks[0].Start], 3)));

        for (var i = 0; i < marks.Count; i++)
        {
            var (field, start, length) = marks[i];
            pattern.Append(GroupFor(field, message.Substring(start, length)));

            var after = start + length;
            if (i + 1 < marks.Count)
            {
                pattern.Append(Segment(message[after..marks[i + 1].Start]));
            }
            else if (field is Field.Counterparty or Field.Note)
            {
                // An open-ended value needs to know where it stops. The next token of the
                // message does that; everything after it is the bank's tail and is dropped.
                pattern.Append(StopAfter(message[after..]));
            }
        }

        return pattern.ToString();
    }

    private static string GroupFor(Field field, string sample) => field switch
    {
        Field.Amount => @"(?<amount>\d[\d,]*(?:\.\d{1,2})?)",
        Field.Last4 => @"(?<last4>\d{4})",
        Field.OccurredOn => $"(?<occurredOn>{DateShape(sample)})",
        Field.Note => @"(?<note>[^\n]+?)",
        _ => @"(?<counterparty>[^\n]+?)",
    };

    // The date's shape, not its value: "30-08-26" becomes digits-dash-digits-dash-digits,
    // "29-Aug-26" keeps its month as a word of letters.
    private static string DateShape(string sample)
    {
        var shape = new StringBuilder();
        var i = 0;
        while (i < sample.Length)
        {
            if (char.IsAsciiDigit(sample[i]))
            {
                while (i < sample.Length && char.IsAsciiDigit(sample[i]))
                {
                    i++;
                }

                shape.Append(@"\d{1,4}");
            }
            else if (char.IsAsciiLetter(sample[i]))
            {
                while (i < sample.Length && char.IsAsciiLetter(sample[i]))
                {
                    i++;
                }

                shape.Append("[A-Za-z]{3,9}");
            }
            else
            {
                shape.Append(char.IsWhiteSpace(sample[i]) ? @"\s+" : Regex.Escape(sample[i].ToString()));
                i++;
            }
        }

        return shape.ToString();
    }

    private static string Segment(string literal) =>
        literal.Length <= LongSegment
            ? Loosen(literal)
            : Loosen(literal[..SegmentEnd]) + @"[\s\S]*?" + Loosen(literal[^SegmentEnd..]);

    private static string StopAfter(string tail)
    {
        var match = Regex.Match(tail, @"^\s*\S+", RegexOptions.None, TimeSpan.FromMilliseconds(50));
        return match.Success ? Loosen(match.Value) : "$";
    }

    private static string LastWords(string text, int count)
    {
        var start = text.Length;
        var seen = 0;

        // Walk back over whole words, keeping the whitespace that separates the last one
        // from the value so "to ZOMATO" still needs its space.
        while (start > 0 && seen < count)
        {
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            seen++;
        }

        return text[start..];
    }

    // The bank's wording kept literal, with what varies between two messages of the same
    // kind loosened: runs of spacing, and runs of digits with their separators (balances,
    // reference numbers, times).
    private static string Loosen(string literal)
    {
        var pattern = new StringBuilder();
        var i = 0;
        while (i < literal.Length)
        {
            var c = literal[i];
            if (char.IsWhiteSpace(c))
            {
                while (i < literal.Length && char.IsWhiteSpace(literal[i]))
                {
                    i++;
                }

                pattern.Append(@"\s+");
            }
            else if (char.IsAsciiDigit(c))
            {
                while (i < literal.Length && (char.IsAsciiDigit(literal[i])
                       || ((literal[i] is ',' or '.') && i + 1 < literal.Length && char.IsAsciiDigit(literal[i + 1]))))
                {
                    i++;
                }

                pattern.Append(@"[\d,.]+");
            }
            else
            {
                pattern.Append(Regex.Escape(c.ToString()));
                i++;
            }
        }

        return pattern.ToString();
    }

    private static string SenderCore(string sender)
    {
        var match = TraiHeader.Match(sender.Trim());
        return match.Success ? match.Groups["core"].Value.ToUpperInvariant() : sender.Trim();
    }

    // A TRAI header's two-letter prefix changes with the route the bank used ("VM-", "AD-",
    // "JD-"), so the rule keeps only the part that names the bank.
    private static string SenderPattern(string sender)
    {
        var trimmed = sender.Trim();
        var match = TraiHeader.Match(trimmed);

        return match.Success
            ? $"^[A-Z]{{2}}-{Regex.Escape(match.Groups["core"].Value.ToUpperInvariant())}(-[A-Z])?$"
            : $"^{Regex.Escape(trimmed)}$";
    }

    private static bool ReadsBack(ParseRule rule, TeachingExample example, long amountMinor)
    {
        if (!rule.TryCompile(out var compiled, out _) || compiled is null)
        {
            return false;
        }

        var parsed = AlertParser.Parse(example.Sender.Trim(), new SmsBody(example.Message), [compiled], Currency.Inr);
        if (parsed is not { } alert || alert.AmountMinor != amountMinor)
        {
            return false;
        }

        return Same(alert.Counterparty, example.Counterparty)
            && Same(alert.Last4, example.Last4)
            && Same(alert.Note, example.Note)
            && (string.IsNullOrWhiteSpace(example.OccurredOn) || alert.OccurredOn is not null);
    }

    // The parser tidies trailing punctuation off a captured name, so compare the same way.
    private static bool Same(string? read, string? expected) =>
        string.IsNullOrWhiteSpace(expected)
        || string.Equals(read?.Trim(), expected.Trim().TrimEnd('.', ',', ';', ':', '!', '-').Trim(), StringComparison.Ordinal);

    private static TaughtRule Fail(string error) => new(null, error);
}
