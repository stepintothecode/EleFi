---
status: accepted
supersedes: 0007
---

# Any number of flat Labels per transaction; Tags removed

A transaction carries **zero or more Labels**, drawn from one flat list. There is no
hierarchy, no per-label side (Expense / Income / Both), no system-owned *Uncategorised*
label, and **no Tag entity at all**. Labels work the way tags work in an issue tracker: you
attach the ones that apply.

This reverses [0007](0007-single-label-plus-tags.md), which allowed exactly one Label from a
two-level tree plus unlimited Tags.

Two things were true at once in the shipped app, and they were what forced the change.
First, one label plus tags is **one concept implemented twice**: two tables, two pickers,
two filter sections, and a rule the user has to learn about which of the two counts toward
totals. Second, the hierarchy paid for itself only for a user who had already built one, and
the *Inside* field sat on the new-label form in front of everybody else asking a question
they had no answer to yet.

The arithmetic objection in 0007 was real and has not gone away. It is now handled rather
than avoided: see **Consequences**.

## Considered options

- **Keep 0007 as it stands.** No migration, and `Σ spend_by_label == total_spend` keeps
  holding for free. Rejected: it keeps two mechanisms for one job, and the mixed purchase
  that started this ("groceries and a kettle from the same shop") still has no honest
  answer.
- **Many labels with amount splits** (₹1,500 Food + ₹500 Household, constrained to sum).
  Still the most accurate option, and still rejected for the same reason 0007 rejected it:
  it puts split machinery in front of a capture flow whose whole point is three taps. It
  remains the natural upgrade if the overlap ever actually bites.
- **Many labels, and forbid overlap in the breakdown** by counting each transaction only
  under its first label. Rejected: it makes the chart depend on the order labels happened to
  be attached in, which is arbitrary and invisible.
- **Many labels, count in full under each, and report the total separately.** Chosen.

## Consequences

- **`Σ spend_by_label(filter) == total_spend(filter)` no longer holds**, and the property
  test asserting it (NFR-3.9) is gone. It is replaced by two weaker facts that are still
  worth enforcing: no single label's figure ever exceeds the total, and the total equals the
  sum over *transactions*, counted once each.
- The breakdown query returns `SpendBreakdown(ByLabel, TotalMinor)`. **`TotalMinor` is
  computed from the transactions, never by summing `ByLabel`.** This is the whole mitigation,
  and it is a structural one: there is no code path that can sum the buckets by accident,
  because the total arrives already computed.
- The chart scales bars to the **largest label**, not to the total, and says in plain words
  that a transaction with two labels counts under both. A user who adds the bars up and gets
  more than they spent is told why on the same screen rather than filing a bug.
- **Unlabelled is now a real state**, not a label. There is no *Uncategorised* row to delete
  by accident, quick capture invents nothing, and the breakdown carries an explicit
  unlabelled bucket (`LabelId == null`) so that money is visible rather than missing.
- Filtering by labels means **any of them**, not all. "Anything to do with Food or Travel"
  is the question people ask.
- Label names are unique case-insensitively across the whole list, enforced by a partial
  unique index on live rows. With no parent to disambiguate them, two *Travel* labels are
  indistinguishable everywhere they appear.
- Deleting a label no longer needs a replacement label. It detaches from its transactions
  and they become unlabelled, which is a legal state now.
- **The migration is the risk.** `20260901045203_MultipleFlatLabels` moves every existing
  `Transactions.LabelId` into the join table before dropping the column, discards only the
  system *Uncategorised* label, de-duplicates names that the old parent scoping had allowed,
  and drops the Tag tables. Tag assignments are **not** migrated into labels: a tag was
  explicitly documented as not affecting arithmetic, and silently promoting it would change
  every historical chart.
- Self Transfers still carry no label and are still excluded from every spend and income
  aggregate. That invariant is untouched.
