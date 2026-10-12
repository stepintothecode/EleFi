using EleFi.Application.Abstractions;
using EleFi.Domain.Filters;
using EleFi.Domain.Parties;

namespace EleFi.Application.Contacts;

/// <summary>A contact linked to the one being looked at.</summary>
/// <param name="LinkId">The link, for unlinking.</param>
/// <param name="ContactId">The other contact.</param>
/// <param name="Name">Its name.</param>
/// <param name="Relation">How the two are related.</param>
public sealed record LinkedContact(Guid LinkId, Guid ContactId, string Name, ContactRelation Relation);

/// <summary>Everything a contact's page shows.</summary>
/// <param name="Contact">The contact.</param>
/// <param name="Details">Phone numbers, emails, addresses.</param>
/// <param name="Linked">Contacts linked to it as the same or another account. Never "not the same".</param>
/// <param name="Aliases">Its other names.</param>
/// <param name="TransactionCount">Live transactions with it.</param>
/// <param name="Totals">What it paid the user and what the user paid it.</param>
public sealed record ContactProfile(
    Party Contact,
    IReadOnlyList<ContactDetail> Details,
    IReadOnlyList<LinkedContact> Linked,
    IReadOnlyList<string> Aliases,
    int TransactionCount,
    FlowTotals Totals);

/// <summary>A contact written, or why not.</summary>
/// <param name="Contact">The contact, when it worked.</param>
/// <param name="Error">What to tell the user, when it did not.</param>
public sealed record ContactResult(Party? Contact, string? Error)
{
    /// <summary>True when it worked.</summary>
    public bool Succeeded => Error is null;

    /// <summary>It worked.</summary>
    /// <param name="contact">The contact.</param>
    public static ContactResult Ok(Party contact) => new(contact, null);

    /// <summary>It did not.</summary>
    /// <param name="error">Why, in words the user can act on.</param>
    public static ContactResult Fail(string error) => new(null, error);
}

/// <summary>A detail added, or why not.</summary>
/// <param name="Detail">The detail kept, when it worked.</param>
/// <param name="Error">What is wrong with it, when it did not.</param>
public sealed record DetailResult(ContactDetail? Detail, string? Error);

/// <summary>A merge done, or why not.</summary>
/// <param name="Merge">What moved, for Undo, when it worked.</param>
/// <param name="Error">Why not, when it did not.</param>
public sealed record MergeResult(ContactMerge? Merge, string? Error);

/// <summary>Two contacts that look like one, ready to show.</summary>
/// <param name="First">One.</param>
/// <param name="Second">The other.</param>
/// <param name="Reason">Why they look alike.</param>
public sealed record DuplicateSuggestion(ContactSummary First, ContactSummary Second, DuplicateReason Reason)
{
    /// <summary>The reason, as the list says it.</summary>
    public string Why => Reason switch
    {
        DuplicateReason.SameName => "Same name, written differently",
        DuplicateReason.SimilarSpelling => "Nearly the same spelling",
        _ => "One name is part of the other",
    };
}

/// <summary>
/// Contacts: the people and businesses money goes to and comes from, kept tidy.
/// </summary>
/// <remarks>
/// <para>
/// A contact is an external Party, the same row a transaction's Source or Destination points
/// at, so there is nothing to keep in step: rename it and every transaction shows the new
/// name. Containers are never contacts.
/// </para>
/// <para>
/// Everything is checked here before the repository writes anything: names stay unique (P4),
/// details are what they say they are (CT2), links join two different contacts once (CT3),
/// and only two different contacts can be merged (CT4). Nothing a merge moves is destroyed,
/// and Undo puts it back.
/// </para>
/// </remarks>
public sealed class ContactService(IContactRepository contacts, ITransactionRepository transactions, IClock clock)
{
    /// <summary>Every live contact, by name.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task<IReadOnlyList<ContactSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        contacts.ListAsync(cancellationToken);

    /// <summary>Everything a contact's page shows, or null when it is not a live contact.</summary>
    /// <param name="id">The contact.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContactProfile?> ProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contact = await contacts.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return null;
        }

        var linked = new List<LinkedContact>();
        foreach (var link in await contacts.LinksAsync(id, cancellationToken).ConfigureAwait(false))
        {
            if (!link.Relation.Joins())
            {
                continue;
            }

            if (await contacts.FindAsync(link.Other(id), cancellationToken).ConfigureAwait(false) is { Name: { } name } other)
            {
                linked.Add(new LinkedContact(link.Id, other.Id, name, link.Relation));
            }
        }

        var only = new TransactionFilter { PartyIds = [id] };

        return new ContactProfile(
            contact,
            await contacts.DetailsAsync(id, cancellationToken).ConfigureAwait(false),
            [.. linked.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)],
            await contacts.AliasesAsync(id, cancellationToken).ConfigureAwait(false),
            await transactions.CountAsync(only, cancellationToken).ConfigureAwait(false),
            await transactions.TotalsAsync(only, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Adds a contact before anything has been recorded with it.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContactResult> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalised = Party.NormaliseName(name ?? string.Empty);
        if (await NameProblemAsync(normalised, null, cancellationToken).ConfigureAwait(false) is { } problem)
        {
            return ContactResult.Fail(problem);
        }

        return ContactResult.Ok(await contacts.AddAsync(normalised, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Adds a contact picked from the phone, with its number, linked to it. A name already
    /// here gets the number and the link instead of a second contact.
    /// </summary>
    /// <param name="picked">What the user picked.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContactResult> CreateFromPhoneAsync(PickedContact picked, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(picked);

        var name = Party.NormaliseName(picked.Name);
        var existing = (await contacts.ListAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        Party? contact;
        if (existing is not null)
        {
            contact = await contacts.FindAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var created = await CreateAsync(name, cancellationToken).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                return created;
            }

            contact = await contacts.FindAsync(created.Contact!.Id, cancellationToken).ConfigureAwait(false) ?? created.Contact;
        }

        if (contact is null)
        {
            return ContactResult.Fail("That contact could not be found.");
        }

        await LinkPhoneContactAsync(contact.Id, picked, cancellationToken).ConfigureAwait(false);
        return ContactResult.Ok(contact);
    }

    /// <summary>
    /// Renames a contact, optionally keeping the old name as an alias so anything recorded
    /// under it later still lands here (CT4).
    /// </summary>
    /// <param name="id">The contact.</param>
    /// <param name="name">The new name.</param>
    /// <param name="keepOldName">True to keep the old name as an alias.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ContactResult> RenameAsync(Guid id, string name, bool keepOldName, CancellationToken cancellationToken = default)
    {
        var contact = await contacts.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return ContactResult.Fail("That contact no longer exists.");
        }

        var normalised = Party.NormaliseName(name ?? string.Empty);
        if (await NameProblemAsync(normalised, id, cancellationToken).ConfigureAwait(false) is { } problem)
        {
            return ContactResult.Fail(problem);
        }

        var old = contact.Name;
        if (string.Equals(old, normalised, StringComparison.Ordinal))
        {
            return ContactResult.Ok(contact);
        }

        contact.Name = normalised;
        await contacts.UpdateAsync(contact, cancellationToken).ConfigureAwait(false);

        // A change of case is the same name to every lookup (P4), so it needs no alias.
        if (keepOldName && old is not null && !string.Equals(old, normalised, StringComparison.OrdinalIgnoreCase))
        {
            await contacts.AddAliasAsync(id, old, cancellationToken).ConfigureAwait(false);
        }

        return ContactResult.Ok(contact);
    }

    /// <summary>Replaces what the user has written about a contact.</summary>
    /// <param name="id">The contact.</param>
    /// <param name="notes">The notes, or blank for none.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task SetNotesAsync(Guid id, string? notes, CancellationToken cancellationToken = default)
    {
        var contact = await contacts.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return;
        }

        var tidy = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (string.Equals(contact.Notes, tidy, StringComparison.Ordinal))
        {
            return;
        }

        contact.Notes = tidy is { Length: > 2000 } ? tidy[..2000] : tidy;
        await contacts.UpdateAsync(contact, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Adds a phone number, email, address or other detail (CT2).</summary>
    /// <param name="id">The contact.</param>
    /// <param name="kind">What kind of detail.</param>
    /// <param name="value">The detail.</param>
    /// <param name="label">An optional label, such as "Work".</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The detail kept, or what is wrong with it.</returns>
    public async Task<DetailResult> AddDetailAsync(
        Guid id,
        ContactDetailKind kind,
        string value,
        string? label,
        CancellationToken cancellationToken = default)
    {
        if (ContactDetail.Problem(kind, value) is { } problem)
        {
            return new DetailResult(null, problem);
        }

        if (await contacts.FindAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return new DetailResult(null, "That contact no longer exists.");
        }

        var detail = ContactDetail.Create(id, kind, value, label, clock.UtcNow);
        await contacts.AddDetailAsync(detail, cancellationToken).ConfigureAwait(false);
        return new DetailResult(detail, null);
    }

    /// <summary>Removes a detail. Soft, like every delete.</summary>
    /// <param name="detailId">The detail.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task RemoveDetailAsync(Guid detailId, CancellationToken cancellationToken = default) =>
        contacts.RemoveDetailAsync(detailId, cancellationToken);

    /// <summary>Links two contacts, or records that they are not the same (CT3).</summary>
    /// <param name="id">One contact.</param>
    /// <param name="otherId">The other.</param>
    /// <param name="relation">How they are related.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Null when linked; otherwise why not.</returns>
    public async Task<string?> LinkAsync(Guid id, Guid otherId, ContactRelation relation, CancellationToken cancellationToken = default)
    {
        if (id == otherId)
        {
            return "A contact cannot be linked to itself.";
        }

        if (await contacts.FindAsync(id, cancellationToken).ConfigureAwait(false) is null
            || await contacts.FindAsync(otherId, cancellationToken).ConfigureAwait(false) is null)
        {
            return "One of those contacts no longer exists.";
        }

        var existing = await contacts.LinksAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing.Any(l => l.Joins(id, otherId)))
        {
            return "Those two are already linked. Unlink them first to change how.";
        }

        await contacts.AddLinkAsync(ContactLink.Between(id, otherId, relation, clock.UtcNow), cancellationToken)
            .ConfigureAwait(false);
        return null;
    }

    /// <summary>Removes a link. Soft, like every delete.</summary>
    /// <param name="linkId">The link.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task UnlinkAsync(Guid linkId, CancellationToken cancellationToken = default) =>
        contacts.RemoveLinkAsync(linkId, cancellationToken);

    /// <summary>Removes whatever live link joins two contacts. For undoing a link.</summary>
    /// <param name="id">One contact.</param>
    /// <param name="otherId">The other.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task UnlinkBetweenAsync(Guid id, Guid otherId, CancellationToken cancellationToken = default)
    {
        var links = await contacts.LinksAsync(id, cancellationToken).ConfigureAwait(false);
        foreach (var link in links.Where(l => l.Joins(id, otherId)))
        {
            await contacts.RemoveLinkAsync(link.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Folds one contact into another (CT4).</summary>
    /// <param name="keepId">The contact that stays.</param>
    /// <param name="absorbId">The contact folded into it.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<MergeResult> MergeAsync(Guid keepId, Guid absorbId, CancellationToken cancellationToken = default)
    {
        if (keepId == absorbId)
        {
            return new MergeResult(null, "Pick two different contacts to merge.");
        }

        if (await contacts.FindAsync(keepId, cancellationToken).ConfigureAwait(false) is null
            || await contacts.FindAsync(absorbId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new MergeResult(null, "Only two of your contacts can be merged, and one of those is not.");
        }

        return new MergeResult(await contacts.MergeAsync(keepId, absorbId, cancellationToken).ConfigureAwait(false), null);
    }

    /// <summary>Puts a merge back exactly as it was.</summary>
    /// <param name="merge">What the merge moved.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public Task UndoMergeAsync(ContactMerge merge, CancellationToken cancellationToken = default) =>
        contacts.UndoMergeAsync(merge, cancellationToken);

    /// <summary>Contacts that look like the same one, surest first, leaving out pairs already answered.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<DuplicateSuggestion>> DuplicatesAsync(CancellationToken cancellationToken = default)
    {
        var all = await contacts.ListAsync(cancellationToken).ConfigureAwait(false);
        var settled = (await contacts.LinksAsync(null, cancellationToken).ConfigureAwait(false))
            .Select(l => (l.PartyId, l.OtherPartyId))
            .ToList();

        var byId = all.ToDictionary(c => c.Id);

        return [.. ContactDuplicates.Find([.. all.Select(c => (c.Id, c.Name))], settled)
            .Select(p => new DuplicateSuggestion(byId[p.First], byId[p.Second], p.Reason))];
    }

    /// <summary>Links a contact to one picked from the phone, adding the picked number if it is new.</summary>
    /// <param name="id">The contact.</param>
    /// <param name="picked">What the user picked.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task LinkPhoneContactAsync(Guid id, PickedContact picked, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(picked);

        var contact = await contacts.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(picked.LookupKey))
        {
            contact.PhoneContactKey = picked.LookupKey;
            await contacts.UpdateAsync(contact, cancellationToken).ConfigureAwait(false);
        }

        if (picked.Phone is { } phone && ContactDetail.Problem(ContactDetailKind.Phone, phone) is null)
        {
            var details = await contacts.DetailsAsync(id, cancellationToken).ConfigureAwait(false);
            if (!details.Any(d => d.Kind == ContactDetailKind.Phone && SamePhone(d.Value, phone)))
            {
                await contacts.AddDetailAsync(
                    ContactDetail.Create(id, ContactDetailKind.Phone, phone, "From phone", clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The transaction list's filter for a contact, with the contacts linked to it when asked.
    /// </summary>
    /// <param name="id">The contact.</param>
    /// <param name="includeLinked">True to include linked contacts, never ones marked not the same.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<TransactionFilter> FilterForAsync(Guid id, bool includeLinked, CancellationToken cancellationToken = default)
    {
        if (!includeLinked)
        {
            return new TransactionFilter { PartyIds = [id] };
        }

        var links = await contacts.LinksAsync(id, cancellationToken).ConfigureAwait(false);
        return new TransactionFilter
        {
            PartyIds = [id, .. links.Where(l => l.Relation.Joins()).Select(l => l.Other(id)).Distinct()],
        };
    }

    private async Task<string?> NameProblemAsync(string name, Guid? excluding, CancellationToken cancellationToken)
    {
        if (name.Length == 0)
        {
            return "Give the contact a name.";
        }

        if (name.Length > 200)
        {
            return "That name is longer than 200 characters.";
        }

        return await contacts.NameTakenAsync(name, excluding, cancellationToken).ConfigureAwait(false)
            ? excluding is null
                ? $"There is already a contact called {name}."
                : $"There is already a contact called {name}. Merge the two instead, from either contact's page."
            : null;
    }

    // The same number however it was written: "+91 98765 43210" and "9876543210".
    private static bool SamePhone(string a, string b)
    {
        var (x, y) = (new string([.. a.Where(char.IsAsciiDigit)]), new string([.. b.Where(char.IsAsciiDigit)]));
        return string.Equals(x, y, StringComparison.Ordinal)
            || (x.Length >= 10 && y.Length >= 10 && string.Equals(x[^10..], y[^10..], StringComparison.Ordinal));
    }
}
