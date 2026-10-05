using EleFi.Domain.Money;
using EleFi.Domain.Primitives;

namespace EleFi.Domain.Goals;

/// <summary>How a goal's progress is measured. Chosen per goal.</summary>
public enum FundingMode
{
    /// <summary>
    /// Progress is the combined balance of the linked containers. For money that is
    /// dedicated: an RD opened only for this goal.
    /// </summary>
    TracksContainerBalance = 0,

    /// <summary>
    /// Progress is an opening allocation plus the transactions attributed to the goal. For
    /// money mixed in with everything else.
    /// </summary>
    TracksContributions = 1,
}

/// <summary>How a goal is doing against what it should have by today.</summary>
public enum GoalStatus
{
    /// <summary>At or above 95% of what it should be by today.</summary>
    OnTrack = 0,

    /// <summary>Below that.</summary>
    Behind = 1,

    /// <summary>The target is reached.</summary>
    Achieved = 2,
}

/// <summary>
/// A named savings target: "₹3,00,000 for an engagement ring by August 2028".
/// </summary>
/// <remarks>
/// <b>A goal never creates money and never counts toward net worth (GL3).</b> Its progress is
/// a view over money already counted in a container, and it is always derived, never stored.
/// </remarks>
public class Goal : Entity
{
    /// <summary>What it is for.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The target, in minor units. Always positive (GL2).</summary>
    public long TargetAmountMinor { get; set; }

    /// <summary>ISO-4217 code.</summary>
    public string CurrencyCode { get; set; } = Currency.Inr.Code;

    /// <summary>When saving started. Progress expected by today is measured from here.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>When it should be reached. Always after <see cref="StartDate"/> (GL1).</summary>
    public DateOnly TargetDate { get; set; }

    /// <summary>How progress is measured.</summary>
    public FundingMode FundingMode { get; set; }

    /// <summary>An optional monthly amount the user means to put aside.</summary>
    public long? MonthlyContributionMinor { get; set; }

    /// <summary>Money already saved before the goal was set up. Contributions mode only.</summary>
    public long OpeningAllocationMinor { get; set; }

    /// <summary>Put away: kept, with its transactions, but out of the list.</summary>
    public bool IsArchived { get; set; }

    /// <summary>The containers linked to it. Required in balance mode (GL4).</summary>
    public List<GoalContainer> Containers { get; set; } = [];
}

/// <summary>A container linked to a goal.</summary>
public class GoalContainer
{
    /// <summary>The goal.</summary>
    public Guid GoalId { get; set; }

    /// <summary>The container.</summary>
    public Guid ContainerId { get; set; }
}
