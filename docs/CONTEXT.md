# EleFi - Domain Glossary

The ubiquitous language for EleFi. Every term here has exactly one meaning, used
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

"Container" is the canonical short form. **Never** call these "accounts" - that word is
reserved for the user's login identity and for the specific *Bank Account* container
kind. Saying "account" when you mean "container" is the single most common way this
model gets muddled.

**Cash and Wallet are not the same thing**, and the difference is worth stating because
they read as synonyms. Cash is physical notes and coins. A Wallet is a stored balance that
can only be spent in one place: a Paytm or PhonePe balance, a gift card, a piggy bank.
Merging them would turn "money I can hand over anywhere" and "money locked inside one app"
into a single number.

Every container is exactly one of:

- an **Asset Container** - its balance is money the user has (bank account, cash, FD)
- a **Liability Container** - its balance is money the user owes (credit card)

Independently, every container is either:

- **Liquid** - spendable this week (bank account, cash, wallet)
- **Locked** - real wealth that cannot be spent on demand (FD, RD, PPF)

### Balance
What a container currently holds. Always **derived** from the container's opening
balance plus every transaction that touched it. Never a number the user types in
directly after setup, and never a number the system stores and maintains.

For a Liability Container, the balance is the amount **owed**.

### Party
Either end of a transaction - where money came from, or where it went. A party is
always exactly one of:

- an **Internal Party** - a Money Container the user owns
- an **External Party** - anyone or anything outside the user's money: a person
  ("Rahul"), a merchant ("Big Bazaar"), an employer, a landlord

The word "party" covers both because the two are interchangeable in the *role* they
play, which is why they share a single suggestion list during entry.

### Transaction
One movement of money, recorded once. It has a **Source Party**, a **Destination
Party**, an amount at each end, a date, and any number of labels.

**Never** say "To" and "From" - those were the original sketch terms and they are
ambiguous, because they meant a container in some rows and a person in others. The
canonical terms are **Source** and **Destination**.

### Transaction Kind
What sort of movement a transaction is. **Derived** from its two parties, never chosen
or stored independently:

| Source | Destination | Kind |
|---|---|---|
| Internal | External | **Debit** - money left the user |
| External | Internal | **Credit** - money reached the user |
| Internal | Internal | **Self Transfer** - moved between the user's own containers |
| External | External | *not a transaction* - not the user's money |

Written as one word, capitalised: Debit, Credit, Self Transfer.

### Opening Balance
What a container held at the moment the user started tracking it, on a stated date.
The anchor every derived balance is calculated from. Not a transaction.

---

## Classification

### Label
A word describing what a transaction was *for* - Food, Travel, Rent, Salary. A
transaction carries **any number of them, including none**, the way an issue carries tags.
One flat list: no parent, no child, no expense/income side.

**Unlabelled** is a state, not a label. There is no *Uncategorised* row. A transaction
nobody has got round to labelling shows up in its own bucket in the breakdown, so the money
is visible rather than hidden behind a fake category.

Because labels overlap, the per-label figures can add up to more than was spent: a shop
labelled both Food and Household counts in full under each. The breakdown therefore always
carries its own **total**, counted once per transaction, and every screen showing the bars
says so. See [ADR-0013](adr/0013-multiple-flat-labels.md).

A Self Transfer carries **no label**: moving money between your own containers is not
spending, and letting transfers into the charts inflates every report.

There is no separate **Tag** concept. There was one; it did the same job as a label and
made the user learn which of the two counted toward totals.

### App
A service, portal, or platform involved in a transaction - Zomato, Uber, Blinkit, GPay,
HDFC Net Banking, Cash, Rapido. One kind of thing, appearing in two roles:

- **Marketplace App** - the platform the user bought *through* (Zomato, Uber)
- **Payment App** - the rail the money moved *along* (GPay, Net Banking, Cash)

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
The act of recording a transaction. Deliberately not called "entry" or "adding" -
capture emphasises speed and the fact that it often happens away from the app.

### Quick Capture
Recording a transaction in roughly three seconds, typically from outside the app - a
notification, a home-screen widget, a Quick Settings tile, or a floating bubble.

A Quick Capture produces a **complete, real Transaction**, not a draft. Missing fields
are filled from smart defaults, the transaction affects balances immediately, and it is
flagged **Needs Review**.

There is no "draft transaction" concept in EleFi, and there should never be one. A
guessed transaction that counts is better than a perfect one that doesn't exist.

A [Capture Suggestion](#capture-suggestion) is not a counter-example. The difference is
who acted: a Quick Capture is the user recording money through a smaller surface, so it
counts immediately. A suggestion is the app reading a bank's SMS with no user involved,
so it counts only once a human says yes.

### Capture Source
Where a transaction came from: the app itself, a widget, a notification, the bubble, the
share sheet, a confirmed Capture Suggestion (from an SMS, or from a Payment App notification
alone), or an import. Recorded on every transaction
and shown in its audit trail.

### Needs Review
A flag meaning "this transaction was created with guessed values and you haven't
confirmed them". It is a nudge, never a restriction - a Needs Review transaction is a
full transaction in every way, and counts toward every balance and every report.

---

---

## Alerts and suggestions

### Transaction Alert
An incoming message that appears to describe a real movement of money. It arrives on one
of two **Alert Channels**:

- an **SMS** from a bank, card issuer, or wallet - "Rs.450.00 debited from a/c XX4417 on
  29-Aug-26 to ZOMATO"
- a **Payment App Notification** from an allow-listed Payment App - "Paid ₹450 to Zomato"

An alert is **evidence that something happened**, not the record of it. It is read on the
device, matched against a Parse Rule, and then discarded. Only alerts from a recognised
**Sender ID** are ever looked at; everything else - OTPs, marketing, personal messages,
every other app's notifications - is dropped at the receiver without being read further.

### Payment App Notification
The notification a Payment App (GPay, PhonePe, Paytm, Amazon Pay, CRED) posts when a payment
completes. It knows who was paid and any note the user typed, which a bank SMS usually does
not; it does not know which account paid, which the SMS does. Read only for apps on a fixed
allow-list. See [ADR-0014](adr/0014-payment-app-notification-ingest.md).

### Sender ID
Who an alert came from. For an SMS, the alphanumeric originating address a bank sends from,
such as `VM-HDFCBK` or `AD-ICICIB`; India's TRAI header scheme makes these stable and
registered. For a Payment App Notification, the app's package name, which Android supplies
and an app cannot forge. Either way it is the first gate.

### Corroboration
Two alerts about the same payment, one from each Alert Channel, merged into one Capture
Suggestion: the bank's SMS supplies the Container, the Payment App supplies the payee's name,
the note, and the Payment App. Same amount, same direction, different channels, minutes
apart. Never two alerts from the same channel.

### Parse Rule
A named pattern that turns a Transaction Alert from a given Sender ID into a Capture
Suggestion. Rules are data, not code, and ship with the app so a new bank format is a
data change rather than a release.

A rule that does not match produces **nothing**. It never guesses a partial transaction:
a plausible wrong amount in a money app is worse than no suggestion at all.

### Capture Suggestion
A parsed, unconfirmed proposal derived from a Transaction Alert: an amount, a direction, a
likely Container, a likely counterparty, a date.

**A Capture Suggestion is not a Transaction.** It holds no balance, appears in no report,
is in no export, and is not backed up. It becomes a Transaction only when the user
confirms it, and confirming is what makes it real.

This is the distinction that keeps [Quick Capture](#quick-capture) honest. Quick Capture
is *the user acting* through a smaller surface, so it produces a complete Transaction
flagged Needs Review. A Capture Suggestion is *the app guessing* with no user action at
all, so it produces nothing until a human agrees. The app never books money on the
strength of a text message.

### Suggestion Prompt
The local notification raised for a Capture Suggestion - "₹450 to Zomato?" with Review and
Dismiss. The only way a suggestion is surfaced outside the app. Review opens the Suggestion
Inbox; there is no one-tap Add on the notification, because the Container and labels are
checked there first.

Dismissing destroys the suggestion. Unactioned suggestions expire and are destroyed too.
Nothing accumulates.

### Suggestion Inbox
The screen listing every pending Capture Suggestion, titled **To confirm** in the app, where
each one is checked, corrected, and added or dismissed. The dashboard shows how many are
waiting.

Deliberately not called "to review", which would blur it with
[Needs Review](#needs-review): a Needs Review item is a real transaction you recorded and
should check; an item in the inbox is not a transaction at all yet.

---

## Goals

### Goal
A named savings target: an amount, a currency, a target date, and a way of measuring
progress. "₹3,00,000 for an engagement ring by August 2028."

### Funding Mode
How a goal's progress is measured. Chosen per goal, because the two real situations are
genuinely different:

- **Tracks Container Balance** - progress *is* the combined balance of the containers
  linked to it. Use when the money is dedicated (an RD opened only for this goal).
- **Tracks Contributions** - progress is the sum of transactions attributed to the goal,
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
The single currency every aggregate is reported in - net worth, spend by label, goal
progress. Set by the user, INR by default.

### FX Rate
The value of one currency in terms of another, on a stated date. EleFi keeps a
history, because a report about March must use March's rates. If it used today's, last
month's report would silently change every day.

### Implied Rate
The rate a cross-currency transaction implies from its own two amounts - ₹8,300 out and
$100 in implies 83.00. An implied rate always beats a market rate for that transaction,
because it already includes the bank's markup and fees.

### Carry Forward
The rule for dates with no published rate - weekends, holidays. The most recent earlier
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
its three components - **Liquid**, **Locked**, and **Owed** - because the total alone
hides the situation that actually matters.

### Statement
A filtered view of every transaction touching one container over a date range,
regardless of which end of the transaction it sat on. The account-statement view, and
the most common thing anyone exports.

### Filter
A composable set of criteria selecting a subset of transactions: dates, kinds,
containers, labels, parties, apps, amount range, text. Several labels selected together
means **any of them**, and "unlabelled" is selectable on its own.

One filter concept serves both the transaction list and the export. **The export is the
filtered view, serialised** - there is no separate notion of "what to export".

### Export
Writing the currently filtered set of transactions out as a file the user keeps, normally
CSV.

An export takes **no configuration of its own** beyond which columns to include. It has no
date picker, no account picker, and no "export all" toggle, because the transaction list
already has all of those and the user has already used them. What is on screen is what
comes out; with no filters applied, everything comes out.

The rule is worth stating as a rule: **whatever the list is showing, the export contains,
row for row.** Any divergence between the two is a bug, not a setting.

### Saved Filter
A filter the user has named and kept, so a recurring question is one tap rather than six.

---

## History

### Audit Event
An immutable record of something that happened to a record: created, updated, deleted,
restored, reviewed. Captures which fields changed, from what to what, and via which
Capture Source.

### Timeline
The audit events for one record, shown in order - the JIRA-style history of a
transaction.

### Soft Delete
Deleting a transaction hides it from every view, every balance, and every export, but
retains it so the deletion is visible in the timeline and can be undone. Nothing the
user deletes is destroyed.

---

## Storage

### Local Database
The database on the user's device, encrypted at rest. **The single source of truth.** Not
a cache, not a mirror - the real thing. EleFi has no server that holds user data.

Encrypted at rest means the file is unreadable without a key held in the platform
keystore, so a pulled device, a stolen backup of the app's data directory, or a rooted
phone yields ciphertext rather than a readable ledger.

### Backup
An encrypted snapshot of the local database stored in the user's own Google Drive.
Protects against a lost, broken, or wiped device.

### Restore
Replacing the local database with a backup. Destructive to whatever is on the device
now, and always confirmed explicitly.

### Sync
Merging changes made on multiple devices so all of them end up with everything. Distinct
from Backup, and deliberately not built in v1 - see
[ADR-0006](adr/0006-google-drive-encrypted-backup.md).

### Recovery Code
The one-time code shown when encrypted backup is enabled, which together with the
passphrase is the only way to read a backup on a new device. If both are lost, the
backup is unrecoverable - by design, and stated plainly in the UI.
