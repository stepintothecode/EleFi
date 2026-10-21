using EleFi.Domain.Alerts;
using EleFi.Domain.Apps;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Labels;
using EleFi.Domain.Parties;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Abstractions;

/// <summary>
/// The persistence seam. Every implementation is the only code that touches the database.
/// </summary>
/// <remarks>
/// Repositories return domain types, never raw rows and never query-able handles. A view
/// model that could compose its own query would eventually forget the soft-delete filter,
/// and a leaked deleted row in a balance is exactly the class of silent wrongness this
/// design exists to prevent.
/// </remarks>
public interface IContainerRepository
{
    /// <summary>Every container, archived ones included.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Container>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Containers that should appear in pickers: not archived, not deleted.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Container>> ListSelectableAsync(CancellationToken cancellationToken = default);

    /// <summary>One container, or null.</summary>
    /// <param name="id">The container's identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Container?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The containers whose stored last-four digits match, for SMS matching (SM11).
    /// </summary>
    /// <remarks>
    /// Returns every match rather than a single one, because two cards ending 4417 is a
    /// real situation and the correct response is to ask the user, not to guess.
    /// </remarks>
    /// <param name="last4">The last four digits from a message.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Container>> FindByLast4Async(string last4, CancellationToken cancellationToken = default);

    /// <summary>Adds a container and creates the Party row that represents it (P3).</summary>
    /// <param name="container">The container to add.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(Container container, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to an existing container.</summary>
    /// <param name="container">The container.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(Container container, CancellationToken cancellationToken = default);

    /// <summary>The Party row representing a container.</summary>
    /// <param name="containerId">The container.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Party?> PartyForAsync(Guid containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another live container already uses this name, case-insensitively.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="excluding">A container to ignore, when renaming one.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<bool> NameTakenAsync(string name, Guid? excluding = null, CancellationToken cancellationToken = default);

    /// <summary>How many live transactions touch this container, at either end.</summary>
    /// <remarks>
    /// C4: a container with transactions cannot be deleted, only archived. Deleting it
    /// would orphan every row that names it, and those rows are real money that moved.
    /// </remarks>
    /// <param name="containerId">The container.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<int> TransactionCountAsync(Guid containerId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a container and the Party row that represents it.</summary>
    /// <param name="containerId">The container.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task SoftDeleteAsync(Guid containerId, CancellationToken cancellationToken = default);

    /// <summary>Brings a soft-deleted container and its Party row back, for undo.</summary>
    /// <param name="containerId">The container.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RestoreAsync(Guid containerId, CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes transactions.</summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Transactions matching a filter, newest first.
    /// </summary>
    /// <remarks>
    /// <b>This is the query the export serialises (X1).</b> There is no second query for
    /// exporting, which is what makes "the export is the filtered view" a guarantee rather
    /// than an intention.
    /// </remarks>
    /// <param name="filter">The filter.</param>
    /// <param name="skip">Rows to skip, for paging.</param>
    /// <param name="take">Rows to return, or null for all of them.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Transaction>> QueryAsync(
        TransactionFilter filter,
        int skip = 0,
        int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>How many transactions a filter selects, for the export button's live count.</summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<int> CountAsync(TransactionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>One transaction with its parties and label loaded, or null.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Transaction?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to an existing transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a transaction. It leaves every balance immediately (T9).</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Brings a soft-deleted transaction back.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Derived balances for every container, computed from opening balances and transactions.
    /// </summary>
    /// <remarks>No balance is stored anywhere (D2). This aggregates on every read.</remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Spending among the transactions a filter selects: the per-label figures and the true
    /// total, separately.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Self Transfers are excluded, always (D1). Moving money between your own containers
    /// is not spending, and this exclusion is what stops a credit-card bill payment from
    /// double-counting every rupee already recorded when it was spent.
    /// </para>
    /// <para>
    /// Takes a filter rather than two dates so the dashboard's ranges, times included, mean
    /// exactly what the list's do. The per-label figures overlap and the total is counted
    /// independently. See <see cref="SpendBreakdown"/>.
    /// </para>
    /// </remarks>
    /// <param name="filter">Which transactions to consider. Usually only a date range.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<SpendBreakdown> SpendByLabelAsync(
        TransactionFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Money in and money out across everything a filter selects, for the list's total.
    /// </summary>
    /// <remarks>
    /// Over the whole filtered set, not the page loaded so far, so the total does not change
    /// as more rows are scrolled into view. Self Transfers count on neither side (D1).
    /// </remarks>
    /// <param name="filter">The filter the list is showing.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<FlowTotals> TotalsAsync(TransactionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Soft-deleted transactions, most recently deleted first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Transaction>> ListDeletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The notes written on past transactions with a payee or payer, each with how often and
    /// how recently it was used, for suggesting the note once "Paid to" is filled in.
    /// </summary>
    /// <param name="partyName">The other party's name, matched without regard to case.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Typeahead.TypeaheadCandidate>> NotesForPartyAsync(string partyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the Steady ledger streak needs: when recording began, and the day of every live
    /// transaction still flagged Needs Review.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<ReviewFacts> ReviewFactsAsync(CancellationToken cancellationToken = default);
}

/// <summary>The ledger's state for the Steady ledger streak.</summary>
/// <param name="FirstRecordedAt">When the earliest live transaction was recorded, or null for none.</param>
/// <param name="NeedsReviewOn">The day of each live transaction still flagged Needs Review.</param>
public sealed record ReviewFacts(DateTimeOffset? FirstRecordedAt, IReadOnlyList<DateOnly> NeedsReviewOn);

/// <summary>What came in and what went out, with transfers between your own containers ignored.</summary>
/// <param name="InMinor">The sum of Credits, in home-currency minor units.</param>
/// <param name="OutMinor">The sum of Debits, in home-currency minor units.</param>
public readonly record struct FlowTotals(long InMinor, long OutMinor)
{
    /// <summary>In minus out. Negative when more left than arrived.</summary>
    public long NetMinor => InMinor - OutMinor;
}

/// <summary>
/// What was spent on transactions carrying one label, over a period.
/// </summary>
/// <remarks>
/// <b>These do not add up to total spend.</b> A transaction with two labels is counted in
/// full under each (ADR-0013), so the figures overlap. Anything showing them alongside a
/// total must compute that total independently, from
/// <see cref="SpendBreakdown.TotalMinor"/>, and must not present the labels as shares of it.
/// </remarks>
/// <param name="LabelId">The label, or null for the unlabelled bucket.</param>
/// <param name="LabelName">The label's name.</param>
/// <param name="Colour">The label's colour, for charts.</param>
/// <param name="AmountMinor">Spend on transactions carrying this label, in home-currency minor units.</param>
public readonly record struct LabelSpend(Guid? LabelId, string LabelName, string? Colour, long AmountMinor);

/// <summary>
/// Spending for a period, with the per-label figures and the real total kept apart.
/// </summary>
/// <remarks>
/// The two are separate fields precisely because they no longer reconcile. Returning only
/// the breakdown would invite summing it, and that sum is larger than the money that
/// actually moved.
/// </remarks>
/// <param name="ByLabel">Per-label figures, largest first. Overlapping.</param>
/// <param name="TotalMinor">What was actually spent, counting each transaction once.</param>
public readonly record struct SpendBreakdown(IReadOnlyList<LabelSpend> ByLabel, long TotalMinor);

/// <summary>
/// Reads the audit trail. There is deliberately no write side.
/// </summary>
/// <remarks>
/// AU1: the trail is append-only, and AU2 puts the appending in database triggers. A
/// repository that could insert an audit event would be a way to forge one, and a trail
/// that can be forged is not worth reading.
/// </remarks>
public interface IAuditRepository
{
    /// <summary>Everything that has happened to one record, newest first.</summary>
    /// <param name="entityType">The table name, such as <c>Transactions</c>.</param>
    /// <param name="entityId">The record.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Audit.AuditEvent>> TimelineAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes labels.</summary>
public interface ILabelRepository
{
    /// <summary>Every live label.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a label.</summary>
    /// <param name="label">The label.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(Label label, CancellationToken cancellationToken = default);

    /// <summary>One label, or null.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Label?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to an existing label.</summary>
    /// <param name="label">The label.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(Label label, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a label and detaches it from every transaction.
    /// </summary>
    /// <remarks>
    /// No replacement is needed now that labels are optional and many. A transaction that
    /// loses its only label becomes unlabelled, which is a legitimate state rather than an
    /// orphan.
    /// </remarks>
    /// <param name="id">The label to remove.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The transactions it was taken off, so an undo can put it back on them.</returns>
    Task<IReadOnlyList<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Brings a deleted label back, on the transactions it was taken off.</summary>
    /// <param name="id">The label.</param>
    /// <param name="transactionIds">The transactions to put it back on.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RestoreAsync(Guid id, IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default);

    /// <summary>True when another live label already uses this name.</summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="excluding">A label to ignore, when renaming one.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<bool> NameTakenAsync(
        string name,
        Guid? excluding = null,
        CancellationToken cancellationToken = default);

    /// <summary>How many live transactions carry this label.</summary>
    /// <param name="labelId">The label.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<int> TransactionCountAsync(Guid labelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many live transactions carry each label, in one query.
    /// </summary>
    /// <remarks>
    /// Labels nobody has used are absent rather than present with zero. Deleted
    /// transactions do not count: a label used only on something since deleted is not one
    /// the user reaches for.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyDictionary<Guid, int>> UsageCountsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes parties, the shared pool behind both ends of a transaction.</summary>
public interface IPartyRepository
{
    /// <summary>
    /// Parties ranked for suggestion: most recently and most frequently used first.
    /// </summary>
    /// <param name="search">Text typed so far, or null for the top of the list.</param>
    /// <param name="limit">How many to return.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Party>> SuggestAsync(
        string? search,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every live external party, for type-to-find fields that search the whole history.
    /// </summary>
    /// <remarks>
    /// Containers are not included: they have no name of their own and are chosen from the
    /// container picker, never typed.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Party>> ListExternalAsync(CancellationToken cancellationToken = default);

    /// <summary>One party, or null.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Party?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an external party by name, or creates it (A4-style auto-create).
    /// </summary>
    /// <remarks>
    /// Auto-creating means capture never stalls to "create the party first", which is one
    /// of the interactions the three-second budget cannot afford.
    /// </remarks>
    /// <param name="name">The party name, normalised by the implementation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Party> GetOrCreateExternalAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Records that a party was used, so suggestions stay ranked.</summary>
    /// <param name="partyIds">The parties used.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task TouchAsync(IEnumerable<Guid> partyIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// What each party is called, for a history that stored only ids.
    /// </summary>
    /// <remarks>
    /// Deleted and merged parties included: a transaction's history still has to say who it
    /// was paid to after that contact was merged into another. A container's party is called
    /// by its container's name. Ids that are not parties are left out.
    /// </remarks>
    /// <param name="ids">The ids to name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>One line in the contact list.</summary>
/// <param name="Id">The contact: an external party.</param>
/// <param name="Name">Its name.</param>
/// <param name="TransactionCount">Live transactions with it, at either end.</param>
/// <param name="LastUsedAt">When it was last used, if ever.</param>
/// <param name="DetailCount">How many phone numbers, emails and addresses it carries.</param>
/// <param name="HasPhoneContact">True when it is linked to a contact on the phone.</param>
public sealed record ContactSummary(
    Guid Id,
    string Name,
    int TransactionCount,
    DateTimeOffset? LastUsedAt,
    int DetailCount,
    bool HasPhoneContact);

/// <summary>
/// Everything a merge moved, so Undo can put it all back exactly.
/// </summary>
/// <param name="KeptId">The contact that stayed.</param>
/// <param name="AbsorbedId">The contact folded into it, now an alias.</param>
/// <param name="MovedSourceIds">Transactions whose Source was the absorbed contact.</param>
/// <param name="MovedDestinationIds">Transactions whose Destination was the absorbed contact.</param>
/// <param name="MovedDetailIds">Details that moved across.</param>
/// <param name="MovedLinkIds">Links re-pointed from the absorbed contact to the kept one.</param>
/// <param name="RetiredLinkIds">Links soft-deleted because they would have joined the kept contact to itself or repeated one it had.</param>
/// <param name="RepointedAliasIds">Older aliases of the absorbed contact, now pointing at the kept one.</param>
/// <param name="KeptNotesBefore">The kept contact's notes before the absorbed one's were added.</param>
/// <param name="KeptPhoneKeyBefore">The kept contact's phone link before the merge.</param>
/// <param name="UsageAdded">How much use was added to the kept contact's count.</param>
public sealed record ContactMerge(
    Guid KeptId,
    Guid AbsorbedId,
    IReadOnlyList<Guid> MovedSourceIds,
    IReadOnlyList<Guid> MovedDestinationIds,
    IReadOnlyList<Guid> MovedDetailIds,
    IReadOnlyList<Guid> MovedLinkIds,
    IReadOnlyList<Guid> RetiredLinkIds,
    IReadOnlyList<Guid> RepointedAliasIds,
    string? KeptNotesBefore,
    string? KeptPhoneKeyBefore,
    int UsageAdded);

/// <summary>
/// Contacts: external parties seen as people and businesses, with their details, links,
/// aliases, and merges.
/// </summary>
public interface IContactRepository
{
    /// <summary>Every live contact, by name.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<ContactSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>One live contact, tracked for editing, or null. Never a container's party.</summary>
    /// <param name="id">The contact.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Domain.Parties.Party?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a name already belongs to another live contact, or is another contact's alias,
    /// case-insensitively (P4, CT4).
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <param name="excluding">The contact being renamed, whose own name and aliases do not count.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<bool> NameTakenAsync(string name, Guid? excluding = null, CancellationToken cancellationToken = default);

    /// <summary>Adds a contact with nothing recorded against it yet.</summary>
    /// <param name="name">Its name, already checked.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Domain.Parties.Party> AddAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to a contact found through this repository.</summary>
    /// <param name="contact">The contact.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(Domain.Parties.Party contact, CancellationToken cancellationToken = default);

    /// <summary>Keeps a name as another name for a contact, so capturing with it lands there (CT4).</summary>
    /// <param name="contactId">The contact it points at.</param>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAliasAsync(Guid contactId, string name, CancellationToken cancellationToken = default);

    /// <summary>A contact's other names: merged-away contacts and names from before a rename.</summary>
    /// <param name="contactId">The contact.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<string>> AliasesAsync(Guid contactId, CancellationToken cancellationToken = default);

    /// <summary>A contact's live details, in the order they were added.</summary>
    /// <param name="contactId">The contact.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Parties.ContactDetail>> DetailsAsync(Guid contactId, CancellationToken cancellationToken = default);

    /// <summary>Adds a checked detail.</summary>
    /// <param name="detail">The detail.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddDetailAsync(Domain.Parties.ContactDetail detail, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a detail.</summary>
    /// <param name="detailId">The detail.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RemoveDetailAsync(Guid detailId, CancellationToken cancellationToken = default);

    /// <summary>Live links, at either end of one contact, or every live link when null.</summary>
    /// <param name="contactId">The contact, or null for all.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Parties.ContactLink>> LinksAsync(Guid? contactId, CancellationToken cancellationToken = default);

    /// <summary>Adds a link.</summary>
    /// <param name="link">The link.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddLinkAsync(Domain.Parties.ContactLink link, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a link.</summary>
    /// <param name="linkId">The link.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RemoveLinkAsync(Guid linkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Folds one contact into another, in one database transaction (CT4): its transactions,
    /// details and links move across, its notes are added, and it becomes an alias.
    /// </summary>
    /// <param name="keepId">The contact that stays.</param>
    /// <param name="absorbId">The contact folded into it.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>What moved, for Undo.</returns>
    Task<ContactMerge> MergeAsync(Guid keepId, Guid absorbId, CancellationToken cancellationToken = default);

    /// <summary>Puts a merge back exactly as it was, in one database transaction.</summary>
    /// <param name="merge">What the merge moved.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UndoMergeAsync(ContactMerge merge, CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes Apps, the shared pool behind marketplace and payment roles.</summary>
public interface IAppRepository
{
    /// <summary>Apps ranked for suggestion.</summary>
    /// <param name="search">Text typed so far.</param>
    /// <param name="limit">How many to return.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<App>> SuggestAsync(
        string? search,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Every live app, for type-to-find fields that search the whole pool.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<App>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds an app by name or creates it (A4).</summary>
    /// <param name="name">The app name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<App> GetOrCreateAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Records that apps were used, so suggestions stay ranked.</summary>
    /// <param name="appIds">The apps used.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task TouchAsync(IEnumerable<Guid> appIds, CancellationToken cancellationToken = default);

    /// <summary>What each app is called, deleted ones included, for a history that stored only ids.</summary>
    /// <param name="ids">The ids to name. Ids that are not apps are left out.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes plans: money the user expects to move.</summary>
public interface IPlanRepository
{
    /// <summary>Plans not yet done, soonest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Planning.Plan>> ListOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Plans done, most recently first.</summary>
    /// <param name="take">How many.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Planning.Plan>> ListCompletedAsync(int take = 100, CancellationToken cancellationToken = default);

    /// <summary>One plan, or null.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Domain.Planning.Plan?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a plan.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(Domain.Planning.Plan plan, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to a plan.</summary>
    /// <param name="plan">The plan, as returned by this repository.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(Domain.Planning.Plan plan, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a plan.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Brings a soft-deleted plan back, for undo.</summary>
    /// <param name="id">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every transaction some plan is linked to, so none is claimed twice.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlySet<Guid>> ClaimedTransactionIdsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes the in-app notification list.</summary>
public interface INoticeRepository
{
    /// <summary>Every notification, newest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Notices.Notice>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>How many are unread, for the bell.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<int> UnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a notification, or rewrites the one already about the same thing (same route),
    /// marking it unread again because it now says something new.
    /// </summary>
    /// <param name="notice">The notification.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpsertAsync(Domain.Notices.Notice notice, CancellationToken cancellationToken = default);

    /// <summary>Marks one read or unread.</summary>
    /// <param name="id">The notification.</param>
    /// <param name="read">True for read.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task SetReadAsync(Guid id, bool read, CancellationToken cancellationToken = default);

    /// <summary>Marks every notification read or unread.</summary>
    /// <param name="read">True for read.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task SetAllReadAsync(bool read, CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes Parse Rules, built-in and taught.</summary>
public interface IParseRuleRepository
{
    /// <summary>Every live rule, in priority order.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<ParseRule>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>One rule, or null.</summary>
    /// <param name="id">The rule.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<ParseRule?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(ParseRule rule, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to a rule.</summary>
    /// <param name="rule">The rule, as returned by this repository.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(ParseRule rule, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a rule.</summary>
    /// <param name="id">The rule.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes Parse Rules and Capture Suggestions.</summary>
public interface ISuggestionRepository
{
    /// <summary>Every rule, enabled or not, in priority order.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<ParseRule>> ListRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>Pending suggestions the user has not acted on.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<CaptureSuggestion>> ListPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggestions created at or after an instant, pending or confirmed, for pairing a new
    /// alert with one already received about the same payment (SM14).
    /// </summary>
    /// <param name="since">The earliest creation instant to include.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<CaptureSuggestion>> ListSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>How many suggestions are waiting for the user, for the dashboard's banner.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a suggestion already holds an alert with this fingerprint, either as its
    /// own or as the one merged into it (SM6).
    /// </summary>
    /// <param name="fingerprint">The fingerprint.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<bool> ExistsAsync(string fingerprint, CancellationToken cancellationToken = default);

    /// <summary>Adds a suggestion.</summary>
    /// <param name="suggestion">The suggestion.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to a suggestion, such as a second alert merged into it.</summary>
    /// <param name="suggestion">The suggestion, as returned by this repository.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(CaptureSuggestion suggestion, CancellationToken cancellationToken = default);

    /// <summary>One suggestion, or null.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<CaptureSuggestion?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a suggestion permanently.
    /// </summary>
    /// <remarks>
    /// A hard delete, deliberately (SM5). Soft delete protects user-entered data; a
    /// suggestion is machine-derived output the user actively rejected, and keeping it
    /// would mean retaining message-derived content after they said no.
    /// </remarks>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks a suggestion confirmed and links the transaction it became (SM7).</summary>
    /// <param name="id">The suggestion.</param>
    /// <param name="transactionId">The transaction created from it.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task MarkConfirmedAsync(Guid id, Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>Destroys suggestions past their expiry (SM5, FR-11.12).</summary>
    /// <param name="asOf">The current instant.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<int> PurgeExpiredAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);
}

/// <summary>Goals and the transactions attributed to them.</summary>
public interface IGoalRepository
{
    /// <summary>Every goal, with its linked containers, oldest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Goals.Goal>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>One goal with its linked containers, or null.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Domain.Goals.Goal?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a goal.</summary>
    /// <param name="goal">The goal.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(Domain.Goals.Goal goal, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to a goal, including which containers are linked.</summary>
    /// <param name="goal">The goal, already loaded through this repository.</param>
    /// <param name="containerIds">The containers it should be linked to afterwards.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(Domain.Goals.Goal goal, IReadOnlyCollection<Guid> containerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a goal and clears it from every transaction attributed to it (GL6). The
    /// transactions themselves are untouched: the money was real regardless of the goal.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Brings a soft-deleted goal back, for undo. Its contributions are re-attributed separately.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Live transactions attributed to any goal, with their parties loaded.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Transaction>> ContributionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Attributes a transaction to a goal, or clears it with null.</summary>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="goalId">The goal, or null for none.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task SetGoalAsync(Guid transactionId, Guid? goalId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The history behind the suggestions on the capture and edit forms: notes, and the apps a
/// purchase was bought through or paid with.
/// </summary>
public interface ISuggestionHistory
{
    /// <summary>Notes written on transactions, with how often each was used.</summary>
    /// <param name="partyName">Only notes on transactions with this contact, at either end; or null for every note.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Typeahead.TypeaheadCandidate>> NotesAsync(string? partyName, CancellationToken cancellationToken = default);

    /// <summary>Apps used in one place on a transaction, with how often each was used there.</summary>
    /// <param name="slot">Bought through, or paid with.</param>
    /// <param name="partyName">Only transactions with this contact, at either end; or null for every transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Typeahead.TypeaheadCandidate>> AppsAsync(Typeahead.AppSlot slot, string? partyName, CancellationToken cancellationToken = default);
}

/// <summary>Receipts and their photos (ADR-0018). The only place their SQL is written.</summary>
public interface IReceiptRepository
{
    /// <summary>Stores a receipt, and its photo when it has one.</summary>
    /// <param name="receipt">The receipt.</param>
    /// <param name="image">Its photo and thumbnail, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task AddAsync(Domain.Receipts.Receipt receipt, Domain.Receipts.ReceiptImage? image, CancellationToken cancellationToken = default);

    /// <summary>One live receipt.</summary>
    /// <param name="id">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Domain.Receipts.Receipt?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>A live receipt carrying this UPI or bank reference, or null.</summary>
    /// <param name="reference">The reference, digits only.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Domain.Receipts.Receipt?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>A transaction's live receipts, oldest first. No photos.</summary>
    /// <param name="transactionId">The transaction.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyList<Domain.Receipts.Receipt>> ForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>The thumbnails of these receipts, by receipt; a receipt with no photo is left out.</summary>
    /// <param name="receiptIds">The receipts.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyDictionary<Guid, byte[]>> ThumbnailsAsync(IReadOnlyCollection<Guid> receiptIds, CancellationToken cancellationToken = default);

    /// <summary>A receipt's full photo, or null when it has none.</summary>
    /// <param name="receiptId">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<byte[]?> PhotoAsync(Guid receiptId, CancellationToken cancellationToken = default);

    /// <summary>Saves a change to a receipt, such as settling it.</summary>
    /// <param name="receipt">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UpdateAsync(Domain.Receipts.Receipt receipt, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a receipt. Its photo stays with it, restorable.</summary>
    /// <param name="id">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Brings a removed receipt back.</summary>
    /// <param name="id">The receipt.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
}
