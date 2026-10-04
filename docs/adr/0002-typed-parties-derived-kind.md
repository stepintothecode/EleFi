---
status: accepted
---

# Transactions have typed source and destination parties; kind is derived

A transaction stores a **source party** and a **destination party**, each of which is
either an internal Money Container or an external person/merchant. Credit, Debit, and
Self Transfer are **derived** from that pair rather than stored. We chose this because
the original "To" and "From" columns meant a container in some rows and a person's name
in others - which makes every balance query a guess - and because deriving the kind makes
a transaction whose type disagrees with its data literally unrepresentable.

## Considered options

- **Literal spreadsheet columns** (`type`, `to_text`, `from_text`, `account_id`).
  Rejected: balances would require string-matching account names, and Self Transfers need
  special-casing in every query.
- **Full double-entry ledger** (header + balancing postings summing to zero). Genuinely
  correct and handles splits natively. Rejected as disproportionate for one person: it
  adds machinery to every feature to solve problems this app does not have.
- **Two linked rows per transfer**. Rejected: every future `SUM` would need to remember to
  de-duplicate transfer pairs, and one forgotten `WHERE` inflates reported spending.

## Consequences

- Source and destination draw from **one party pool**, so shared autocomplete across both
  fields falls out of the model rather than being built.
- External → External is rejected by the schema: it isn't the user's money.
- **Split transactions are not possible.** Adding them later means either a postings table
  or per-label allocations - a real migration. Accepted deliberately; see
  [ADR-0013](0013-multiple-flat-labels.md), which took the cheaper route of allowing several
  labels on one transaction and reporting the overlap honestly, and which supersedes
  [ADR-0007](0007-single-label-plus-tags.md).
- Reading data requires joining through `Party` to reach a container, which is slightly
  less direct than a `container_id` column would be.
