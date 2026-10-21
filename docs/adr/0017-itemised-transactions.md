---
status: accepted
date: 2026-10-20
supersedes: 0013 (in part)
---

# A transaction can be split into items, each with its own amount and labels

A transaction may optionally hold **items**: what the one payment bought, each with a name,
an amount in integer minor units, a note, and any number of labels. The items always add up
to the transaction's amount. Whatever the named items leave is one **Unnamed** item, kept
right by the app, so nobody has to type the last item to make the sum work.

With items, the labels live on the items and the transaction carries none of its own. The
spend breakdown counts each item's amount under that item's labels, so a ₹500 shop split into
₹200 Food and ₹300 Groceries counts ₹200 and ₹300, not ₹500 twice.

This supersedes the part of [0013](0013-multiple-flat-labels.md) that rejected "many labels
with amount splits". Everything else in 0013 stands: labels are flat, optional and many per
transaction (or per item), unlabelled is a state, and the breakdown's total is computed once
per transaction and never by summing the buckets.

## Why

The user asked, on 2026-10-20, for exactly the option 0013 kept in reserve:

> a transaction could optionally hold items, each with an amount (integer minor units),
> labels and a note. The items must add up to the transaction's amount. [...] Your ₹500
> would go in as ₹200 Food and ₹300 Groceries, with no double counting.

0013 rejected splits because they put machinery in front of a three-tap capture. That
objection is met by keeping itemising **out of capture**: it is an optional step when editing,
and shop receipts can fill the items in. Capture is unchanged.

## Considered options

- **Keep 0013's overlap and say so on screen.** No schema change. Rejected: the mixed
  purchase that started 0013 is the common case for a grocery or pharmacy run, and "counts in
  full under each" makes those charts wrong in exactly the way people notice.
- **Split the transaction into several transactions.** No new concept. Rejected: one payment
  would become several rows, each with its own Source and Destination, so the bank statement
  and the list stop matching, and a receipt or an alert has nothing single to attach to.
- **Items that need not add up**, with any gap simply unlabelled. Simpler to edit. Rejected:
  the total would be split between the items and an invisible remainder, and a wrong item
  amount would never surface. A visible Unnamed item makes the gap something the user sees.
- **Items, adding up, with an automatic Unnamed remainder.** Chosen.

## Consequences

- **New tables**: `TransactionItems` (id, transaction, name or null for Unnamed, amount, note,
  position) and `TransactionItemLabels`. Invariants `IT1` to `IT5` in the
  [domain model](../requirements/domain-model.md#items-adr-0017). `IT2` (an amount above zero) is a
  CHECK; `IT1` (they add up) spans rows, so `Itemisation.Balance` keeps it on every write and
  a property test tries to break it.
- **Balances, the kind, the Source and the Destination do not change.** An item is never
  money: it divides a transaction for labelling, nothing more.
- **Items are part of the transaction, so there is no soft delete on them.** Removing an item
  is an edit, recorded in the transaction's history ("Item Milk ₹200.00 to nothing") and
  undone like any edit. The transaction's own soft delete keeps its items with it. This is a
  deliberate reading of "deletes are soft": the deleted thing is the transaction, and items
  come and go with edits the way its labels do.
- **The spend breakdown reads items.** Shares come from `LabelShares.Of`, which uses item
  labels when there are items. An item with two labels still counts under both, so the
  buckets can still exceed the total, and the total is still computed per transaction.
- **Filters and search see items.** A label filter matches a transaction through any item's
  label; Unlabelled means no label anywhere; search covers item names and notes.
- **The export stays one row per transaction** (`X1`): a new Items column holds
  `Milk 200.00 (Food); Unnamed 300.00`, and Labels lists the items' labels.
- **Itemising moves labels.** The first item starts bare and the transaction's labels move
  onto Unnamed; removing the last item gives Unnamed's labels back to the transaction. A first
  item that takes the whole amount takes the labels itself. Nothing is unlabelled by accident.
- **A Self Transfer cannot be itemised**, since it is not spending (`T6`). Changing an
  itemised transaction into one is refused until the items are removed.
- **Editing the amount resizes Unnamed**, and an amount below the named items is refused, with
  Ele saying by how much.
- **Cost**: five more audit triggers, two more includes on every transaction load (split
  queries, so rows are not multiplied), and a migration (`TransactionItems`) that only adds
  tables, so no existing row is touched.
