using EleFi.Application.Abstractions;

namespace EleFi.Ui.Services;

/// <summary>Which tab a guide step lights up.</summary>
public enum GuideSpot
{
    /// <summary>No tab: the step is about the screen as a whole.</summary>
    None = 0,

    /// <summary>Home.</summary>
    Home,

    /// <summary>Plans, and the goals beside them.</summary>
    Plans,

    /// <summary>Containers.</summary>
    Containers,

    /// <summary>Quick capture.</summary>
    Quick,

    /// <summary>The full capture form.</summary>
    Capture,

    /// <summary>All transactions.</summary>
    All,

    /// <summary>Settings.</summary>
    Settings,
}

/// <summary>One stop on Ele's guide.</summary>
/// <param name="Spot">The tab it lights up.</param>
/// <param name="Route">The page it opens behind the guide, or null to stay where it is.</param>
/// <param name="Title">A few words, in bold.</param>
/// <param name="Line">A sentence or two: what it is and why it matters. Each sentence is shown on a line of its own.</param>
/// <param name="Mood">How Ele looks while saying it.</param>
public sealed record GuideStep(GuideSpot Spot, string? Route, string Title, string Line, MascotMood Mood);

/// <summary>
/// Ele's guide: a short walk through every major feature, over the app itself.
/// </summary>
/// <remarks>
/// <para>
/// Each step opens its page behind a dimmed overlay and lights up its tab, so the user sees
/// the real screen while Ele says one sentence about it. Previous and Next sit beside Ele;
/// Skip, at the top, ends it at any point.
/// </para>
/// <para>
/// It starts on its own once, for a new user: one with no containers who has not finished or
/// skipped it before. After that it is in Settings. Finishing and skipping are remembered on
/// the device, not in the ledger: it is a preference, not user data.
/// </para>
/// </remarks>
/// <param name="store">Where finishing it is remembered.</param>
public sealed class EleGuide(ISettingsStore store)
{
    /// <summary>The setting that records the guide was finished or skipped.</summary>
    public const string DoneKey = "guide.done";

    /// <summary>The walk, in order. Containers come first: nothing else works without one.</summary>
    public static IReadOnlyList<GuideStep> Steps { get; } =
    [
        new(GuideSpot.None, string.Empty, "Hi, I'm Ele!", "I'll show you around. It takes a minute.", MascotMood.Happy),
        new(GuideSpot.Containers, "containers", "Start here",
            "Add where your money lives: bank accounts, cards, cash. Everything else builds on this.", MascotMood.Idle),
        new(GuideSpot.Capture, "capture", "Record money",
            "Amount, who, and which container. Labels and notes if you like.", MascotMood.Idle),
        new(GuideSpot.Quick, null, "In a hurry?",
            "Quick asks three questions. Fill in the rest later.", MascotMood.Idle),
        new(GuideSpot.All, "transactions", "Everything, in one list",
            "Search, filter and export. The arrows at the top undo and redo.", MascotMood.Idle),
        new(GuideSpot.Plans, "plans", "Plans and goals",
            "Bills, SIPs and savings goals. I remind you on the day.", MascotMood.Idle),
        new(GuideSpot.Settings, "settings", "Let me record for you",
            "Turn on bank SMS and payment apps. I record each payment for you to check.", MascotMood.Idle),
        new(GuideSpot.Home, string.Empty, "That's it!",
            "Your money at a glance. Tap me on the home page to hide amounts.", MascotMood.Happy),
    ];

    /// <summary>Raised when the guide opens, moves or closes.</summary>
    public event Action? Changed;

    /// <summary>True while the guide is on screen.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The step showing, counted from zero.</summary>
    public int Index { get; private set; }

    /// <summary>The step showing.</summary>
    public GuideStep Current => Steps[Index];

    /// <summary>True on the first step, where there is nothing to go back to.</summary>
    public bool IsFirst => Index == 0;

    /// <summary>True on the last step, where Next finishes.</summary>
    public bool IsLast => Index == Steps.Count - 1;

    /// <summary>True once the guide has been finished or skipped on this device.</summary>
    public bool HasFinished =>
        string.Equals(store.Read(DoneKey, "false"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>A step's line split into sentences, each shown on a line of its own.</summary>
    /// <param name="line">The step's line.</param>
    public static IReadOnlyList<string> Sentences(string line) =>
        [.. System.Text.RegularExpressions.Regex.Split(line ?? string.Empty, @"(?<=[.!?])\s+")
            .Where(s => s.Length > 0)];

    /// <summary>
    /// True when the guide should start on its own: a new user, with nothing set up, who has
    /// not seen it.
    /// </summary>
    /// <param name="hasContainers">Whether any container exists.</param>
    public bool ShouldStartOnItsOwn(bool hasContainers) => !hasContainers && !HasFinished && !IsOpen;

    /// <summary>Opens the guide at its first step.</summary>
    public void Start()
    {
        Index = 0;
        IsOpen = true;
        Changed?.Invoke();
    }

    /// <summary>Moves on, or finishes on the last step.</summary>
    public void Next()
    {
        if (!IsOpen)
        {
            return;
        }

        if (IsLast)
        {
            Finish();
            return;
        }

        Index++;
        Changed?.Invoke();
    }

    /// <summary>Goes back a step. Does nothing on the first.</summary>
    public void Previous()
    {
        if (!IsOpen || IsFirst)
        {
            return;
        }

        Index--;
        Changed?.Invoke();
    }

    /// <summary>Ends the guide now, from any step. It does not start on its own again.</summary>
    public void Skip() => Finish();

    private void Finish()
    {
        IsOpen = false;
        Index = 0;
        store.Write(DoneKey, "true");
        Changed?.Invoke();
    }
}
