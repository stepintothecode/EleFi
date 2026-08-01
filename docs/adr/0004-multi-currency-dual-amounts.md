---
status: accepted
---

# Multi-currency: each end of a transaction carries its own amount and currency

A transaction stores `source_amount`/`source_currency` and
`destination_amount`/`destination_currency`, constrained equal when the currencies match.
We chose this because a cross-currency movement genuinely has two amounts — ₹8,300 leaves
and $100 arrives — and both are facts the bank asserted, so any single-amount model must
invent one of them.

## Considered options

- **Single amount plus an FX rate**, deriving the other side. Rejected: the derived figure
  won't match what the bank actually credited once markup, fees, and their rounding are
  applied, so the foreign balance drifts from reality by small amounts that accumulate.
- **Single-currency transactions with an FX clearing container.** Defensible, and the
  residual is literally the FX cost — but it needs a synthetic container in the UI and
  turns one real-world event into two rows.

## Consequences

- Single-currency transactions are unaffected: the CHECK constraint makes them behave
  exactly as a one-amount model would.
- A transaction's **implied rate** (source ÷ destination) is authoritative for that
  transaction, beating any market rate because it includes the bank's markup.
- Market rates are needed only to value *balances* and to convert *reports* into the home
  currency — kept in a local `FxRate` table with carry-forward for weekends and holidays.
- **A purely-INR user never triggers a rate fetch or a network call.** Multi-currency
  costs them nothing.
- Reports must always convert at the rate on each transaction's own date, never today's,
  or a past month's report would silently change every day.
