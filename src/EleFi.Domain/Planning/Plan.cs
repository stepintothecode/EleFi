using EleFi.Domain.Primitives;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Planning;

/// <summary>Where an open plan sits in the list.</summary>
public enum PlanBucket
{
    /// <summary>Due before today and not done.</summary>
    Overdue = 0,

    /// <summary>Due today.</summary>
    Today = 1,

    /// <summary>Due in the next six days.</summary>
    ThisWeek = 2,

    /// <summary>Due after that.</summary>
    Later = 3,
}

/// <summary>
/// Money the user expects to move: a SIP, the rent, the card bill, paying a friend back.
/// </summary>
/// <remarks>
/// <para>
/// <b>A Plan is not a Transaction.</b> It never touches a balance, a report or an export
/// (D2, X1). It becomes money only through a real transaction: one recorded by hand, or by an
/// SMS or a payment app, that the plan is then linked to.
/// </para>
/// <para>
/// A repeating plan works like a repeating task in a to-do app: completing one occurrence
/// marks it done and creates the next one. It rolls forward on completion, not on the
/// calendar, so a month that was missed stays overdue instead of being quietly replaced.
/// </para>
/// </remarks>
public class Plan : Entity
{
    /// <summary>What it is: "SIP, Axis Bluechip".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Anything else worth remembering.</summary>
    public string? Note { get; set; }

    /// <summary>The amount expected, in minor units, or null for a plan with no fixed amount.</summary>
    public long? AmountMinor { get; set; }

    /// <summary>The currency of <see cref="AmountMinor"/>.</summary>
    public string CurrencyCode { get; set; } = Money.Currency.Inr.Code;

    /// <summary>Whether money leaves, arrives, or moves between the user's own containers.</summary>
    public TransactionKind Direction { get; set; } = TransactionKind.Debit;

    /// <summary>The user's container at the plan's end, if chosen. The Source for a transfer.</summary>
    public Guid? ContainerId { get; set; }

    /// <summary>Who is paid or who pays, for a Debit or Credit.</summary>
    public string? CounterpartyName { get; set; }

    /// <summary>The container money moves into, for a transfer such as a SIP into a mutual fund.</summary>
    public Guid? DestinationContainerId { get; set; }

    /// <summary>When it is due.</summary>
    public DateOnly DueOn { get; set; }

    /// <summary>The time it is due, if it matters.</summary>
    public TimeOnly? DueTime { get; set; }

    /// <summary>How often it comes round.</summary>
    public RepeatFrequency RepeatFrequency { get; set; }

    /// <summary>Every how many of <see cref="RepeatFrequency"/>.</summary>
    public int RepeatInterval { get; set; } = 1;

    /// <summary>For a weekly plan, the days it falls on; none for the weekday it was first due.</summary>
    public RepeatDays RepeatDays { get; set; }

    /// <summary>The day of the month a monthly or yearly series keeps to. 31 is the last day.</summary>
    public int AnchorDay { get; set; } = 1;

    /// <summary>The last date an occurrence may fall on, or null for no end.</summary>
    public DateOnly? RepeatUntil { get; set; }

    /// <summary>How many occurrences the series has in all, or null for no limit.</summary>
    public int? RepeatCount { get; set; }

    /// <summary>Which occurrence of its series this is, from one.</summary>
    public int Occurrence { get; set; } = 1;

    /// <summary>Shared by every occurrence of one repeating plan.</summary>
    public Guid SeriesId { get; set; }

    /// <summary>When it was ticked off, or null while open.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The transaction it became, when there was one.</summary>
    public Guid? TransactionId { get; set; }

    /// <summary>True when this occurrence was skipped rather than done.</summary>
    public bool IsSkipped { get; set; }

    /// <summary>
    /// The day its reminder fires, when snoozed; otherwise the reminder fires on the due date.
    /// </summary>
    public DateOnly? RemindOn { get; set; }

    /// <summary>
    /// Labels to carry into the transaction it becomes. Any number, as on a transaction.
    /// </summary>
    public List<PlanLabel> Labels { get; init; } = [];

    /// <summary>The repeat rule, as a value.</summary>
    public RepeatRule Repeat
    {
        get => new(RepeatFrequency, RepeatInterval, RepeatDays);
        set
        {
            RepeatFrequency = value.Frequency;
            RepeatInterval = Math.Max(1, value.Interval);
            RepeatDays = value.Frequency == RepeatFrequency.Weekly ? value.Days : RepeatDays.None;
        }
    }

    /// <summary>True once ticked off, or skipped.</summary>
    public bool IsDone => CompletedAt is not null;

    /// <summary>The day its reminder should fire: the snoozed day, or the due date.</summary>
    public DateOnly ReminderDay => RemindOn ?? DueOn;

    /// <summary>Where an open plan belongs in the list, as of today.</summary>
    /// <param name="today">Today, from the device clock.</param>
    public PlanBucket BucketOn(DateOnly today) =>
        DueOn < today ? PlanBucket.Overdue
        : DueOn == today ? PlanBucket.Today
        : DueOn <= today.AddDays(6) ? PlanBucket.ThisWeek
        : PlanBucket.Later;

    /// <summary>
    /// Ticks this occurrence off and returns the next one, if it repeats.
    /// </summary>
    /// <param name="now">The completion instant.</param>
    /// <param name="transactionId">The transaction it became, if any.</param>
    /// <returns>The next occurrence, not yet saved, or null.</returns>
    public Plan? Complete(DateTimeOffset now, Guid? transactionId)
    {
        CompletedAt = now;
        TransactionId = transactionId;
        UpdatedAt = now;

        if (NextDue() is not { } next)
        {
            return null;
        }

        var following = new Plan
        {
            Title = Title,
            Note = Note,
            AmountMinor = AmountMinor,
            CurrencyCode = CurrencyCode,
            Direction = Direction,
            ContainerId = ContainerId,
            CounterpartyName = CounterpartyName,
            DestinationContainerId = DestinationContainerId,
            DueOn = next,
            DueTime = DueTime,
            RepeatFrequency = RepeatFrequency,
            RepeatInterval = RepeatInterval,
            RepeatDays = RepeatDays,
            AnchorDay = AnchorDay,
            RepeatUntil = RepeatUntil,
            RepeatCount = RepeatCount,
            Occurrence = Occurrence + 1,
            SeriesId = SeriesId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        following.Labels.AddRange(Labels.Select(l => new PlanLabel { PlanId = following.Id, LabelId = l.LabelId }));
        return following;
    }

    /// <summary>
    /// Skips this occurrence: it is put away without a transaction, and the series rolls on.
    /// </summary>
    /// <param name="now">The instant.</param>
    /// <returns>The next occurrence, not yet saved, or null.</returns>
    public Plan? Skip(DateTimeOffset now)
    {
        IsSkipped = true;
        return Complete(now, null);
    }

    /// <summary>
    /// When the next occurrence is due, or null when this one is the last: it does not repeat,
    /// the next would fall after its end date, or its count is used up.
    /// </summary>
    public DateOnly? NextDue()
    {
        if (Repeat.NextAfter(DueOn, AnchorDay) is not { } next)
        {
            return null;
        }

        if (RepeatUntil is { } until && next > until)
        {
            return null;
        }

        return RepeatCount is { } count && Occurrence >= count ? null : next;
    }
}

/// <summary>Joins a plan to a label it carries into its transaction.</summary>
public class PlanLabel
{
    /// <summary>The plan.</summary>
    public Guid PlanId { get; set; }

    /// <summary>The label.</summary>
    public Guid LabelId { get; set; }
}
