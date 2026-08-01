# EleFund — Domain Glossary

The ubiquitous language for EleFund. Every term here has exactly one meaning, used
consistently in conversation, code, UI copy, and documentation.

This file is a **glossary and nothing else**. No schemas, no APIs, no file paths, no
implementation decisions. Those live in
[docs/requirements/domain-model.md](requirements/domain-model.md) and
[docs/adr/](adr/).

---

## Core money concepts

### Money Container
Anything that holds the user's money. A bank account, a credit card, a cash wallet, a
piggy bank, a fixed deposit, a recurring deposit, a PPF account.

"Container" is the canonical short form. **Never** call these "accounts" — that word is
reserved for the user's login identity and for the specific *Bank Account* container
kind. Saying "account" when you mean "container" is the single most common way this
model gets muddled.

Every container is exactly one of:

- an **Asset Container** — its balance is money the user has (bank account, cash, FD)
- a **Liability Container** — its balance is money the user owes (credit card)

Independently, every container is either:

- **Liquid** — spendable this week (bank account, cash, wallet)
- **Locked** — real wealth that cannot be spent on demand (FD, RD, PPF)

### Balance
What a container currently holds. Always **derived** from the container's opening
balance plus every transaction that touched it. Never a number the user types in
directly after setup, and never a number the system stores and maintains.

For a Liability Container, the balance is the amount **owed**.

### Party
Either end of a transaction — where money came from, or where it went. A party is
always exactly one of:

- an **Internal Party** — a Money Container the user owns
- an **External Party** — anyone or anything outside the user's money: a person
  ("Rahul"), a merchant ("Big Bazaar"), an employer, a landlord

The word "party" covers both because the two are interchangeable in the *role* they
play, which is why they share a single suggestion list during entry.

### Transaction
One movement of money, recorded once. It has a **Source Party**, a **Destination
Party**, an amount at each end, a date, and a label.

**Never** say "To" and "From" — those were the original sketch terms and they are
ambiguous, because they meant a container in some rows and a person in others. The
canonical terms are **Source** and **Destination**.

### Transaction Kind
What sort of movement a transaction is. **Derived** from its two parties, never chosen
or stored independently:

| Source | Destination | Kind |
|---|---|---|
| Internal | External | **Debit** — money left the user |
| External | Internal | **Credit** — money reached the user |
| Internal | Internal | **Self Transfer** — moved between the user's own containers |
| External | External | *not a transaction* — not the user's money |

Written as one word, capitalised: Debit, Credit, Self Transfer.

### Opening Balance
What a container held at the moment the user started tracking it, on a stated date.
The anchor every derived balance is calculated from. Not a transaction.

---

## Classification

### Label
The single category describing what a transaction was *for* — Food, Travel, Rent,
Salary. Exactly one per transaction, so category totals always reconcile to real money
spent.

Labels form a shallow hierarchy, at most two deep: *Food → Groceries*. A parent label
in a filter or report includes its children.

Each label declares which **side** it applies to — expense, income, or both — so the
picker shows only the handful that make sense for what is being entered.

A Self Transfer normally carries **no label**: moving money between your own containers
is not spending, and letting transfers into the category charts inflates every report.

### Tag
A free-form marker attached to a transaction for ad-hoc grouping — `goa-trip`,
`reimbursable`, `wedding`. Any number per transaction.

Tags deliberately carry **no arithmetic**. They filter and they group, but no total is
ever computed by tag, which is precisely why a transaction may have many of them
without breaking anything.

The distinction that matters: **one Label answers "what was this for", many Tags answer
"what else is this connected to".**

### App
A service, portal, or platform involved in a transaction — Zomato, Uber, Blinkit, GPay,
HDFC Net Banking, Cash, Rapido. One kind of thing, appearing in two roles:

- **Marketplace App** — the platform the user bought *through* (Zomato, Uber)
- **Payment App** — the rail the money moved *along* (GPay, Net Banking, Cash)

Both roles draw from one shared pool, so a name typed in either place is suggested in
both afterwards.

An App never determines a balance. The Source Container is the sole truth about where
money left; the Payment App only describes *how*. A wrong or missing App can never make
a balance wrong.

An App may declare a **Default Container**, so choosing "GPay" pre-fills HDFC and
choosing "Cash" pre-fills the wallet.

---

## Capture

### Capture
The act of recording a transaction. Deliberately not called "entry" or "adding" —
capture emphasises speed and the fact that it often happens away from the app.

### Quick Capture
Recording a transaction in roughly three seconds, typically from outside the app — a
notification, a home-screen widget, a Quick Settings tile, or a floating bubble.

A Quick Capture produces a **complete, real Transaction**, not a draft. Missing fields
are filled from smart defaults, the transaction affects balances immediately, and it is
flagged **Needs Review**.

There is no "draft transaction" concept in EleFund, and there should never be one. A
guessed transaction that counts is better than a perfect one that doesn't exist.

### Capture Source
Where a transaction came from: the app itself, a widget, a notification, the bubble, the
share sheet, or an import. Recorded on every transaction and shown in its audit trail.

### Needs Review
A flag meaning "this transaction was created with guessed values and you haven't
confirmed them". It is a nudge, never a restriction — a Needs Review transaction is a
full transaction in every way, and counts toward every balance and every report.

---

## Goals

### Goal
A named savings target: an amount, a currency, a target date, and a way of measuring
progress. "₹3,00,000 for an engagement ring by August 2028."

### Funding Mode
How a goal's progress is measured. Chosen per goal, because the two real situations are
genuinely different:

- **Tracks Container Balance** — progress *is* the combined balance of the containers
  linked to it. Use when the money is dedicated (an RD opened only for this goal).
- **Tracks Contributions** — progress is the sum of transactions attributed to the goal,
  plus an opening allocation. Use when the money is mixed in with other money.

### Contribution
A transaction attributed to a goal, in Tracks Contributions mode.

### Allocation
Money earmarked for a goal. Critically, **an allocation never creates money**. ₹1 lakh
allocated to a goal is still ₹1 lakh in the container holding it, and still ₹1 lakh of
net worth. Goals are a *view over* money the user already has.

### Goal Status
**On Track**, **Behind**, or **Achieved**, computed by comparing actual progress against
what progress should be by today, given the start date, target date, and target amount.

---

## Money and currency

### Home Currency
The single currency every aggregate is reported in — net worth, spend by label, goal
progress. Set by the user, INR by default.

### FX Rate
The value of one currency in terms of another, on a stated date. EleFund keeps a
history, because a report about March must use March's rates. If it used today's, last
month's report would silently change every day.

### Implied Rate
The rate a cross-currency transaction implies from its own two amounts — ₹8,300 out and
$100 in implies 83.00. An implied rate always beats a market rate for that transaction,
because it already includes the bank's markup and fees.

### Carry Forward
The rule for dates with no published rate — weekends, holidays. The most recent earlier
rate is used. A deliberate rule, so reports never have holes.

---

## Reporting

### Cycle
The user's reporting period. Calendar month by default, but with a configurable start
day, so someone paid on the 25th can align their cycles to their salary rather than to
the calendar.

Distinct from a **Statement Cycle**, which belongs to a credit card and is dictated by
the bank.

### Net Worth
Total assets minus total liabilities, in the home currency. Always presented alongside
its three components — **Liquid**, **Locked**, and **Owed** — because the total alone
hides the situation that actually matters.

### Statement
A filtered view of every transaction touching one container over a date range,
regardless of which end of the transaction it sat on. The account-statement view, and
the most common thing anyone exports.

### Filter
A composable set of criteria selecting a subset of transactions: dates, kinds,
containers, labels, tags, parties, apps, amount range, text.

One filter concept serves both the transaction list and the export. **The export is the
filtered view, serialised** — there is no separate notion of "what to export".

### Saved Filter
A filter the user has named and kept, so a recurring question is one tap rather than six.

---

## History

### Audit Event
An immutable record of something that happened to a record: created, updated, deleted,
restored, reviewed. Captures which fields changed, from what to what, and via which
Capture Source.

### Timeline
The audit events for one record, shown in order — the JIRA-style history of a
transaction.

### Soft Delete
Deleting a transaction hides it from every view, every balance, and every export, but
retains it so the deletion is visible in the timeline and can be undone. Nothing the
user deletes is destroyed.

---

## Storage

### Local Database
The database on the user's device. **The single source of truth.** Not a cache, not a
mirror — the real thing. EleFund has no server that holds user data.

### Backup
An encrypted snapshot of the local database stored in the user's own Google Drive.
Protects against a lost, broken, or wiped device.

### Restore
Replacing the local database with a backup. Destructive to whatever is on the device
now, and always confirmed explicitly.

### Sync
Merging changes made on multiple devices so all of them end up with everything. Distinct
from Backup, and deliberately not built in v1 — see
[ADR-0006](adr/0006-google-drive-encrypted-backup.md).

### Recovery Code
The one-time code shown when encrypted backup is enabled, which together with the
passphrase is the only way to read a backup on a new device. If both are lost, the
backup is unrecoverable — by design, and stated plainly in the UI.
