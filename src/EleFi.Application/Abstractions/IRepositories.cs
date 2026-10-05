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
}

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
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

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
