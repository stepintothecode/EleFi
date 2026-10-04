using EleFi.Application.Abstractions;
using EleFi.Domain.Balances;
using EleFi.Domain.Containers;
using EleFi.Domain.Money;

namespace EleFi.Application.Containers;

/// <summary>What the "add a container" form gathered.</summary>
/// <param name="Name">What the user calls it.</param>
/// <param name="Kind">Which of the seven kinds.</param>
/// <param name="CurrencyCode">The currency it is denominated in.</param>
/// <param name="OpeningBalanceMinor">What it held on the as-of date.</param>
/// <param name="OpeningBalanceAsOf">The day tracking started.</param>
/// <param name="InstitutionName">The bank or issuer, for bank-like kinds.</param>
/// <param name="AccountNumberLast4">Last four digits only. Never more.</param>
/// <param name="Ifsc">The IFSC code, for Indian bank accounts.</param>
/// <param name="CreditLimitMinor">The credit limit, for cards.</param>
/// <param name="StatementDay">Statement day of month, for cards.</param>
/// <param name="PaymentDueDay">Payment due day of month, for cards.</param>
/// <param name="Colour">A hex colour for the UI.</param>
public sealed record CreateContainerRequest(
    string Name,
    ContainerKind Kind,
    string CurrencyCode,
    long OpeningBalanceMinor,
    DateOnly OpeningBalanceAsOf,
    string? InstitutionName = null,
    string? AccountNumberLast4 = null,
    string? Ifsc = null,
    long? CreditLimitMinor = null,
    int? StatementDay = null,
    int? PaymentDueDay = null,
    string? Colour = null);

/// <summary>The outcome of creating a container.</summary>
/// <param name="Container">The saved container, when it succeeded.</param>
/// <param name="Error">Why it was refused, when it failed.</param>
public readonly record struct ContainerResult(Container? Container, string? Error)
{
    /// <summary>True when a container was written.</summary>
    public bool Succeeded => Container is not null;
}

/// <summary>
/// Creates and edits Money Containers, and reports their derived balances.
/// </summary>
public sealed class ContainerService(
    IContainerRepository containers,
    ITransactionRepository transactions,
    IClock clock)
{
    /// <summary>Every container, with archived ones last.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<Container>> ListAsync(CancellationToken cancellationToken = default) =>
        containers.ListAsync(cancellationToken);

    /// <summary>Containers that belong in a picker.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<Container>> ListSelectableAsync(CancellationToken cancellationToken = default) =>
        containers.ListSelectableAsync(cancellationToken);

    /// <summary>
    /// Every container's balance, derived on this read.
    /// </summary>
    /// <remarks>No balance is stored, at any layer, including a cache (D2).</remarks>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<ContainerBalance>> BalancesAsync(CancellationToken cancellationToken = default) =>
        transactions.BalancesAsync(cancellationToken);

    /// <summary>
    /// Net worth, split into Liquid, Locked, and Owed.
    /// </summary>
    /// <remarks>
    /// Always presented with its three components, because the total alone hides the
    /// situation that actually matters: most of the wealth locked away and a card bill due.
    /// </remarks>
    /// <param name="homeCurrency">The currency to report in.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<NetWorth> NetWorthAsync(
        Currency homeCurrency,
        CancellationToken cancellationToken = default)
    {
        var balances = await transactions.BalancesAsync(cancellationToken).ConfigureAwait(false);

        // v1 is single-currency for aggregates. Foreign containers are excluded rather
        // than converted at a rate the app does not have yet, because a wrong total is
        // worse than an incomplete one that says so.
        var inHomeCurrency = balances
            .Where(b => string.Equals(b.Balance.Currency.Code, homeCurrency.Code, StringComparison.Ordinal))
            .ToList();

        return BalanceMath.Summarise(inHomeCurrency, homeCurrency);
    }

    /// <summary>
    /// Validates and creates a container, along with the Party row that represents it.
    /// </summary>
    /// <param name="request">What the form gathered.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContainerResult> CreateAsync(
        CreateContainerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new ContainerResult(null, "Give the container a name.");
        }

        // Two containers called "HDFC" are indistinguishable in every picker, filter chip,
        // and export row, so the user cannot tell which one a transaction touched.
        if (await containers.NameTakenAsync(request.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return new ContainerResult(null, $"You already have a container called '{request.Name.Trim()}'.");
        }

        if (!Currency.TryOf(request.CurrencyCode, out var currency))
        {
            return new ContainerResult(null, "That currency code is not one I recognise.");
        }

        if (request.OpeningBalanceAsOf > clock.Today)
        {
            return new ContainerResult(null, "The opening balance date cannot be in the future.");
        }

        // C2: card-only fields belong only on a card. Silently keeping a statement day on a
        // cash wallet would make a later query for "cards with bills due" wrong.
        if (request.Kind != ContainerKind.CreditCard
            && (request.CreditLimitMinor is not null || request.StatementDay is not null || request.PaymentDueDay is not null))
        {
            return new ContainerResult(null, "Credit limit, statement day, and due day apply to credit cards only.");
        }

        if (request.StatementDay is < 1 or > 31 || request.PaymentDueDay is < 1 or > 31)
        {
            return new ContainerResult(null, "Statement and due days must be between 1 and 31.");
        }

        var now = clock.UtcNow;
        var container = new Container
        {
            Name = request.Name.Trim(),
            Kind = request.Kind,
            CurrencyCode = currency.Code,
            OpeningBalanceMinor = request.OpeningBalanceMinor,
            OpeningBalanceAsOf = request.OpeningBalanceAsOf,
            InstitutionName = string.IsNullOrWhiteSpace(request.InstitutionName) ? null : request.InstitutionName.Trim(),
            AccountNumberLast4 = request.AccountNumberLast4,
            Ifsc = string.IsNullOrWhiteSpace(request.Ifsc) ? null : request.Ifsc.Trim().ToUpperInvariant(),
            CreditLimitMinor = request.CreditLimitMinor,
            StatementDay = request.StatementDay,
            PaymentDueDay = request.PaymentDueDay,
            Colour = request.Colour,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await containers.AddAsync(container, cancellationToken).ConfigureAwait(false);
        return new ContainerResult(container, null);
    }

    /// <summary>How many transactions reference a container, at either end.</summary>
    /// <remarks>
    /// Exposed so a delete confirmation can say what will actually happen rather than
    /// refusing after the user has committed to it.
    /// </remarks>
    /// <param name="containerId">The container.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<int> TransactionCountAsync(Guid containerId, CancellationToken cancellationToken = default) =>
        containers.TransactionCountAsync(containerId, cancellationToken);

    /// <summary>
    /// Renames a container and updates the fields that are safe to change.
    /// </summary>
    /// <remarks>
    /// Currency and opening balance are not here. C5 makes currency immutable once
    /// transactions exist, because changing it would silently reinterpret every historical
    /// amount. The opening balance is editable through its own method, since changing it
    /// moves every derived balance and deserves its own confirmation.
    /// </remarks>
    /// <param name="id">The container.</param>
    /// <param name="name">The new name.</param>
    /// <param name="institutionName">The bank or issuer.</param>
    /// <param name="accountNumberLast4">Last four digits only.</param>
    /// <param name="colour">A hex colour.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContainerResult> RenameAsync(
        Guid id,
        string name,
        string? institutionName = null,
        string? accountNumberLast4 = null,
        string? colour = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ContainerResult(null, "Give the container a name.");
        }

        var container = await containers.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (container is null)
        {
            return new ContainerResult(null, "That container no longer exists.");
        }

        if (await containers.NameTakenAsync(name, id, cancellationToken).ConfigureAwait(false))
        {
            return new ContainerResult(null, $"You already have a container called '{name.Trim()}'.");
        }

        container.Name = name.Trim();
        container.InstitutionName = string.IsNullOrWhiteSpace(institutionName) ? null : institutionName.Trim();
        container.AccountNumberLast4 = accountNumberLast4;
        container.Colour = colour;
        container.UpdatedAt = clock.UtcNow;

        await containers.UpdateAsync(container, cancellationToken).ConfigureAwait(false);
        return new ContainerResult(container, null);
    }

    /// <summary>
    /// Corrects the opening balance, which moves every derived balance for this container.
    /// </summary>
    /// <param name="id">The container.</param>
    /// <param name="openingBalanceMinor">The corrected opening balance, signed.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContainerResult> SetOpeningBalanceAsync(
        Guid id,
        long openingBalanceMinor,
        CancellationToken cancellationToken = default)
    {
        var container = await containers.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (container is null)
        {
            return new ContainerResult(null, "That container no longer exists.");
        }

        container.OpeningBalanceMinor = openingBalanceMinor;
        container.UpdatedAt = clock.UtcNow;

        await containers.UpdateAsync(container, cancellationToken).ConfigureAwait(false);
        return new ContainerResult(container, null);
    }

    /// <summary>
    /// Archives a container: hidden from pickers, still counted in history (C4).
    /// </summary>
    /// <param name="id">The container.</param>
    /// <param name="archived">Whether it should be archived.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task ArchiveAsync(Guid id, bool archived, CancellationToken cancellationToken = default)
    {
        var container = await containers.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (container is null)
        {
            return;
        }

        container.IsArchived = archived;
        container.UpdatedAt = clock.UtcNow;
        await containers.UpdateAsync(container, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a container, but only while nothing references it.
    /// </summary>
    /// <remarks>
    /// C4: a container with transactions is archived, never deleted. Removing it would
    /// orphan rows describing money that genuinely moved, and no later report could explain
    /// where it went. The caller is told to archive instead rather than silently doing
    /// something different from what the button said.
    /// </remarks>
    /// <param name="id">The container.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContainerResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var container = await containers.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (container is null)
        {
            return new ContainerResult(null, "That container no longer exists.");
        }

        var used = await containers.TransactionCountAsync(id, cancellationToken).ConfigureAwait(false);
        if (used > 0)
        {
            return new ContainerResult(
                null,
                $"'{container.Name}' has {used} {(used == 1 ? "transaction" : "transactions")}, so it cannot be "
                + "deleted. Archive it instead: it disappears from the pickers and your history stays correct.");
        }

        await containers.SoftDeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return new ContainerResult(container, null);
    }
}
