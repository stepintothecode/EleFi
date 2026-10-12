using EleFi.Application.Abstractions;

namespace EleFi.Ui.Services;

/// <summary>Something that just happened to a transaction, worth Ele reacting to.</summary>
public enum EleMoment
{
    /// <summary>A transaction was recorded: captured, quick-captured, or recorded for a plan.</summary>
    Recorded = 0,

    /// <summary>A transaction was edited.</summary>
    Updated = 1,

    /// <summary>A transaction was deleted. Softly: it is restorable.</summary>
    Deleted = 2,

    /// <summary>A deleted transaction came back.</summary>
    Restored = 3,

    /// <summary>A transaction flagged Needs Review was confirmed as it stood.</summary>
    Reviewed = 4,
}

/// <summary>How Ele feels about what it is saying.</summary>
public enum EleTone
{
    /// <summary>Something went right: recorded, linked, merged.</summary>
    Pleased = 0,

    /// <summary>Something neutral happened: deleted, undone, archived. Not pleased, not cross.</summary>
    Calm = 1,

    /// <summary>Something needs fixing: a required field is empty, a value cannot be read.</summary>
    Cross = 2,
}

/// <summary>Ele on screen: how it looks, what it says, and what about.</summary>
/// <param name="Id">Identity, so an old timer cannot dismiss a newer reaction.</param>
/// <param name="Moment">The transaction change it is about, when it is about one.</param>
/// <param name="Tone">How Ele feels about it.</param>
/// <param name="Mood">How Ele looks: follows the tone.</param>
/// <param name="Line">What Ele says.</param>
/// <param name="Amount">The amount involved, shown as money so Hide amounts covers it.</param>
/// <param name="About">Who or what it was about: "Zomato".</param>
public sealed record EleReaction(
    Guid Id,
    EleMoment? Moment,
    EleTone Tone,
    MascotMood Mood,
    string Line,
    string? Amount,
    string? About);

/// <summary>
/// Ele: the one way the app tells the user what just happened.
/// </summary>
/// <remarks>
/// <para>
/// There are no toasts. Every confirmation, every undo and every problem ("Who was it paid
/// to?") is Ele popping up for a few seconds and saying it: pleased when something worked,
/// calm for a deletion or an undo, cross when something needs fixing.
/// </para>
/// <para>
/// One at a time, and never in the way. A new reaction takes the place of the one showing,
/// which slides down and fades (<see cref="Leaving"/>). Each leaves by itself after
/// <see cref="Lifetime"/>, a problem after <see cref="ProblemLifetime"/>, and the cross sends
/// it away at once. While it shows, every tap goes through to the app underneath (NFR-2.7).
/// </para>
/// <para>
/// Scoped like the old toasts, so a reaction raised by a page that then navigates away is
/// still on screen on the next one. Its sound can be switched off in Settings.
/// </para>
/// </remarks>
/// <param name="store">Where the sound choice is kept.</param>
/// <param name="time">The clock behind the timers.</param>
public sealed class EleReactions(ISettingsStore store, TimeProvider time) : IDisposable
{
    /// <summary>The settings key for Ele's sound.</summary>
    public const string SoundKey = "ele.sound";

    // Several per moment, taken in turn, so recording a week's backlog does not have Ele
    // repeat itself on every one. None says "saved": that reads as money put aside.
    private static readonly Dictionary<EleMoment, string[]> Lines = new()
    {
        [EleMoment.Recorded] =
        [
            "Got it, that's recorded!",
            "Noted! Your ledger is up to date.",
            "Another one in the book.",
            "Recorded. Nicely done!",
        ],
        [EleMoment.Updated] =
        [
            "All fixed up.",
            "Updated. Looking tidy!",
            "Changed, and the history remembers.",
        ],
        [EleMoment.Deleted] =
        [
            "Deleted. It waits in Settings if you want it back.",
            "Removed. Still there in Deleted if you need it.",
        ],
        [EleMoment.Restored] =
        [
            "Welcome back! It's flagged for a quick check.",
            "Back in the ledger. Give it a look when you can.",
        ],
        [EleMoment.Reviewed] =
        [
            "Checked and confirmed. Thanks!",
            "Looks right to me too.",
        ],
    };

    private readonly Lock _gate = new();
    private readonly Dictionary<EleMoment, int> _said = [];
    private ITimer? _timer;
    private ITimer? _leaveTimer;

    /// <summary>How long Ele stays before leaving by itself.</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromSeconds(4);

    /// <summary>How long a problem stays: a little longer, because it has to be read and acted on.</summary>
    public static TimeSpan ProblemLifetime { get; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a replaced reaction takes to slide down and fade.</summary>
    public static TimeSpan LeaveDuration { get; } = TimeSpan.FromMilliseconds(420);

    /// <summary>Raised when Ele arrives, changes what it is saying, or leaves.</summary>
    public event Action? Changed;

    /// <summary>What Ele is showing now, or null when it is not on screen.</summary>
    public EleReaction? Current { get; private set; }

    /// <summary>The reaction just replaced, sliding down and fading, or null.</summary>
    public EleReaction? Leaving { get; private set; }

    /// <summary>False when the user has switched Ele's sound off.</summary>
    public bool SoundEnabled => !string.Equals(store.Read(SoundKey, "true"), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>The line Ele says for a moment the <paramref name="turn"/>th time it happens.</summary>
    /// <param name="moment">What happened.</param>
    /// <param name="turn">How many times it has happened before, from zero.</param>
    public static string LineFor(EleMoment moment, int turn)
    {
        var lines = Lines[moment];
        return lines[((turn % lines.Length) + lines.Length) % lines.Length];
    }

    /// <summary>The tone of a transaction change: calm for a deletion, pleased otherwise.</summary>
    /// <param name="moment">What happened.</param>
    public static EleTone ToneFor(EleMoment moment) =>
        moment == EleMoment.Deleted ? EleTone.Calm : EleTone.Pleased;

    /// <summary>How Ele looks in a tone: happy, at rest, or cross.</summary>
    /// <param name="tone">The tone.</param>
    public static MascotMood MoodFor(EleTone tone) => tone switch
    {
        EleTone.Pleased => MascotMood.Happy,
        EleTone.Cross => MascotMood.Concerned,
        _ => MascotMood.Idle,
    };

    /// <summary>Turns Ele's sound on or off for this device.</summary>
    /// <param name="enabled">True for sound.</param>
    public void SetSoundEnabled(bool enabled) => store.Write(SoundKey, enabled ? "true" : "false");

    /// <summary>Brings Ele on to react to a change to a transaction.</summary>
    /// <param name="moment">What happened.</param>
    /// <param name="amount">The amount, already formatted, if there was one.</param>
    /// <param name="about">Who or what it was about, if anyone.</param>
    public void React(EleMoment moment, string? amount = null, string? about = null)
    {
        int turn;
        lock (_gate)
        {
            turn = _said.GetValueOrDefault(moment);
            _said[moment] = turn + 1;
        }

        Show(moment, ToneFor(moment), LineFor(moment, turn), amount, about);
    }

    /// <summary>Brings Ele on to say something.</summary>
    /// <param name="line">What Ele says.</param>
    /// <param name="tone">How it feels about it.</param>
    /// <param name="amount">An amount, already formatted, if there is one.</param>
    /// <param name="about">Who or what it is about, if anyone.</param>
    public void Say(string line, EleTone tone = EleTone.Pleased, string? amount = null, string? about = null) =>
        Show(null, tone, line, amount, about);

    /// <summary>Brings Ele on, cross, to say what needs fixing.</summary>
    /// <param name="line">The problem, in words the user can act on.</param>
    public void Problem(string line) => Show(null, EleTone.Cross, line, null, null);

    /// <summary>Sends Ele away now: its cross.</summary>
    public void Dismiss()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;

            if (Current is not { } going)
            {
                return;
            }

            // Leaves the same way as when a new one replaces it: down, and fading.
            Current = null;
            _leaveTimer?.Dispose();
            Leaving = going;
            _leaveTimer = time.CreateTimer(_ => Left(going.Id), null, LeaveDuration, Timeout.InfiniteTimeSpan);
        }

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _leaveTimer?.Dispose();
            _timer = _leaveTimer = null;
        }
    }

    private void Show(EleMoment? moment, EleTone tone, string line, string? amount, string? about)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_gate)
        {
            _timer?.Dispose();

            // The one showing slides down and fades while the new one takes its place.
            if (Current is { } previous)
            {
                _leaveTimer?.Dispose();
                Leaving = previous;
                _leaveTimer = time.CreateTimer(_ => Left(previous.Id), null, LeaveDuration, Timeout.InfiniteTimeSpan);
            }

            var reaction = new EleReaction(
                Guid.CreateVersion7(),
                moment,
                tone,
                MoodFor(tone),
                line.Trim(),
                string.IsNullOrWhiteSpace(amount) ? null : amount,
                string.IsNullOrWhiteSpace(about) ? null : about.Trim());

            Current = reaction;
            var stay = tone == EleTone.Cross ? ProblemLifetime : Lifetime;
            _timer = time.CreateTimer(_ => Expire(reaction.Id), null, stay, Timeout.InfiniteTimeSpan);
        }

        Changed?.Invoke();
    }

    // Only the reaction this timer was started for. A newer one has its own timer.
    private void Expire(Guid id)
    {
        if (Current?.Id == id)
        {
            Dismiss();
        }
    }

    private void Left(Guid id)
    {
        lock (_gate)
        {
            if (Leaving?.Id != id)
            {
                return;
            }

            Leaving = null;
        }

        Changed?.Invoke();
    }
}
