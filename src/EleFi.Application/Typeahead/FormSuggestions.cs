using EleFi.Application.Abstractions;

namespace EleFi.Application.Typeahead;

/// <summary>Where on a transaction an app was used.</summary>
public enum AppSlot
{
    /// <summary>"Bought through": the marketplace, such as Zomato or Amazon.</summary>
    BoughtThrough = 0,

    /// <summary>"Paid with": the payment app or method, such as GPay or Cash.</summary>
    PaidWith = 1,
}

/// <summary>The whole-history lists, read once when a form opens.</summary>
/// <param name="Notes">Every note, with how often it was used.</param>
/// <param name="BoughtThrough">Apps used as "bought through", with how often.</param>
/// <param name="PaidWith">Apps used as "paid with", with how often.</param>
/// <param name="AllApps">Every app ever named, used or not.</param>
public sealed record SuggestionPools(
    IReadOnlyList<TypeaheadCandidate> Notes,
    IReadOnlyList<TypeaheadCandidate> BoughtThrough,
    IReadOnlyList<TypeaheadCandidate> PaidWith,
    IReadOnlyList<TypeaheadCandidate> AllApps);

/// <summary>What the three free-text fields offer for the contact on the form.</summary>
/// <param name="Notes">For "Note".</param>
/// <param name="BoughtThrough">For "Bought through".</param>
/// <param name="PaidWith">For "Paid with".</param>
public sealed record FormSuggestionLists(
    IReadOnlyList<TypeaheadCandidate> Notes,
    IReadOnlyList<TypeaheadCandidate> BoughtThrough,
    IReadOnlyList<TypeaheadCandidate> PaidWith);

/// <summary>
/// Suggestions for "Bought through", "Paid with" and "Note" on the capture and edit forms.
/// </summary>
/// <remarks>
/// <para>
/// In priority order: what was used with this contact before, when the contact is known; then
/// what is used most across every transaction; and for apps, then any app ever named. A new
/// contact has no history of its own, so it gets the most used straight away.
/// </para>
/// <para>
/// The whole-history lists are read once when the form opens (<see cref="LoadAsync"/>);
/// only a known contact costs a read as its name is typed, and an unknown one costs nothing.
/// </para>
/// </remarks>
/// <param name="history">Notes and apps as used on transactions.</param>
/// <param name="apps">Every app ever named.</param>
public sealed class FormSuggestions(ISuggestionHistory history, IAppRepository apps)
{
    /// <summary>The whole-history lists, for <see cref="ForAsync"/>.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<SuggestionPools> LoadAsync(CancellationToken cancellationToken = default) => new(
        await history.NotesAsync(null, cancellationToken).ConfigureAwait(false),
        await history.AppsAsync(AppSlot.BoughtThrough, null, cancellationToken).ConfigureAwait(false),
        await history.AppsAsync(AppSlot.PaidWith, null, cancellationToken).ConfigureAwait(false),
        TypeaheadCandidates.From(await apps.ListAsync(cancellationToken).ConfigureAwait(false)));

    /// <summary>The three lists for the contact on the form.</summary>
    /// <param name="pools">From <see cref="LoadAsync"/>.</param>
    /// <param name="partyName">Who was paid or who paid, as typed; blank for a transfer.</param>
    /// <param name="known">Whether that name is an existing contact. A new one has no history.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<FormSuggestionLists> ForAsync(
        SuggestionPools pools,
        string? partyName,
        bool known,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pools);

        IReadOnlyList<TypeaheadCandidate> notes = [];
        IReadOnlyList<TypeaheadCandidate> bought = [];
        IReadOnlyList<TypeaheadCandidate> paid = [];

        var name = partyName?.Trim();
        if (known && !string.IsNullOrEmpty(name))
        {
            notes = await history.NotesAsync(name, cancellationToken).ConfigureAwait(false);
            bought = await history.AppsAsync(AppSlot.BoughtThrough, name, cancellationToken).ConfigureAwait(false);
            paid = await history.AppsAsync(AppSlot.PaidWith, name, cancellationToken).ConfigureAwait(false);
        }

        return new FormSuggestionLists(
            TypeaheadCandidates.InTiers(notes, pools.Notes),
            TypeaheadCandidates.InTiers(bought, pools.BoughtThrough, pools.AllApps),
            TypeaheadCandidates.InTiers(paid, pools.PaidWith, pools.AllApps));
    }
}
