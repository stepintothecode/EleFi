using System.Text.RegularExpressions;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Alerts;

/// <summary>
/// The Parse Rules that ship with the app: bank SMS formats and Payment App notifications.
/// </summary>
/// <remarks>
/// <para>
/// Rules are data (NFR-8.11), and they live here rather than in the seeding code so the
/// corpus tests run against exactly the rules the app ships. A test against a hand-copied
/// pattern proves nothing about the one on the device.
/// </para>
/// <para>
/// Bundled, never fetched (FR-11.26). Every pattern is deliberately narrow: a wrong
/// suggestion costs the user attention and trust, a missed one costs one manual entry.
/// </para>
/// <para>
/// <b>The Payment App formats are best effort.</b> No app publishes its notification
/// wording, and it changes between releases. The patterns accept the common phrasings
/// ("Paid ₹450 to Zomato", "₹450 paid to Zomato", "Rahul paid you ₹500", "Received ₹500
/// from Rahul", "Payment of ₹450 to Zomato was successful") and refuse anything that says
/// failed, pending, or requested. A credit-card bill becomes a Self Transfer suggestion, so
/// paying the card never counts as spending (D1). A phrasing they miss produces nothing, as
/// FR-11.4 requires.
/// </para>
/// </remarks>
public static class BuiltInParseRules
{
    // Named groups the parser recognises: amount, currency, last4, counterparty, occurredOn, note.
    private const string Amount = @"(?<currency>₹|Rs\.?|INR)\s?(?<amount>\d[\d,]*(?:\.\d{1,2})?)";

    // A name runs to the end of the line, a full stop, or a joining word. Lazy, so it stops
    // at the first of those rather than swallowing the rest of the notification.
    private const string Name = @"(?<counterparty>[^\n.,!?₹]{2,60}?)";
    private const string Note = @"(?:\s+for\s+(?<note>[^\n]{1,80}?))?";
    private const string End = @"(?=\s*(?:$|[.,!]|\s(?:using|via|from|on|with|in|into|to|is|was|has|successfully)\b))";

    // Anywhere in the notification, not just beside the amount: "₹450 sent to Zomato.
    // Payment pending" must produce nothing, however the success half is worded.
    private const string Settled =
        @"\A(?![\s\S]*\b(?:failed|failure|pending|declined|reversed|refund|refunded|unsuccessful|cancelled|requested|request)\b)[\s\S]*?";

    private const string AppPaid =
        @"(?m)" + Settled + @"(?:^|\b)(?:"
        + @"(?:you\s+)?(?:paid|sent)\s+" + Amount + @"\s+(?:successfully\s+)?to\s+" + Name + Note + End
        + @"|" + Amount + @"\s+(?:paid|sent)\s+(?:successfully\s+)?to\s+" + Name + Note + End
        + @"|payment\s+of\s+" + Amount + @"\s+to\s+" + Name
        + @"\s+(?:is\s+|was\s+|has\s+been\s+)?(?:successful|completed|done)"
        + @")";

    // A credit-card bill paid through an app is a Self Transfer, not spending: the purchases
    // on the card were already counted when they were made. The card is named but not read
    // as last four digits, because it is the Destination, and SM11 matches only the Source.
    private const string AppCardBill =
        @"(?m)" + Settled + @"(?:(?:you\s+)?paid\s+" + Amount + @"|payment\s+of\s+" + Amount + @"|" + Amount + @"\s+paid)"
        + @"\s+(?:successfully\s+)?(?:to|for|towards)\s+(?<counterparty>[^\n.,!?₹]{0,40}?credit\s*card(?:\s+bill)?)\b";

    private const string AppReceived =
        @"(?m)" + Settled + @"(?:"
        + @"^\s*" + Name + @"\s+(?:has\s+)?(?:paid|sent)\s+you\s+" + Amount
        + @"|received\s+" + Amount + @"\s+from\s+" + Name + Note + End
        + @"|" + Amount + @"\s+received\s+from\s+" + Name + Note + End
        + @")";

    /// <summary>One shipped rule, before it becomes a row.</summary>
    /// <param name="Name">The rule's name, unique among built-ins. Used to add new ones to an existing install.</param>
    /// <param name="Channel">Which channel it reads.</param>
    /// <param name="Sender">The sender pattern: an SMS header, or an app's package.</param>
    /// <param name="Body">The body pattern.</param>
    /// <param name="Direction">Whether a match means money left or arrived.</param>
    /// <param name="Priority">Lower runs first.</param>
    /// <param name="AppName">The Payment App, for app rules.</param>
    public sealed record Definition(
        string Name,
        AlertChannel Channel,
        string Sender,
        string Body,
        TransactionKind Direction,
        int Priority,
        string? AppName = null);

    /// <summary>Every rule the app ships, SMS first.</summary>
    public static IReadOnlyList<Definition> All { get; } = Build();

    /// <summary>Creates fresh rows for every shipped rule.</summary>
    /// <param name="now">The creation instant.</param>
    public static IReadOnlyList<ParseRule> Create(DateTimeOffset now) =>
        All.Select(d => ToRule(d, now)).ToList();

    /// <summary>Turns one definition into a row.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="now">The creation instant.</param>
    public static ParseRule ToRule(Definition definition, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new ParseRule
        {
            Name = definition.Name,
            Channel = definition.Channel,
            SenderPattern = definition.Sender,
            BodyPattern = definition.Body,
            Direction = definition.Direction,
            Priority = definition.Priority,
            AppName = definition.AppName,
            IsBuiltIn = true,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static List<Definition> Build()
    {
        var rules = new List<Definition>
        {
            new("HDFC debit", AlertChannel.Sms,
                @"^[A-Z]{2}-HDFCBK(-[ST])?$",
                @"(?<currency>Rs\.?|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+debited\s+from\s+(?:a/c\s*)?(?:XX|\*+)?(?<last4>\d{4}).*?(?:to|at)\s+(?<counterparty>[A-Za-z0-9 &._-]{2,40})",
                TransactionKind.Debit, 10),

            // HDFC's UPI format, one fact per line: Sent / From / To / On / Ref.
            new("HDFC UPI sent", AlertChannel.Sms,
                @"^[A-Z]{2}-HDFCBK(-[ST])?$",
                @"(?m)Sent\s+(?<currency>Rs\.?|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+From\s+HDFC\s+Bank\s+A/?C\s*(?:XX|x+|\*+)?(?<last4>\d{4})\s+To\s+(?<counterparty>[^\n]{2,60}?)\s+On\s+(?<occurredOn>\d{2}/\d{2}/\d{2,4})",
                TransactionKind.Debit, 9),

            new("HDFC credit", AlertChannel.Sms,
                @"^[A-Z]{2}-HDFCBK(-[ST])?$",
                @"(?<currency>Rs\.?|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+credited\s+to\s+(?:a/c\s*)?(?:XX|\*+)?(?<last4>\d{4})",
                TransactionKind.Credit, 11),

            new("ICICI debit", AlertChannel.Sms,
                @"^[A-Z]{2}-ICICIB(-[ST])?$",
                @"(?<currency>Rs|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+debited\s+from\s+(?:Acct\s*)?(?:XX|\*+)?(?<last4>\d{4}).*?;\s*(?<counterparty>[A-Za-z0-9 &._-]{2,40})\s+credited",
                TransactionKind.Debit, 20),

            new("SBI debit", AlertChannel.Sms,
                @"^[A-Z]{2}-SBIBNK(-[ST])?$|^[A-Z]{2}-SBIINB(-[ST])?$",
                @"(?<currency>Rs\.?|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+(?:debited|withdrawn).*?(?:A/c\s*)?(?:XX|\*+)?(?<last4>\d{4})",
                TransactionKind.Debit, 30),

            new("Axis debit", AlertChannel.Sms,
                @"^[A-Z]{2}-AXISBK(-[ST])?$",
                @"(?<currency>INR|Rs\.?)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+debited\s*(?:A/c\s*no\.?\s*)?(?:XX|\*+)?(?<last4>\d{4}).*?(?<counterparty>[A-Za-z0-9 &._-]{2,40})",
                TransactionKind.Debit, 40),

            new("Kotak debit", AlertChannel.Sms,
                @"^[A-Z]{2}-KOTAKB(-[ST])?$",
                @"(?<currency>Rs\.?|INR)\s*(?<amount>[\d,]+(?:\.\d{1,2})?)\s+(?:has been )?debited\s+from\s+(?:your\s+)?(?:Kotak\s+Bank\s+)?(?:A/?c\s*)?(?:XX|\*+)?(?<last4>\d{4})",
                TransactionKind.Debit, 50),
        };

        var priority = 200;
        foreach (var app in PaymentApps.All)
        {
            var sender = $"^{Regex.Escape(app.Package)}$";

            // Ahead of "paid", which would otherwise read a card bill as spending.
            rules.Add(new($"{app.Name} card bill", AlertChannel.PaymentApp, sender, AppCardBill,
                TransactionKind.SelfTransfer, priority - 100, app.Name));

            rules.Add(new($"{app.Name} paid", AlertChannel.PaymentApp, sender, AppPaid,
                TransactionKind.Debit, priority++, app.Name));

            rules.Add(new($"{app.Name} received", AlertChannel.PaymentApp, sender, AppReceived,
                TransactionKind.Credit, priority++, app.Name));
        }

        return rules;
    }
}
