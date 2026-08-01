---
status: accepted
---

# Balances are always derived, never stored

A container's balance is computed on every read as `opening_balance + inflows − outflows`.
No `current_balance` column exists anywhere, at any layer, including caches. We chose this
because back-dated entries, edits, and deletes are routine in a manual tracker, and each
one would require a stored balance to rewind and replay correctly — where any bug leaves
a silently wrong number that the user won't notice until they reconcile against a real
bank statement.

## Considered options

- **Stored running balance.** Rejected: fast reads bought with a permanent class of
  corruption bug, in the one area where being wrong is unacceptable.
- **Derived plus monthly snapshot rollups.** Rejected as premature — real complexity
  (snapshot invalidation on back-dated entries) bought to solve a performance problem
  that does not exist at this data volume.

## Consequences

- Back-dating, editing, and deleting are automatically correct with no extra code. This
  is the main benefit and it compounds across every feature.
- Aggregates must stay indexed. A heavy decade is ~40,000 rows, where SQLite sums in
  single-digit milliseconds, so there is ample headroom — but the benchmarks in NFR-1
  run against 50,000 rows to keep it honest.
- If a future feature genuinely needs cached balances, caching is additive and can be
  invalidated correctly precisely *because* the true value is always computable.
