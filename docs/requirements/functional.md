# EleFund — Functional Requirements

What the system does, from the user's perspective. Vocabulary from
[CONTEXT.md](../CONTEXT.md). Data rules in
[domain-model.md](domain-model.md).

**Requirement IDs are stable.** Tests, tickets, and commits reference them. Never
renumber; mark superseded requirements as such and add a new ID.

**Release column:** `1.0` · `1.1` · `1.2` · `2.0` · `3.0` — see
[roadmap.md](../roadmap.md).

---

## Problem statement

Kartikeya tracks money across many containers — several bank accounts, a credit card,
cash, a PPF, deposits — and currently has no single place that answers *"what do I
actually have, and where did it go?"*

Existing apps fail for three reasons:

1. **Capture is too slow.** By the time an app is open and a form is filled, the moment
   has passed. Transactions go unrecorded, the data develops holes, and once the data has
   holes it stops being worth maintaining. This is how every previous attempt died.
2. **They assume bank integration.** Indian account aggregator coverage is patchy,
   consent expires, and cash — a large share of real spending — is invisible to it.
3. **They model money too simply.** Credit cards get treated as wallets, transfers get
   counted as spending, and the resulting numbers can't be trusted.

## Solution

A local-first money tracker where **capture takes three seconds and works with the
network off**, the data model is exact enough that balances can be trusted, and the
user's financial data never leaves their device except as an encrypted backup in their
own Google Drive.

---

## FR-1 · Money Containers

| ID | Requirement | Release |
|---|---|---|
| FR-1.1 | Create a container: name, kind, currency, opening balance, opening-balance date | 1.0 |
| FR-1.2 | Support all seven kinds: BankAccount, CreditCard, Cash, Wallet, FixedDeposit, RecurringDeposit, PPF | 1.0 |
| FR-1.3 | Capture institution name, **last 4 digits only** of account number, and IFSC for bank kinds | 1.0 |
| FR-1.4 | Capture credit limit, statement day, and payment due day for CreditCard | 1.0 |
| FR-1.5 | Capture interest rate, maturity date, tenure, and installment for FD/RD/PPF | 1.0 |
| FR-1.6 | Show each container's current derived balance | 1.0 |
| FR-1.7 | Show a CreditCard balance as *amount owed*, visually distinct from asset balances | 1.0 |
| FR-1.8 | Show available credit (`limit − owed`) on a CreditCard | 1.0 |
| FR-1.9 | Edit any container field except currency once transactions exist | 1.0 |
| FR-1.10 | Archive a container; hidden from pickers, retained in history and reports | 1.0 |
| FR-1.11 | Reorder containers manually; assign colour and icon | 1.0 |
| FR-1.12 | Container detail view: balance, statement-style transaction list, quick actions | 1.0 |
| FR-1.13 | Surface maturity dates within 30 days on the dashboard | 1.1 |
| FR-1.14 | Surface credit card payment due dates within 7 days | 1.1 |
| FR-1.15 | Reconciliation: enter a real-world balance, see the difference, create a balancing adjustment | 1.2 |

**User stories**

1. As a user, I want to add my HDFC savings account with the balance it had on the day I started, so my tracked balance matches reality from day one.
2. As a user, I want my credit card to show what I owe rather than a positive balance, so I am never misled into thinking debt is money.
3. As a user, I want to add my PPF account, so my net worth reflects all my wealth even though I can't spend it this month.
4. As a user, I want to archive an account I've closed, so it stops cluttering my pickers while my historical reports stay accurate.
5. As a user, I want to see available credit on my card, so I know how much headroom I have before a large purchase.
6. As a user, I want to be warned that my FD matures in three weeks, so I can decide what to do with the money before it silently rolls over.
7. As a user, I want to correct a drift between my tracked balance and my real bank balance, so small missed cash spends don't accumulate into distrust of the whole app.

---

## FR-2 · Capture

The core feature. Everything else exists to make this worth doing.

| ID | Requirement | Release |
|---|---|---|
| FR-2.1 | Record a transaction with source party, destination party, amount, date, label | 1.0 |
| FR-2.2 | Derive and display the kind (Credit/Debit/SelfTransfer) from the chosen parties — never ask for it separately | 1.0 |
| FR-2.3 | Offer a kind-first shortcut (Debit / Credit / Transfer) that pre-filters the party pickers, without kind ever becoming a stored field | 1.0 |
| FR-2.4 | Default the date to today; allow any past date | 1.0 |
| FR-2.5 | Reject future dates with a clear explanation | 1.0 |
| FR-2.6 | Optional: description, marketplace app, payment app, tags | 1.0 |
| FR-2.7 | Autocomplete every party, label, app, and tag field from previously entered values, ranked by recency and frequency | 1.0 |
| FR-2.8 | Draw source and destination suggestions from **one shared party pool** | 1.0 |
| FR-2.9 | Draw marketplace and payment suggestions from **one shared app pool** | 1.0 |
| FR-2.10 | Auto-create a party, app, or tag on first use, with no separate creation step | 1.0 |
| FR-2.11 | Auto-fill the source container when a payment app has a default container | 1.0 |
| FR-2.12 | Numeric keypad focused on open; amount is the first and default-focused field | 1.0 |
| FR-2.13 | Complete a typical capture in **≤ 3 interactions** after opening the form | 1.0 |
| FR-2.14 | Save and immediately open a fresh form ("save and add another") | 1.0 |
| FR-2.15 | Duplicate an existing transaction as the basis for a new one | 1.0 |
| FR-2.16 | Capture works fully offline with no visible difference | 1.0 |
| FR-2.17 | Second currency fields appear only when source and destination currencies differ | 1.2 |
| FR-2.18 | Show the implied FX rate on a cross-currency transaction | 1.2 |
| FR-2.19 | Attribute a transaction to a goal during capture | 1.1 |
| FR-2.20 | Voice capture: "four fifty Zomato" parsed into a draft form | 3.0 |

**User stories**

8. As a user, I want to record a ₹40 chai in under five seconds, so recording it costs less than the chai.
9. As a user, I want the app to know that HDFC → Wallet is a transfer rather than asking me, so I make one less decision per entry.
10. As a user, I want typing "Rah" to suggest "Rahul" because I paid him last month, so I never type a name twice.
11. As a user, I want choosing "GPay" to fill in HDFC automatically, so the common case needs no thought.
12. As a user, I want to enter yesterday's cash spend, so forgetting for a day doesn't create a permanent hole.
13. As a user, I want capture to work in a basement with no signal, so the app never fails at the exact moment I need it.
14. As a user, I want to add five transactions in a row without the form closing, so catching up on a week's backlog isn't painful.
15. As a user, I want my ₹8,300 → $100 transfer recorded with both real amounts, so my USD balance matches my actual USD account.

---

## FR-3 · Quick Capture

| ID | Requirement | Release |
|---|---|---|
| FR-3.1 | Quick capture creates a **complete Transaction** using defaults, flagged `needs_review` — never a draft | 2.0 |
| FR-3.2 | Persistent Android notification with an inline-reply amount field | 2.0 |
| FR-3.3 | Notification action buttons for the three most-used labels | 2.0 |
| FR-3.4 | Android Quick Settings tile opening a minimal capture sheet | 2.0 |
| FR-3.5 | Android home-screen widget: amount field plus recent-label shortcuts | 2.0 |
| FR-3.6 | Widget shows current net worth and this-cycle spend | 2.0 |
| FR-3.7 | Defaults are learned: most-used container by time of day, most-used label by amount band | 2.0 |
| FR-3.8 | Every quick-captured transaction records its `capture_source` | 2.0 |
| FR-3.9 | Dashboard shows a persistent "N transactions need review" prompt | 1.0 |
| FR-3.10 | Review queue: swipe through pending items, confirm or correct in one gesture | 2.0 |
| FR-3.11 | Floating bubble over other apps, with a permission-request flow that explains why | 3.0 |
| FR-3.12 | Bubble degrades gracefully when the OS kills its service; never silently stops without telling the user | 3.0 |
| FR-3.13 | Share-sheet target accepting shared text (e.g. a payment confirmation) | 3.0 |

**User stories**

16. As a user, I want to type an amount into a notification without leaving WhatsApp, so recording costs no context switch.
17. As a user, I want a home-screen widget showing my net worth, so I'm reminded of my position without opening anything.
18. As a user, I want the app to learn that I mostly buy food from my wallet in the evening, so its guesses are usually right.
19. As a user, I want quick-captured transactions marked for review, so I can trust my balances while knowing which entries were guessed.
20. As a user, I want to clear my review queue in under a minute, so the flag is a genuine nudge rather than a growing debt.

---

## FR-4 · Transaction list ("the wall")

| ID | Requirement | Release |
|---|---|---|
| FR-4.1 | Reverse-chronological list, grouped by date, with sticky date headers | 1.0 |
| FR-4.2 | Each row shows kind, counterparty, label, amount, and a `needs_review` indicator | 1.0 |
| FR-4.3 | Virtualised list; smooth at 50,000+ transactions | 1.0 |
| FR-4.4 | Full-text search over description, party, app, and tag | 1.0 |
| FR-4.5 | Filter by date range, with presets: This Cycle, Last Cycle, This FY, Last FY, Custom | 1.0 |
| FR-4.6 | Filter by kind (multi-select) | 1.0 |
| FR-4.7 | Filter by container (multi-select), matching **either end** of the transaction | 1.0 |
| FR-4.8 | Filter by label (multi-select); selecting a parent includes its children | 1.0 |
| FR-4.9 | Filter by tag, party, app, amount range, currency, and `needs_review` | 1.0 |
| FR-4.10 | All filters compose with AND; multi-select within a dimension is OR | 1.0 |
| FR-4.11 | Show the count and total of the filtered set | 1.0 |
| FR-4.12 | Save a filter under a name; re-apply in one tap | 1.0 |
| FR-4.13 | Filter state survives navigation away and back | 1.0 |
| FR-4.14 | Edit a transaction inline from the list | 1.0 |
| FR-4.15 | Delete with undo available for at least 10 seconds | 1.0 |
| FR-4.16 | Indian digit grouping (`₹1,23,456`) when locale is `en-IN` | 1.0 |

**User stories**

21. As a user, I want every transaction touching HDFC, whether money went in or out, so the view is a real account statement.
22. As a user, I want to see all Food spending including Groceries and Dining without ticking each child, so the hierarchy saves me work rather than creating it.
23. As a user, I want to save "HDFC, this FY, no transfers" as a filter, so preparing for my accountant is one tap next April.
24. As a user, I want ₹1,23,456 shown the Indian way, so I read the magnitude correctly at a glance.
25. As a user, I want to undo an accidental delete, so a mis-swipe doesn't cost me a record.

---

## FR-5 · Dashboard

| ID | Requirement | Release |
|---|---|---|
| FR-5.1 | Net worth headline in the home currency | 1.0 |
| FR-5.2 | Three tiles: Liquid, Locked, Owed | 1.0 |
| FR-5.3 | Spend by label for the current cycle, largest first | 1.0 |
| FR-5.4 | Tap any label to drill into its filtered transaction list | 1.0 |
| FR-5.5 | Cycle income vs spend vs net | 1.0 |
| FR-5.6 | `needs_review` count with a link to the review queue | 1.0 |
| FR-5.7 | Cycle selector: current, previous, any past cycle | 1.0 |
| FR-5.8 | Goal progress summary with on-track status | 1.1 |
| FR-5.9 | Net worth trend over time | 1.2 |
| FR-5.10 | Month-over-month comparison per label | 1.2 |
| FR-5.11 | Top parties and top apps by spend | 1.2 |
| FR-5.12 | Month-in-review summary, generated on the first day of each cycle | 2.0 |
| FR-5.13 | Optional local notification when the month-in-review is ready | 2.0 |

**Charts follow the `dataviz` skill** for palette, form, and layout, so every chart in the
app reads as one system.

**User stories**

26. As a user, I want net worth broken into liquid, locked, and owed, so one number doesn't hide that most of my wealth is untouchable.
27. As a user, I want to tap "Food ₹8,420" and see exactly which transactions made it up, so a surprising number is immediately explicable.
28. As a user, I want my cycle to start on the 25th when I'm paid, so "what's left this month" means something.
29. As a user, I want a monthly summary I didn't ask for, so the app gives something back for the effort of maintaining it.

---

## FR-6 · Goals

| ID | Requirement | Release |
|---|---|---|
| FR-6.1 | Create a goal: name, target amount, currency, start date, target date | 1.1 |
| FR-6.2 | Choose funding mode per goal | 1.1 |
| FR-6.3 | Link one or more containers to a goal | 1.1 |
| FR-6.4 | Set an optional monthly contribution target | 1.1 |
| FR-6.5 | Set an opening allocation for money already saved | 1.1 |
| FR-6.6 | Show progress, percentage, and amount remaining | 1.1 |
| FR-6.7 | Show On Track / Behind / Achieved against expected-by-today | 1.1 |
| FR-6.8 | Show the monthly amount required to still hit the target | 1.1 |
| FR-6.9 | Attribute a transaction to a goal (contributions mode) | 1.1 |
| FR-6.10 | Warn when combined allocations exceed a linked container's balance | 1.1 |
| FR-6.11 | Never include goal progress in net worth | 1.1 |
| FR-6.12 | Archive a goal without deleting its transactions | 1.1 |
| FR-6.13 | Projected completion date at the current contribution rate | 1.2 |

**User stories**

30. As a user, I want my ring-fund RD's balance to be the goal's progress automatically, so a dedicated account needs no ongoing bookkeeping.
31. As a user, I want to tag ₹10,000/month from my salary account toward the ring, so a goal works even when the money is mixed with everything else.
32. As a user, I want to know I need ₹8,300/month from here to hit ₹3L by Aug 2028, so the goal tells me what to do rather than just how I'm doing.
33. As a user, I want warning when three goals claim more than my account holds, so I find out now rather than at the deadline.

---

## FR-7 · Export

| ID | Requirement | Release |
|---|---|---|
| FR-7.1 | Export the **currently filtered** transaction set to CSV | 1.1 |
| FR-7.2 | Export uses the same filter object as the list — no separate export configuration | 1.1 |
| FR-7.3 | UTF-8 **with BOM**, so Excel renders ₹ and Devanagari correctly | 1.1 |
| FR-7.4 | ISO-8601 dates (`2026-03-31`) | 1.1 |
| FR-7.5 | Amounts as bare decimals, no symbol, no thousands separators; currency in its own column | 1.1 |
| FR-7.6 | Separate columns for source and destination amounts and currencies | 1.1 |
| FR-7.7 | Columns: date, kind, source, destination, amounts, currencies, label, parent label, tags, description, marketplace app, payment app, goal, needs_review, capture source, created/updated timestamps | 1.1 |
| FR-7.8 | User-selectable column subset, remembered between exports | 1.1 |
| FR-7.9 | Soft-deleted transactions never exported | 1.1 |
| FR-7.10 | Delivered via the OS share sheet and save-to-file | 1.1 |
| FR-7.11 | Filename encodes the filter: `elefund-hdfc-2025-04-01_2026-03-31.csv` | 1.1 |
| FR-7.12 | Progress indicator; exporting 50,000 rows must not freeze the UI | 1.1 |
| FR-7.13 | **Free:** export all transactions, unfiltered. **Paid:** export a filtered set | 3.0 |
| FR-7.14 | Full JSON export of every entity, always free, as a portability guarantee | 1.1 |
| FR-7.15 | Excel (XLSX) export with formatting | 3.0 |
| FR-7.16 | PDF statement export per container | 3.0 |

**The free/paid split is deliberate.** Data portability stays free — it is the escape
hatch that makes trusting the app reasonable, and paywalling it in a *money* app is
corrosive. The filtering is what people will actually pay for.

**User stories**

34. As a user, I want to export FY 2025-26, credits and debits only, HDFC only, so my accountant gets exactly what they asked for and nothing else.
35. As a user, I want the CSV to open cleanly in Excel with ₹ intact, so I don't spend twenty minutes fixing encoding.
36. As a user, I want amounts Excel treats as numbers, so I can sum a column without cleaning it first.
37. As a user, I want to export everything for free, so I know I could leave EleFund if I ever wanted to.

---

## FR-8 · Audit trail

| ID | Requirement | Release |
|---|---|---|
| FR-8.1 | Record every create, update, delete, restore, and review on every entity | 1.0 |
| FR-8.2 | Record field-level before/after values | 1.0 |
| FR-8.3 | Record the capture source of the change | 1.0 |
| FR-8.4 | Timeline view on each transaction, newest first | 1.0 |
| FR-8.5 | Soft delete only; deleted transactions are restorable | 1.0 |
| FR-8.6 | A "recently deleted" view with restore | 1.0 |
| FR-8.7 | Audit written by database triggers, unbypassable by application code | 1.0 |
| FR-8.8 | Free-text comments on a transaction, appearing in its timeline | 2.0 |
| FR-8.9 | Global activity feed across all entities | 2.0 |

**User stories**

38. As a user, I want to see that I changed an amount from ₹450 to ₹540 last Tuesday, so I can tell whether a surprising number is an error or a correction.
39. As a user, I want to restore a transaction I deleted last week, so a mistake is recoverable rather than permanent.
40. As a user, I want the trail to include changes made from the widget, so history is complete regardless of where a change came from.

---

## FR-9 · Backup and restore

| ID | Requirement | Release |
|---|---|---|
| FR-9.1 | Sign in with Google solely to enable backup | 1.0 |
| FR-9.2 | Request only the `drive.appdata` scope — never broader Drive access | 1.0 |
| FR-9.3 | Encrypt every backup client-side before upload | 1.0 |
| FR-9.4 | Derive the encryption key from a user passphrase; cache it in the OS keystore | 1.0 |
| FR-9.5 | Show a one-time recovery code with an unambiguous warning about loss | 1.0 |
| FR-9.6 | Automatic backup when charging and on Wi-Fi, at most daily | 1.0 |
| FR-9.7 | Manual "back up now" | 1.0 |
| FR-9.8 | Retain a rolling window of the last N backups | 1.0 |
| FR-9.9 | Show last-backup time and size in settings | 1.0 |
| FR-9.10 | Restore requires passphrase or recovery code | 1.0 |
| FR-9.11 | Restore **replaces** local data and says so explicitly before proceeding | 1.0 |
| FR-9.12 | Refuse to restore a backup from a newer schema version than the installed app | 1.0 |
| FR-9.13 | Warn plainly that a lost passphrase and recovery code means an unrecoverable backup | 1.0 |
| FR-9.14 | Local encrypted backup file export, independent of Google | 1.2 |
| FR-9.15 | True multi-device sync with change-log merge | 3.0 |

**User stories**

41. As a user, I want my data backed up without thinking about it, so losing my phone doesn't lose eight years of records.
42. As a user, I want Google to hold ciphertext it cannot read, so choosing local-first for privacy isn't undone by the backup.
43. As a user, I want to be told bluntly that losing my passphrase loses my backup, so I write it down at setup rather than discovering the problem later.
44. As a user, I want restore to warn me it will replace what's on this device, so I don't destroy entries I hadn't backed up.

---

## FR-10 · Settings, security, onboarding

| ID | Requirement | Release |
|---|---|---|
| FR-10.1 | Biometric or device-credential lock on launch and on resume | 1.0 |
| FR-10.2 | Configurable auto-lock delay | 1.0 |
| FR-10.3 | Hide balances from the app switcher preview | 1.0 |
| FR-10.4 | Set home currency | 1.0 |
| FR-10.5 | Set cycle start day (1–28) | 1.0 |
| FR-10.6 | Light, dark, and system theme | 1.0 |
| FR-10.7 | Onboarding: create the first container and capture the first transaction inside two minutes | 1.0 |
| FR-10.8 | Seed a starter label set (Food, Travel, Shopping, Bills, Rent, Salary, Health, Uncategorised) | 1.0 |
| FR-10.9 | Manage labels, tags, apps, and parties: rename, merge, delete, recolour | 1.0 |
| FR-10.10 | Merge duplicate parties or apps, reassigning their transactions | 1.1 |
| FR-10.11 | "Privacy mode" blurring all amounts on demand | 1.1 |
| FR-10.12 | Wipe all local data, with a typed confirmation | 1.0 |

**User stories**

45. As a user, I want a fingerprint prompt on open, so handing someone my phone doesn't hand them my finances.
46. As a user, I want balances hidden in the app switcher, so a glance over my shoulder shows nothing.
47. As a user, I want to merge "Zomato" and "zomato" into one, so my history isn't fragmented by typos.
48. As a user, I want to blur amounts on a train, so I can review categories in public without displaying my net worth.

---

## Acceptance criteria for v1.0

v1.0 is done when all of the following are true on a real Android device:

- [ ] All seven container kinds can be created, and every balance is arithmetically correct against a hand-computed check.
- [ ] A credit card purchase increases debt without touching any bank balance; paying the bill leaves net worth unchanged.
- [ ] A typical capture completes in ≤ 3 interactions and under 5 seconds.
- [ ] Capture succeeds with aeroplane mode on, with no visible difference.
- [ ] Every filter dimension works and composes; a saved filter reapplies exactly.
- [ ] Editing, deleting, and restoring a transaction each appear correctly in its timeline.
- [ ] Deleting a transaction immediately and correctly changes the affected balances.
- [ ] A backup taken on one device restores correctly onto a freshly installed app.
- [ ] Restoring with the wrong passphrase fails safely, leaving local data untouched.
- [ ] The list stays smooth with 50,000 seeded transactions.
- [ ] Biometric lock engages on resume.
- [ ] **The app has been used for real, daily, for two weeks without a data-correctness bug.**
