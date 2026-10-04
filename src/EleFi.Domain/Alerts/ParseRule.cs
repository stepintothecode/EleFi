using System.Text.RegularExpressions;
using EleFi.Domain.Primitives;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Alerts;

/// <summary>
/// A named pattern that turns a bank's SMS into a Capture Suggestion.
/// </summary>
/// <remarks>
/// <para>
/// Rules are <b>data, not code</b>. Supporting a new bank is a row plus a corpus test,
/// never a new class, which is what keeps the maintenance treadmill bounded as message
/// formats drift.
/// </para>
/// <para>
/// A rule that does not match produces nothing. It never guesses a partial transaction: a
/// plausible wrong amount in a money app is worse than no suggestion at all.
/// </para>
/// </remarks>
public class ParseRule : Entity
{
    /// <summary>The named captures a body pattern may use. Anything else is ignored.</summary>
    public static readonly IReadOnlyList<string> RecognisedGroups =
        ["amount", "currency", "last4", "counterparty", "occurredOn", "note"];

    /// <summary>What this rule is called: "HDFC card debit".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Which kind of alert this rule reads. A rule never sees the other channel's messages.
    /// </summary>
    /// <remarks>
    /// Kept separate rather than inferred from the sender pattern, so a crafted SMS sender
    /// can never be matched by a rule written for an app notification, or the reverse.
    /// </remarks>
    public AlertChannel Channel { get; set; } = AlertChannel.Sms;

    /// <summary>
    /// For a Payment App rule, the App the payment went through: "GPay". Null for SMS rules.
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// An anchored regex matched against the SMS sender, such as <c>^[A-Z]{2}-HDFCBK$</c>.
    /// </summary>
    /// <remarks>
    /// The sender is the first gate and the reason this feature is auditable: a message
    /// from an unrecognised sender is discarded before its body is examined (SM3). India's
    /// TRAI header scheme makes these stable and registered, which is what makes
    /// sender-based filtering reliable enough to lead with.
    /// </remarks>
    public string SenderPattern { get; set; } = string.Empty;

    /// <summary>A regex with named groups from <see cref="RecognisedGroups"/>.</summary>
    public string BodyPattern { get; set; } = string.Empty;

    /// <summary>Whether a match means money left or arrived.</summary>
    public TransactionKind Direction { get; set; } = TransactionKind.Debit;

    /// <summary>Whether this rule is currently used.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>True for rules shipped with the app, which the user may disable but not delete.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Lower numbers are tried first, so a specific rule can precede a general one.</summary>
    public int Priority { get; set; }

    /// <summary>
    /// Compiles the two patterns, or reports why they cannot be used.
    /// </summary>
    /// <remarks>
    /// Returns a reason rather than throwing, because a user-written rule with a broken
    /// pattern must show them what is wrong rather than crash the receiver.
    /// </remarks>
    /// <param name="compiled">The compiled rule, when this returns true.</param>
    /// <param name="error">Why compilation failed, when this returns false.</param>
    public bool TryCompile(out CompiledParseRule? compiled, out string? error)
    {
        compiled = null;
        error = null;

        if (string.IsNullOrWhiteSpace(SenderPattern))
        {
            error = "The sender pattern is empty. A rule that matches every sender would read every message.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(BodyPattern))
        {
            error = "The body pattern is empty.";
            return false;
        }

        try
        {
            const RegexOptions Options =
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

            var timeout = TimeSpan.FromMilliseconds(250);
            var sender = new Regex(SenderPattern, Options, timeout);
            var body = new Regex(BodyPattern, Options, timeout);

            if (!body.GetGroupNames().Contains("amount", StringComparer.Ordinal))
            {
                error = "The body pattern must capture a group named 'amount'. Without it there is nothing to suggest.";
                return false;
            }

            compiled = new CompiledParseRule(this, sender, body);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = $"Pattern is not valid: {ex.Message}";
            return false;
        }
    }
}

/// <summary>A <see cref="ParseRule"/> with its patterns compiled and ready to match.</summary>
/// <param name="Rule">The rule this came from.</param>
/// <param name="Sender">The compiled sender pattern.</param>
/// <param name="Body">The compiled body pattern.</param>
public sealed record CompiledParseRule(ParseRule Rule, Regex Sender, Regex Body)
{
    /// <summary>True when this rule is willing to look at a message from this sender.</summary>
    /// <param name="sender">The SMS originating address, or the posting app's package.</param>
    public bool MatchesSender(string sender) =>
        !string.IsNullOrWhiteSpace(sender) && Sender.IsMatch(sender.Trim());

    /// <summary>True when this rule reads alerts from this channel and this sender.</summary>
    /// <param name="channel">Where the alert arrived from.</param>
    /// <param name="sender">The SMS originating address, or the posting app's package.</param>
    public bool Accepts(AlertChannel channel, string sender) =>
        Rule.IsEnabled && Rule.Channel == channel && MatchesSender(sender);
}
