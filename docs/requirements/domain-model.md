# EleFi - Domain Model

Entities, relationships, invariants, and state transitions. Vocabulary throughout is
from [CONTEXT.md](../CONTEXT.md).

This document describes **what is true about the data**, independent of storage
technology. Schema notation is SQLite-flavoured because that is the target (reached
through EF Core, see [ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md)), but the
invariants are the point - they must hold whatever the implementation.

---

## 1. Conventions applying to every entity

| Rule | Reason |
|---|---|
| **Primary key is a client-generated UUIDv7** | Makes offline creation and retry idempotent. UUIDv7 is time-ordered, so it indexes like an integer instead of scattering B-tree writes. |
| **`user_id` on every row** | Multi-tenant-safe from day one (Q1). Single user today; not a migration later. |
| **`created_at`, `updated_at`** - UTC epoch ms | `updated_at` is the merge key for last-write-wins sync in v2. |
| **`deleted_at`** - nullable, UTC epoch ms | Soft delete everywhere. Doubles as the sync tombstone. Retrofitting tombstones after data exists is painful; adding the column now is free. |
| **Money is an integer count of minor units** | See §2. |
| **Dates that mean "a calendar day"** are `TEXT` in `YYYY-MM-DD` | A transaction happened on a *date*, not at an instant. Storing it as a timestamp introduces a timezone bug the day the user crosses one. |
| **Timestamps that mean "an instant"** are integer epoch ms, UTC | Unambiguous, sortable, timezone-safe. |

**Every query filters `deleted_at IS NULL` and `user_id = :me`.** This is enforced in one
place in the repository layer, never remembered per query - see
[software-development.md](software-development.md) §4.

---

## 2. Money representation

Money is stored as an **integer count of minor units** (paise for INR, cents for USD)
alongside an ISO-4217 currency code.

```
amount_minor   INTEGER   -- 45050  means  ₹450.50
currency       TEXT      -- 'INR'
```

**Why integers rather than a decimal type:** SQLite has no exact decimal type. Its
`NUMERIC` affinity silently falls back to an 8-byte IEEE-754 float, and JavaScript has no
decimal type at all. Integer minor units are exact in both, and `Number.MAX_SAFE_INTEGER`
covers ₹90,071,992,547,409 - sufficient.

**Invariants**

- `M1` - No floating-point value is ever used to hold, transport, or compute money.
- `M2` - Currency code is ISO-4217 uppercase, three characters.
- `M3` - Minor-unit exponent is looked up per currency (INR/USD = 2, JPY = 0). Never
  assumed to be 2.
- `M4` - Amounts are non-negative. Direction is expressed by which end of the transaction
  the party sits on, never by a negative number. There is exactly one way to represent
  "money left this container", which means there is exactly one thing to get right.

---

## 3. Container

A Money Container. See [CONTEXT.md](../CONTEXT.md#money-container).

```
Container
  id                     TEXT PK        uuidv7
  user_id                TEXT NOT NULL
  name                   TEXT NOT NULL        "HDFC Savings"
  kind                   TEXT NOT NULL        see ContainerKind
  currency               TEXT NOT NULL
  opening_balance_minor  INTEGER NOT NULL DEFAULT 0
  opening_balance_as_of  TEXT NOT NULL        YYYY-MM-DD
  is_archived            INTEGER NOT NULL DEFAULT 0
  sort_order             INTEGER
  colour, icon           TEXT

  -- bank-ish kinds
  institution_name       TEXT                 "HDFC Bank"
  account_number_last4   TEXT                 '4417' - LAST FOUR ONLY
  ifsc                   TEXT

  -- CreditCard
  credit_limit_minor     INTEGER
  statement_day          INTEGER  1..31
  payment_due_day        INTEGER  1..31

  -- FixedDeposit / RecurringDeposit / PPF
  interest_rate_bps      INTEGER              750 = 7.50%
  maturity_date          TEXT   YYYY-MM-DD
  tenure_months          INTEGER
  installment_minor      INTEGER              RD monthly installment

  created_at, updated_at, deleted_at
```

### ContainerKind

| Kind | Nature | Liquidity | Notes |
|---|---|---|---|
| `BankAccount` | Asset | Liquid | Savings or current |
| `CreditCard` | **Liability** | Liquid | Balance = amount owed |
| `Cash` | Asset | Liquid | Physical cash on hand |
| `Wallet` | Asset | Liquid | Paytm balance, piggy bank, any other holder |
| `FixedDeposit` | Asset | **Locked** | |
| `RecurringDeposit` | Asset | **Locked** | |
| `PPF` | Asset | **Locked** | |

`is_liability` and `is_liquid` are **derived from kind**, not stored. Storing them creates
the possibility of a credit card marked as an asset, and there is no reason to permit
that state to exist.

### Invariants

- `C1` - `account_number_last4` is at most 4 characters. **The full account number is
  never stored, transmitted, or logged.** It cannot help the user and it is the single
  most damaging field in a breach.
- `C2` - `credit_limit_minor`, `statement_day`, `payment_due_day` are non-null only when
  `kind = 'CreditCard'`.
- `C3` - `maturity_date`, `interest_rate_bps`, `tenure_months` are meaningful only for
  `FixedDeposit`, `RecurringDeposit`, `PPF`.
- `C4` - A container with transactions cannot be hard-deleted, only archived. An archived
  container is hidden from pickers but still contributes to historical reports.
- `C5` - A container's `currency` is immutable once it has transactions. Changing it would
  silently reinterpret every historical amount.

### Credit card semantics - the trap, stated explicitly

Spending ₹2,000 on a credit card is a **Debit** with the card as Source. It:

- increases the amount owed on the card,
- does **not** reduce any bank balance,
- reduces net worth by ₹2,000 immediately.

Paying the card bill is a **Self Transfer** from the bank container to the card container.
It:

- reduces the bank balance,
- reduces the amount owed,
- leaves net worth **unchanged** - one asset down, one liability down.

`INV-CC` - **A credit card bill payment must never appear as spending in any report.**
It is a Self Transfer, and Self Transfers are excluded from spend-by-label by
construction. Getting this wrong double-counts every rupee of card spending.

### Interest and accrual - explicitly out of scope

FD/RD/PPF interest is **not** accrued automatically. `interest_rate_bps` and
`maturity_date` are informational - used to show "matures in 3 months", never to
synthesise money. Interest is recorded as a normal Credit when it actually lands.

Rationale: real accrual needs per-instrument compounding rules, TDS handling, and PPF's
own calendar. It is a product in itself and would displace the capture experience that
v1 exists to deliver.

---

## 4. Party

Either end of a transaction. Exactly one of Internal (a Container) or External.

```
Party
  id             TEXT PK
  user_id        TEXT NOT NULL
  kind           TEXT NOT NULL     'Container' | 'External'
  container_id   TEXT NULL   FK -> Container.id     -- iff kind='Container'
  name           TEXT NULL                          -- iff kind='External'
  usage_count    INTEGER NOT NULL DEFAULT 0
  last_used_at   INTEGER
  created_at, updated_at, deleted_at
```

### Invariants

- `P1` - `kind='Container'` ⟹ `container_id` NOT NULL **and** `name` NULL.
- `P2` - `kind='External'` ⟹ `name` NOT NULL **and** `container_id` NULL.
- `P3` - Exactly one Party row exists per Container, created and archived with it.
- `P4` - External party names are unique per user, case-insensitively and after trimming.
  "Rahul", "rahul ", and "RAHUL" are one party, so the suggestion list does not fragment.

### Why parties are one table

Because Source and Destination are the same *role* whether the far end is a shop or the
user's own wallet, a single table means **one suggestion pool feeding both fields** - the
behaviour requested in point 5 of the original brief falls out of the model rather than
being special-cased.

`usage_count` and `last_used_at` exist solely to rank suggestions. Neither ever
participates in a monetary calculation.

---

## 5. Transaction

The central entity.

```
Transaction
  id                        TEXT PK       uuidv7, client-generated
  user_id                   TEXT NOT NULL

  source_party_id           TEXT NOT NULL  FK -> Party.id
  source_amount_minor       INTEGER NOT NULL
  source_currency           TEXT NOT NULL

  destination_party_id      TEXT NOT NULL  FK -> Party.id
  destination_amount_minor  INTEGER NOT NULL
  destination_currency      TEXT NOT NULL

  occurred_on               TEXT NOT NULL     YYYY-MM-DD
  description               TEXT
  label_id                  TEXT   FK -> Label.id
  marketplace_app_id        TEXT   FK -> App.id
  payment_app_id            TEXT   FK -> App.id
  goal_id                   TEXT   FK -> Goal.id

  needs_review              INTEGER NOT NULL DEFAULT 0
  capture_source            TEXT NOT NULL     see CaptureSource
  status                    TEXT NOT NULL DEFAULT 'Cleared'   -- reserved, see below

  created_at, updated_at, deleted_at
```

### Derived: Transaction Kind

Never stored. Computed from the two parties' kinds:

```
kind(t) =
  source.kind='Container' AND destination.kind='External'  -> 'Debit'
  source.kind='External'  AND destination.kind='Container' -> 'Credit'
  source.kind='Container' AND destination.kind='Container' -> 'SelfTransfer'
  otherwise                                                -> INVALID
```

Because kind is derived, a transaction whose kind disagrees with its parties is not
merely discouraged - it is **unrepresentable**. That is the whole point of ADR-0002.

### Invariants

- `T1` - At least one party is Internal. External→External is rejected: it is not the
  user's money and recording it would corrupt every report.
- `T2` - `source_party_id ≠ destination_party_id`. Money cannot move to where it already is.
- `T3` - `source_amount_minor > 0` **and** `destination_amount_minor > 0`. Zero-amount
  transactions are meaningless; negative amounts are prohibited by `M4`.
- `T4` - `source_currency = destination_currency` ⟹ `source_amount_minor =
  destination_amount_minor`. Enforced by a CHECK constraint, so a same-currency
  transaction cannot lose or invent money.
- `T5` - `label_id` is required when kind is Debit or Credit; optional when Self Transfer.
- `T6` - `label.applies_to` must admit the transaction's kind (an Income label cannot be
  used on a Debit).
- `T7` - `occurred_on` may be in the past freely. Future dates are rejected in v1 -
  future-dated transactions are a scheduling feature, and letting them into balances
  silently would make today's net worth wrong.
- `T8` - `goal_id` is settable only when the goal's `funding_mode = 'TracksContributions'`.
- `T9` - Deleting a transaction sets `deleted_at`. It is never removed. It immediately
  leaves every balance, report, and export.

### CaptureSource

`App` · `Widget` · `Notification` · `Bubble` · `QuickTile` · `ShareSheet` · `Sms` ·
`Import` · `Restore`

`Sms` means the transaction was created by the user confirming a Capture Suggestion
(§12). It never means the app created it by itself - see `SM2`.

Recorded so the audit trail can say *"created via widget"*, and so it is possible to ask
"are widget-captured transactions more often wrong?" - the question that tells you
whether the defaulting logic is any good.

### Status - reserved, not implemented in v1

`status` ships with a single value, `Cleared`, and nothing reads it. It exists so that
adding `Pending` (an uncleared cheque, an unsettled card charge) and `Scheduled`
(recurring rent) in v2 is a value addition rather than a schema migration across a table
of real data.

When those arrive, balance splits into **cleared balance** and **projected balance**.
That split touches every query, which is precisely why it is not in v1.

---

## 6. Label

```
Label
  id           TEXT PK
  user_id      TEXT NOT NULL
  name         TEXT NOT NULL
  parent_id    TEXT NULL  FK -> Label.id
  applies_to   TEXT NOT NULL    'Expense' | 'Income' | 'Both'
  icon, colour TEXT
  sort_order   INTEGER
  created_at, updated_at, deleted_at
```

- `L1` - Maximum depth 2. A label with a `parent_id` may not itself be a parent.
  Deeper hierarchies are a navigation cost users pay on every single capture, for
  structure they use once a year.
- `L2` - Name unique per user per parent.
- `L3` - A parent label selected in a filter or report **includes its children**.
- `L4` - Deleting a label in use reassigns its transactions to a designated replacement,
  or to the seeded `Uncategorised`. A transaction is never orphaned.
- `L5` - `Uncategorised` is seeded, `applies_to='Both'`, and cannot be deleted. Quick
  Capture depends on it existing.

**Exactly one label per transaction** is what makes spend-by-label reconcile exactly to
total spend. See [ADR-0007](../adr/0007-single-label-plus-tags.md).

---

## 7. Tag

```
Tag                  TransactionTag
  id        TEXT PK    transaction_id  FK
  user_id   TEXT       tag_id          FK
  name      TEXT       PRIMARY KEY (transaction_id, tag_id)
  colour    TEXT
```

- `G1` - Name unique per user, case-insensitive, trimmed.
- `G2` - **No total is ever computed by tag.** Tags filter and group; they never sum.
  This is the rule that lets a transaction carry many tags without breaking arithmetic.

---

## 8. App

```
App
  id                    TEXT PK
  user_id               TEXT NOT NULL
  name                  TEXT NOT NULL     "Zomato", "GPay", "Cash"
  default_container_id  TEXT NULL  FK -> Container.id
  icon, colour          TEXT
  usage_count           INTEGER NOT NULL DEFAULT 0
  last_used_at          INTEGER
  created_at, updated_at, deleted_at
```

- `A1` - Name unique per user, case-insensitive, trimmed.
- `A2` - One pool serves both the marketplace and payment roles. No role field: "Cash" is
  legitimately both, and forcing a choice at creation time creates duplicates.
- `A3` - **An App never participates in a balance calculation.** A wrong or missing App is
  a cosmetic error. This is the entire justification for keeping it separate from
  Container.
- `A4` - Auto-created on first use, so capture never stalls to "create the app first".

---

## 9. Goal

```
Goal
  id                          TEXT PK
  user_id                     TEXT NOT NULL
  name                        TEXT NOT NULL
  target_amount_minor         INTEGER NOT NULL
  currency                    TEXT NOT NULL
  start_date                  TEXT NOT NULL   YYYY-MM-DD
  target_date                 TEXT NOT NULL   YYYY-MM-DD
  funding_mode                TEXT NOT NULL   'TracksContainerBalance'
                                            | 'TracksContributions'
  monthly_contribution_minor  INTEGER NULL
  opening_allocation_minor    INTEGER NOT NULL DEFAULT 0
  is_archived                 INTEGER NOT NULL DEFAULT 0
  created_at, updated_at, deleted_at

GoalContainer
  goal_id, container_id        PRIMARY KEY (goal_id, container_id)
```

### Progress

```
TracksContainerBalance:
  progress = Σ balance(c) for c in goal.containers, converted to goal.currency

TracksContributions:
  progress = opening_allocation
           + Σ destination_amount of transactions where goal_id = goal.id
           - Σ source_amount      of transactions where goal_id = goal.id
```

### Status

```
elapsed_fraction = (today - start_date) / (target_date - start_date), clamped 0..1
expected         = target_amount × elapsed_fraction
status           = progress >= target_amount        -> 'Achieved'
                 | progress >= expected × 0.95      -> 'OnTrack'
                 | otherwise                        -> 'Behind'
required_monthly = (target_amount - progress) / months_remaining
```

### Invariants

- `GL1` - `target_date > start_date`.
- `GL2` - `target_amount_minor > 0`.
- `GL3` - **A goal never contributes to net worth.** Its progress is a *view over* money
  already counted in a container. Any code path where goal progress adds to net worth is
  a bug, not a feature.
- `GL4` - `funding_mode='TracksContainerBalance'` requires at least one linked container.
- `GL5` - When several `TracksContributions` goals draw on the same container and their
  combined allocations exceed that container's balance, the UI **warns** - it does not
  block. Over-allocation is a real thing users do knowingly; silently hiding it is what
  produces a nasty surprise in 2028.
- `GL6` - Deleting a goal nulls `goal_id` on its transactions. The transactions themselves
  are untouched - the money was real regardless of the goal.

---

## 10. FxRate

```
FxRate
  rate_date    TEXT NOT NULL     YYYY-MM-DD
  base_ccy     TEXT NOT NULL
  quote_ccy    TEXT NOT NULL
  rate         TEXT NOT NULL     decimal as string, 8 dp
  source       TEXT NOT NULL     'Provider' | 'Manual' | 'Implied'
  fetched_at   INTEGER
  PRIMARY KEY (rate_date, base_ccy, quote_ccy)
```

### Valuation rules - the order matters

1. **A cross-currency transaction is valued by its own two amounts.** Its implied rate
   already includes the bank's markup, so it is more accurate than any market rate.
2. **Balances and net worth** use the most recent available rate.
3. **Historical reports** use the rate on each transaction's own `occurred_on`. Using
   today's rate would make March's report change every day, so it could never be
   reconciled against anything.
4. **Carry forward** for dates with no rate (weekends, holidays). Never interpolate,
   never leave a hole.

- `FX1` - `rate` is stored as a decimal **string**, parsed with a decimal library. It is
  the one place a float would be most tempting and most wrong.
- `FX2` - Rates are cached indefinitely; a fetched rate is never re-fetched or overwritten
  by a later provider correction.
- `FX3` - With `home_currency = INR` and no foreign containers, **no rate is ever fetched
  and no network call is made.** Multi-currency support costs a purely-INR user nothing.

---

## 11. AuditEvent

```
AuditEvent
  id            TEXT PK
  user_id       TEXT NOT NULL
  entity_type   TEXT NOT NULL    'Transaction' | 'Container' | 'Goal' | 'Label' | ...
  entity_id     TEXT NOT NULL
  action        TEXT NOT NULL    'Created'|'Updated'|'Deleted'|'Restored'|'Reviewed'
  changes       TEXT             JSON [{field, from, to}]
  capture_source TEXT
  occurred_at   INTEGER NOT NULL
```

- `AU1` - Append-only. Audit events are never updated or deleted, including when their
  subject is deleted.
- `AU2` - Written by **SQLite triggers**, not application code, so no code path can bypass
  the trail - including a future bulk import or a restore.
- `AU3` - `changes` records only fields that actually differed.
- `AU4` - Retained for at least 24 months, then compactable. Growth is bounded by the fact
  that a human can only enter so many transactions.

**Note:** the stack is C# again ([ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md)),
so an EF Core `SaveChanges` interceptor is available. Triggers are kept anyway. An
interceptor sees only writes that go through the `DbContext`, and this app has code paths
that will not: a restore that swaps the database file, a seeded performance dataset, a
future bulk import, and anything written from the Android SMS service outside the normal
scope. A trail with holes in exactly those places is worse than no trail, because it looks
complete. See [ADR-0008](../adr/0008-audit-via-sqlite-triggers.md).

---

## 12. CaptureSuggestion and ParseRule

The SMS-assisted capture path. Vocabulary in
[CONTEXT.md](../CONTEXT.md#alerts-and-suggestions); the decision and its risks in
[ADR-0010](../adr/0010-on-device-sms-assisted-capture.md).

**Read `SM2` before reading anything else in this section.** Everything here is shaped by
it.

### ParseRule

Ships as bundled data, editable by the user. Not code.

```
ParseRule
  id                     TEXT PK
  user_id                TEXT NOT NULL
  name                   TEXT NOT NULL        "HDFC card debit"
  sender_pattern         TEXT NOT NULL        '^[A-Z]{2}-HDFCBK$'  (regex, anchored)
  body_pattern           TEXT NOT NULL        regex with named groups
  direction              TEXT NOT NULL        'Debit' | 'Credit'
  is_enabled             INTEGER NOT NULL DEFAULT 1
  is_builtin             INTEGER NOT NULL DEFAULT 0
  priority               INTEGER NOT NULL DEFAULT 0
  created_at, updated_at, deleted_at
```

`body_pattern` names its captures, and only these are recognised:

| Group | Meaning | Required |
|---|---|---|
| `amount` | Decimal as written in the message | yes |
| `currency` | ISO code or symbol; defaults to home currency | no |
| `last4` | Last four digits of the account or card | no |
| `counterparty` | Merchant or payer text | no |
| `occurred_on` | Date as written | no |

Anything a rule does not capture is left empty on the suggestion. **A rule never
substitutes a guess for a missing capture.**

### CaptureSuggestion

```
CaptureSuggestion
  id                     TEXT PK       uuidv7
  user_id                TEXT NOT NULL
  parse_rule_id          TEXT NOT NULL  FK -> ParseRule.id
  fingerprint            TEXT NOT NULL  hash(sender, sent_at, amount_minor, last4)

  amount_minor           INTEGER NOT NULL
  currency               TEXT NOT NULL
  direction              TEXT NOT NULL     'Debit' | 'Credit'
  container_id           TEXT NULL  FK -> Container.id   -- matched via last4
  counterparty_text      TEXT NULL                       -- raw, unresolved
  occurred_on            TEXT NULL         YYYY-MM-DD

  state                  TEXT NOT NULL     'Pending'|'Confirmed'|'Dismissed'|'Expired'
  transaction_id         TEXT NULL  FK -> Transaction.id  -- iff state='Confirmed'
  created_at, expires_at
```

There is deliberately **no `body` column, and no `sender` column**. The message text is
never written to disk. `fingerprint` is a one-way hash kept only to suppress duplicates,
and the sender is folded into it rather than stored beside it.

### State transitions

```
                 (rule matches)
  Transaction Alert ──────────────► Pending ──── user confirms ───► Confirmed
                                       │                              │
                                       ├──── user dismisses ────► Dismissed  (row deleted)
                                       └──── expires_at passes ──► Expired    (row deleted)
```

`Confirmed` is the only state that outlives the suggestion, and it survives only as a
`transaction_id` pointer for duplicate suppression. Everything else is removed.

### Invariants

- `SM1` - **An SMS body is never persisted, never logged, never audited, never backed up,
  and never exported.** It exists as a string in memory inside the receiver and is
  unreachable after the suggestion is built. This is the strictest rule in the document
  and it has no exceptions.
- `SM2` - **A CaptureSuggestion is not a Transaction and never becomes one without an
  explicit user confirmation.** It contributes to no balance, no aggregate, no report, no
  export, and no backup. The app does not book money because a bank sent a text message.
- `SM3` - Only messages whose sender matches an **enabled** rule's `sender_pattern` are
  parsed. Every other message is discarded inside the receiver, before the body is
  examined. There is no code path that reads an unmatched message's body.
- `SM4` - OTP, verification, and one-time-passcode messages are never parsed, never
  stored, and never surfaced, including when they arrive from a matching sender. Builtin
  rules must not match them, and this is covered by a test with real OTP formats.
- `SM5` - Suggestions are **hard-deleted** on dismiss and on expiry. This is a deliberate
  exception to soft delete: soft delete protects *user-entered* data, and a suggestion is
  machine-derived data the user has actively rejected. Keeping it would be retaining
  message-derived content the user said no to.
- `SM6` - `fingerprint` is unique among live suggestions per user, so a re-delivered or
  duplicated alert produces one suggestion, not two.
- `SM7` - A suggestion in state `Confirmed` cannot be confirmed again. Its
  `transaction_id` is the proof, and it is what stops a re-parse creating a second
  transaction for one real payment.
- `SM8` - Confirmation runs through the **same** transaction creation path as manual
  capture, so `T1`-`T9` apply unchanged. There is no privileged write.
- `SM9` - A confirmed transaction records `capture_source = 'Sms'` and the audit trail
  says the suggestion was confirmed, never that the SMS created it.
- `SM10` - With the SMS permission absent or revoked, **no functionality is lost** beyond
  suggestions themselves. Every affected screen degrades to manual capture.
- `SM11` - `container_id` is populated only by an exact `last4` match against a
  non-archived container. An ambiguous or absent match leaves it null and the user is
  asked. It is never inferred from the counterparty, the amount, or history.

### Why suggestions are not just `needs_review` transactions

It is tempting to reuse the existing Needs Review flag and have SMS create real
transactions directly. It would be less code.

It is rejected because the two flags mean different things. `needs_review` means *"you
recorded this quickly and should check the details"*. A suggestion means *"a machine
thinks this might have happened"*. Collapsing them puts unverified machine output into
balances, and the first time a promotional message parses as a ₹50,000 debit, every number
in the app is wrong and the user has no idea why. `SM2` is what prevents that class of
failure entirely rather than mitigating it.

---

## 13. Supporting entities

```
UserSettings
  user_id, home_currency, cycle_start_day (1..28, default 1),
  biometric_lock_enabled, backup_enabled, backup_frequency,
  theme, locale, number_format ('en-IN' -> lakh/crore grouping)

SavedFilter
  id, user_id, name, filter_json, sort_order

BackupRecord
  id, user_id, drive_file_id, size_bytes, transaction_count,
  schema_version, created_at, encrypted (bool)
```

`cycle_start_day` is capped at 28 so every month has one - there is no 30th of February,
and a cycle that silently shifts in short months is worse than one that starts on the 28th.

---

## 14. Derived calculations

```
balance(container) =
    opening_balance_minor
  + Σ destination_amount_minor  WHERE destination_party = container's party
  - Σ source_amount_minor       WHERE source_party      = container's party
  ... over transactions WHERE deleted_at IS NULL

net_worth =
    Σ balance(c) → home_currency  for c where NOT is_liability(c.kind)
  - Σ balance(c) → home_currency  for c where     is_liability(c.kind)

liquid = Σ balance(c) for asset c where is_liquid(c.kind)
locked = Σ balance(c) for asset c where NOT is_liquid(c.kind)
owed   = Σ balance(c) for liability c

spend_by_label(cycle) =
    Σ source_amount → home_currency
    GROUP BY label
    WHERE kind = 'Debit'                    -- Self Transfers excluded, always
      AND occurred_on within cycle
      AND deleted_at IS NULL
```

- `D1` - **Self Transfers never appear in spend or income figures.** Moving your own money
  is not spending. This single exclusion is what stops credit-card bill payments from
  double-counting.
- `D2` - Balances are always computed, never stored. See
  [ADR-0003](../adr/0003-derived-balances.md).
- `D3` - Every aggregate is expressed in the home currency, converted per §10.

### Export as a derived view

An export is not a separate query. It is the transaction list's own query, serialised.

```
export(filter, columns) = serialise(list(filter), columns)
```

- `X1` - **For every filter `f`, the rows in `export(f)` are exactly the rows the list
  shows under `f`, in the same order.** Not a subset, not a superset, not re-sorted. This
  is a property test, not a manual check (NFR-3.11).
- `X2` - The empty filter means everything, so an unfiltered export contains every
  transaction the user could see by scrolling.
- `X3` - Soft-deleted transactions are in neither, because the base query builder excludes
  them once for both (§1).
- `X4` - Export never re-derives an amount. What it writes is what `balance` and the list
  were computed from, so a CSV column and an on-screen figure cannot disagree.
- `X5` - CaptureSuggestions are not transactions, so they are never exported (`SM2`).

---

## 15. Entity relationships

```
UserSettings 1 ─── 1 User

Container 1 ─── 1 Party           (every container has exactly one party)
Container 1 ─── * App             (as default_container)
Container * ─── * Goal            (via GoalContainer)

Party 1 ─── * Transaction         (as source)
Party 1 ─── * Transaction         (as destination)

Label 1 ─── * Label               (parent → children, depth ≤ 2)
Label 1 ─── * Transaction

Tag * ─── * Transaction           (via TransactionTag)

App 1 ─── * Transaction           (as marketplace)
App 1 ─── * Transaction           (as payment)

Goal 1 ─── * Transaction          (contributions, TracksContributions only)

AuditEvent * ─── 1 (any entity)   (polymorphic by entity_type + entity_id)

ParseRule 1 ─── * CaptureSuggestion
CaptureSuggestion 0..1 ─ 1 Transaction   (only once confirmed; SM7)
Container 1 ─── * CaptureSuggestion      (matched by last4; nullable, SM11)
```

---

## 16. Deliberately absent

Named here so their absence is a decision on record rather than an oversight:

| Not modelled | Why | Revisit |
|---|---|---|
| Split transactions | One label per transaction keeps totals exact (ADR-0007) | If real usage demands it |
| Double-entry postings | Overkill for one person; ADR-0002 gives correctness more cheaply | Never expected |
| Budgets per label | Not in the brief; a substantial feature (limits, rollover, alerts) | v3 |
| Recurring transactions | Needs `status='Scheduled'` and projected balances | v2 |
| Interest accrual | Per-instrument compounding and TDS is a product in itself | v3+ |
| Attachments / receipts | Storage, sync size, and thumbnailing; blows up backup size | v2 |
| Shared / household accounts | Q1 chose single-user | If ever multi-user |
| Bank statement import | Per-bank formats; a maintenance treadmill | v3 |
| **Automatic** SMS transaction *booking* | An SMS is evidence, not authority. Parsing is in scope (§12); creating a transaction without confirmation is not, and `SM2` forbids it | Never |
| Reading SMS for anything but Parse Rules | OTPs, personal messages, and marketing are dropped at the receiver. There is no code path that stores them (`SM3`) | Never |
