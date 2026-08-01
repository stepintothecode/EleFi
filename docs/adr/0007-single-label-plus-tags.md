---
status: accepted
---

# Exactly one Label per transaction, plus unlimited Tags

A transaction carries exactly one Label (from a two-level hierarchy) and any number of
Tags. All money arithmetic uses the Label; **no total is ever computed from a Tag**. We
chose this because multi-label categorisation double-counts — a ₹2,000 shop tagged both
*Food* and *Household* contributes ₹2,000 to each, so the category chart sums to ₹4,000
against ₹2,000 of real money, and the dashboard ends up having to explain why its own
numbers don't add up.

## Considered options

- **Many labels per transaction**, accepting overlap. Closest to the original brief's
  wording. Rejected: category totals that don't reconcile to real spend undermine the
  dashboard's entire purpose.
- **Many labels with amount splits** (₹1,500 Food + ₹500 Household, constrained to sum).
  Accurate and expressive. Rejected because it reintroduces split machinery and a heavier
  entry UI, slowing the fast-capture flow that v1 exists to deliver.
- **Flat single label.** Simplest, but no roll-up of "all Food spending" across children.

## Consequences

- `Σ spend_by_label(filter) == total_spend(filter)` holds exactly, and is enforced by a
  property test (NFR-3.9).
- Tags provide the flexibility multi-labelling would have, without touching arithmetic —
  which is exactly why they're safe to allow without limit.
- Labels declare a side (Expense / Income / Both), so the capture picker shows a short
  relevant list rather than everything.
- Self Transfers carry no label, keeping transfers out of every spending chart.
- **The cost:** a genuinely mixed purchase must be assigned one label or entered as two
  transactions. Revisit only if real usage proves this painful.
