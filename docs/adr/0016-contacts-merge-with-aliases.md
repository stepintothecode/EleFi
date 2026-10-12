---
status: accepted
date: 2026-10-11
---

# Contacts are external Parties; a merge leaves the old name behind as an alias

The people and businesses money goes to and comes from are **Contacts**. A Contact is not a
new entity beside the Party: it *is* the external Party a transaction already points at,
given optional details (phone numbers, emails, addresses), notes, a link to a contact on the
phone, links to other contacts, and other names (**aliases**).

Duplicates are folded together by a **merge**. The absorbed contact's transactions, details
and links move to the one kept, and its row stays, soft-deleted, as an alias pointing at the
kept contact. Capturing with an alias's name, by hand or from an alert, lands on the kept
contact. A rename can leave the old name behind as an alias the same way.

## Why

The user asked, on 2026-10-11:

> Contacts list... Even merging or linking contacts... Or cleaning up the duplicates. [...]
> Even link a "Contact" from phone's contact if possible. Or if not, then just add phone
> numbers, email, address, etc. details, and notes about a person. Also linking between
> contacts, for example, one contact "is same as" some other contact, or just another account
> of same entity.

Most duplicates are made by alerts, not by people. A bank writes `ZOMATO LTD`, a payment app
writes `Zomato`, a UPI alert writes `zomato@hdfcbank`, and P4's case-insensitive match keeps
the three apart. Merging them once is not enough if the next SMS brings `ZOMATO LTD` straight
back. The alias is what makes a merge stick.

## Considered options

- **A separate Contact entity that Parties point at.** Rejected. Every transaction already
  names its Party; a second layer means two names to keep in step and a join on every list.
  Making the Party the Contact means a rename shows on every transaction at once.
- **Merge by re-pointing transactions and hard-deleting the duplicate.** Rejected. It breaks
  the non-negotiable that deletes are soft, loses the duplicate's name for the audit trail,
  and does nothing about the next alert using that name.
- **Merge into a list of alias strings on the kept Party.** Rejected. An alias as a Party row
  of its own reuses P4's lookup, appears in history name resolution for free, survives the
  backup with no new column shape, and is exactly what Undo needs to bring back.
- **Read the phone's address book with `READ_CONTACTS`.** Rejected. The system picker hands
  back the one contact and number the user picked, with no permission and nothing else of
  the address book seen. What is picked is copied, not kept in step.
- **Link without merge only.** Rejected: links are right for two real accounts of one person,
  wrong for one payee spelt three ways. Both are offered.

## Consequences

- `Parties` gains `Notes`, `PhoneContactKey` and `MergedIntoPartyId` (indexed, no foreign key,
  so the table every transaction points at is not rebuilt). New tables `ContactDetails` and
  `ContactLinks`, both soft-deleting and audited.
- `GetOrCreateExternalAsync` looks up live contacts first, then aliases. A new name never
  matches an alias silently in a way the user did not set up: aliases exist only from a
  merge or a rename they chose.
- A merge moves every transaction through the change tracker, so each one's history records
  the move by name. Undo, offered at once, reverses exactly what moved. Undo is not offered
  later: after other edits there is no single honest "before".
- An alias reserves its name (P4 now covers aliases), so a second contact cannot take it.
  Renaming a contact back to one of its own aliases is allowed.
- A link joins two contacts that stay separate: **Same as**, or **Another account of**. A
  third relation, **Not the same**, joins nothing and only stops a duplicate suggestion
  coming back.
- Duplicate suggestions are computed, never stored: same name written differently, one letter
  out, or one name inside the other. Nothing is merged without the user choosing it.
