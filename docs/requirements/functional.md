# EleFi - Functional Requirements

What the system does, from the user's perspective. Vocabulary from
[CONTEXT.md](../CONTEXT.md). Data rules in
[domain-model.md](domain-model.md).

**Requirement IDs are stable.** Tests, tickets, and commits reference them. Never
renumber; mark superseded requirements as such and add a new ID.

**Release column:** `1.0` · `1.1` · `1.2` · `2.0` · `3.0` - see
[roadmap.md](../roadmap.md).

---

## Problem statement

Kartikeya tracks money across many containers - several bank accounts, a credit card,
cash, a PPF, deposits - and currently has no single place that answers *"what do I
actually have, and where did it go?"*

Existing apps fail for three reasons:

1. **Capture is too slow.** By the time an app is open and a form is filled, the moment
   has passed. Transactions go unrecorded, the data develops holes, and once the data has
   holes it stops being worth maintaining. This is how every previous attempt died.
2. **They assume bank integration.** Indian account aggregator coverage is patchy,
   consent expires, and cash - a large share of real spending - is invisible to it.
3. **They model money too simply.** Credit cards get treated as wallets, transfers get
   counted as spending, and the resulting numbers can't be trusted.

## Solution

A local-first money tracker where **capture takes three seconds and works with the
network off**, the data model is exact enough that balances can be trusted, and the
user's financial data never leaves their device except as an encrypted backup in their
own Google Drive.

Two things narrow the gap between "money moved" and "money recorded":

- **The bank already told you.** EleFi reads its own alert SMS on the device, parses it
  with a bundled rule, and records the transaction at once, flagged Needs Review, with a
  notification to check or delete it (FR-11, ADR-0015).
- **What you filtered is what you get.** The transaction list's filters *are* the export
  configuration, so getting a year of one account to an accountant is the same six taps
  you already do to look at it (FR-7).

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
| FR-1.8 | Show available credit (`limit - owed`) on a CreditCard | 1.0 |
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
| FR-2.1 | Record a transaction with source party, destination party, amount, and date. Labels are optional, and any number may be attached | 1.0 |
| FR-2.2 | Derive and display the kind (Credit/Debit/SelfTransfer) from the chosen parties - never ask for it separately | 1.0 |
| FR-2.3 | Offer a kind-first shortcut (Debit / Credit / Transfer) that pre-filters the party pickers, without kind ever becoming a stored field | 1.0 |
| FR-2.4 | Default the date to today; allow any past date | 1.0 |
| FR-2.5 | Reject future dates with a clear explanation | 1.0 |
| FR-2.6 | Optional: description, marketplace app, payment app, labels | 1.0 |
| FR-2.7 | Autocomplete every party, label, and app field from previously entered values, ranked by recency and frequency | 1.0 |
| FR-2.8 | Draw source and destination suggestions from **one shared party pool** | 1.0 |
| FR-2.9 | Draw marketplace and payment suggestions from **one shared app pool** | 1.0 |
| FR-2.10 | Auto-create a party or app on first use, with no separate creation step | 1.0 |
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
| FR-2.21 | Note, Bought through and Paid with each offer their top three before anything is typed: first what was used with this contact before, then what is used most across all transactions. A new contact gets the most used straight away | 1.1 |

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
| FR-3.1 | Quick capture creates a **complete Transaction** using defaults, flagged `needs_review` - never a draft | 2.0 |
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
| FR-3.14 | In-app quick capture has a Debit / Credit / Self Transfer switch under its title, Debit first. Its steps follow the kind: Paid from and Paid to; Received into and Received from; Source and Destination, both containers. The Quick tab has its own faded dusty-teal disc, never the purple pill of the open tab; open, its label turns teal and the disc gets a thin ring | 1.1 |

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
| FR-4.2 | Each row shows kind, counterparty, its labels, amount, and a `needs_review` indicator | 1.0 |
| FR-4.3 | Virtualised list; smooth at 50,000+ transactions | 1.0 |
| FR-4.4 | Full-text search over description, party, and app | 1.0 |
| FR-4.5 | Filter by date range, with presets: This Cycle, Last Cycle, This FY, Last FY, Custom | 1.0 |
| FR-4.6 | Filter by kind (multi-select) | 1.0 |
| FR-4.7 | Filter by container (multi-select), matching **either end** of the transaction | 1.0 |
| FR-4.8 | Filter by label (multi-select, matching **any** of the chosen labels), and by "unlabelled" on its own | 1.0 |
| FR-4.9 | Filter by party, app, amount range, currency, and `needs_review` | 1.0 |
| FR-4.10 | All filters compose with AND; multi-select within a dimension is OR | 1.0 |
| FR-4.11 | Show the count and total of the filtered set | 1.0 |
| FR-4.12 | Save a filter under a name; re-apply in one tap | 1.0 |
| FR-4.13 | Filter state survives navigation away and back | 1.0 |
| FR-4.14 | Edit a transaction inline from the list | 1.0 |
| FR-4.15 | Delete, edit and confirm can be undone and redone from two arrows beside Filters and beside Close on an open transaction, greyed out when there is nothing to undo or redo (FR-14.1) | 1.0 |
| FR-4.16 | Indian digit grouping (`₹1,23,456`) when locale is `en-IN` | 1.0 |
| FR-4.17 | An export control on this page, exporting exactly the current filtered set - see FR-7.17-7.22 | 1.0 |
| FR-4.18 | The Filters sheet is compact: search, then one row per filter (Dates, Kind, Container, Label, and Contact when opened from one) saying what is chosen, which drops its options open underneath, one at a time; Needs review only is a switch. Clear filters is in the sheet's head, beside the cross | 1.1 |

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
| FR-5.2 | Three tiles: Liquid, Locked, Owed. Tapping one lists the containers that make it up | 1.0 |
| FR-5.3 | Spend by label for the current cycle, largest first, including an "unlabelled" bucket | 1.0 |
| FR-5.3a | State on the same screen that a transaction with several labels counts in full under each, so the bars can exceed the total. Bars are scaled to the largest label, never to the total | 1.0 |
| FR-5.4 | Tap any label to drill into its filtered transaction list | 1.0 |
| FR-5.5 | Cycle income vs spend vs net | 1.0 |
| FR-5.6 | `needs_review` count with a link to the review queue | 1.0 |
| FR-5.7 | Four equal period tabs: Today, This month (shown first), Last month, and Range, which then shows the chosen dates briefly ("10-12 Aug") | 1.0 |
| FR-5.8 | Goal progress summary with on-track status | 1.1 |
| FR-5.8a | What is still planned this month: to pay, to come in, and how many plans | 1.1 |
| FR-5.8b | The Steady ledger streak, with this week's status, recent weeks, freezes and the next milestone (S42) | 1.1 |
| FR-5.8c | Containers are listed on the Containers tab only; Home shows the figures they add up to | 1.1 |
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
| FR-6.13 | Projected completion date at the current pace: the last 90 days of contributions, or the planned monthly amount before there are any | 1.1 |
| FR-6.14 | Choose the goal a transaction counts toward on the capture form | 1.1 |

**User stories**

30. As a user, I want my ring-fund RD's balance to be the goal's progress automatically, so a dedicated account needs no ongoing bookkeeping.
31. As a user, I want to earmark ₹10,000/month from my salary account toward the ring, so a goal works even when the money is mixed with everything else.
32. As a user, I want to know I need ₹8,300/month from here to hit ₹3L by Aug 2028, so the goal tells me what to do rather than just how I'm doing.
33. As a user, I want warning when three goals claim more than my account holds, so I find out now rather than at the deadline.

---

## FR-7 · Export

**Amended 2026-08-29.** Filtered CSV export was v1.1 and its filtering was to be a paid
feature. It is now core, free, and in v1.0: the All Transactions page ships with an export
that respects the filters already applied to it. FR-7.13 is superseded by FR-7.17. Every
other ID keeps its meaning; only the release column moved.

| ID | Requirement | Release |
|---|---|---|
| FR-7.1 | Export the **currently filtered** transaction set to CSV | 1.0 |
| FR-7.2 | Export uses the same filter object as the list - no separate export configuration | 1.0 |
| FR-7.3 | UTF-8 **with BOM**, so Excel renders ₹ and Devanagari correctly | 1.0 |
| FR-7.4 | ISO-8601 dates (`2026-03-31`) | 1.0 |
| FR-7.5 | Amounts as bare decimals, no symbol, no thousands separators; currency in its own column | 1.0 |
| FR-7.6 | Separate columns for source and destination amounts and currencies | 1.0 |
| FR-7.7 | Columns: date, time, kind, source, destination, amounts, currencies, labels (semicolon-separated), description, marketplace app, payment app, needs_review, capture source, created/updated timestamps | 1.0 |
| FR-7.8 | User-selectable column subset, remembered between exports | 1.0 |
| FR-7.9 | Soft-deleted transactions never exported | 1.0 |
| FR-7.10 | Delivered via the OS share sheet and save-to-file | 1.0 |
| FR-7.11 | Filename encodes the filter: `elefi-hdfc-2025-04-01_2026-03-31.csv` | 1.0 |
| FR-7.12 | Progress indicator; exporting 50,000 rows must not freeze the UI | 1.0 |
| ~~FR-7.13~~ | ~~**Free:** export all transactions, unfiltered. **Paid:** export a filtered set~~ **Superseded by FR-7.17** | - |
| FR-7.14 | Full JSON export of every entity, always free, as a portability guarantee | 1.1 |
| FR-7.15 | Excel (XLSX) export with formatting | 3.0 |
| FR-7.16 | PDF statement export per container | 3.0 |
| FR-7.17 | Filtered CSV export is a **core, free** capability of the All Transactions page. *(supersedes FR-7.13)* | 1.0 |
| FR-7.18 | The export control lives **on the All Transactions page**, not in settings, not in a separate export screen | 1.0 |
| FR-7.19 | The export button names the set it will write: "Export 342 transactions", updating live as filters change | 1.0 |
| FR-7.20 | With **no** filters applied, the export contains every non-deleted transaction | 1.0 |
| FR-7.21 | With **any** filters applied, the export contains exactly the filtered set - same rows, same order as the list (`X1`) | 1.0 |
| FR-7.22 | Extends FR-7.11: with no filter applied the filename reads `elefi-all-<date>.csv` | 1.0 |

**Export takes no configuration of its own.** There is no export dialog with its own date
range and its own account picker. The transaction list already has every one of those
controls and the user has already set them; asking again is both redundant and a way for
the two to disagree. What is on screen is what comes out. See `X1`-`X5` in
[domain-model.md](domain-model.md).

**On the free/paid split.** FR-7.13 previously reserved *filtered* export for a paid tier
and left unfiltered export free. That is now reversed: filtering is what makes the export
useful, and an export that cannot be narrowed is not really an export. Whether some other
capability carries a price later is an open product question - but it will not be this
one, and it will not be data portability, which stays free permanently (FR-7.14, NFR-7.1).
Paywalling the exit from a money app is corrosive.

**User stories**

34. As a user, I want to export FY 2025-26, credits and debits only, HDFC only, so my accountant gets exactly what they asked for and nothing else.
35. As a user, I want the CSV to open cleanly in Excel with ₹ intact, so I don't spend twenty minutes fixing encoding.
36. As a user, I want amounts Excel treats as numbers, so I can sum a column without cleaning it first.
37. As a user, I want to export everything for free, so I know I could leave EleFi if I ever wanted to.

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
| FR-8.10 | The timeline names parties, containers and apps, and shows dates, flags and capture sources in words, never as stored ids or numbers; a party since deleted or merged is still named | 1.0 |

**User stories**

38. As a user, I want to see that I changed an amount from ₹450 to ₹540 last Tuesday, so I can tell whether a surprising number is an error or a correction.
39. As a user, I want to restore a transaction I deleted last week, so a mistake is recoverable rather than permanent.
40. As a user, I want the trail to include changes made from the widget, so history is complete regardless of where a change came from.

---

## FR-9 · Backup and restore

| ID | Requirement | Release |
|---|---|---|
| FR-9.1 | Sign in with Google solely to enable backup | 1.0 |
| FR-9.2 | Request only the `drive.appdata` scope - never broader Drive access | 1.0 |
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
| FR-9.16 | Settings > Backup and Restore holds three rows: Google Drive backup, Export to a file, Restore from a file. Until FR-9.1-9.13 are built, the Drive row opens a page laid out like WhatsApp's chat backup (last backup, Back up, Google Account, how often, mobile data, encrypted), all greyed out under "Coming soon" | 1.1 |

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
| FR-10.5 | Set cycle start day (1-28) | 1.0 |
| FR-10.6 | Light, dark, and system theme | 1.0 |
| FR-10.7 | Onboarding: create the first container and capture the first transaction inside two minutes | 1.0 |
| FR-10.8 | Seed a starter label set (Food, Travel, Shopping, Bills, Rent, Health, Salary, Investment). No system-owned label, and every seeded one is deletable | 1.0 |
| FR-10.9 | Manage labels, apps, and parties: create, rename, delete, recolour. Creating and renaming a label happens in a dialog, not a form the user scrolls past the whole list to reach | 1.0 |
| FR-10.10 | Merge duplicate parties or apps, reassigning their transactions | 1.1 |
| FR-10.11 | "Privacy mode" blurring all amounts on demand | 1.1 |
| FR-10.12 | Wipe all local data, with a typed confirmation | 1.0 |
| FR-10.13 | Deleting a label or a container asks first, naming the thing and stating how many transactions reference it. A container in use offers Archive instead of failing after the fact | 1.0 |
| FR-10.14 | Export every entity to a single local JSON file, and restore from one. The restore **replaces** local data rather than merging, says so before starting, and refuses a file written by a newer schema version | 1.0 |
| FR-10.15 | Say plainly at the point of export that the JSON file is **not encrypted** | 1.0 |
| FR-10.16 | An About section linking to the support page, the source repository, and the channel, as icons rather than paragraphs | 1.0 |

**User stories**

45. As a user, I want a fingerprint prompt on open, so handing someone my phone doesn't hand them my finances.
46. As a user, I want balances hidden in the app switcher, so a glance over my shoulder shows nothing.
47. As a user, I want to merge "Zomato" and "zomato" into one, so my history isn't fragmented by typos.
48. As a user, I want to blur amounts on a train, so I can review categories in public without displaying my net worth.

---

## FR-11 · SMS-assisted capture *(Android only)*

Vocabulary in [CONTEXT.md](../CONTEXT.md#alerts-and-suggestions). Data model in
[domain-model.md](domain-model.md) §12. The decision, and the Play-policy risk that could
force the fallback path to become the only path, are in
[ADR-0010](../adr/0010-on-device-sms-assisted-capture.md).

**The governing rule was `SM2`: a parsed message never becomes a transaction on its own.**
**Superseded on 2026-10-05 by ADR-0015:** a parsed message is recorded straight away, flagged
Needs Review. Rows below that describe confirming a suggestion are kept for the record and
read as describing the recorded transaction instead.
Every requirement below is downstream of it.

| ID | Requirement | Release |
|---|---|---|
| FR-11.1 | Read incoming SMS on-device and match the sender against enabled Parse Rules | 1.2 |
| FR-11.2 | Discard non-matching messages inside the receiver, without reading the body | 1.2 |
| FR-11.3 | Parse a matched alert into a Capture Suggestion: amount, currency, direction, container hint, counterparty hint, date | 1.2 |
| FR-11.4 | Produce **no suggestion at all** when a rule matches the sender but not the body. Never a partial guess | 1.2 |
| FR-11.5 | Raise a local notification (Suggestion Prompt) for each new suggestion | 1.2 |
| FR-11.6 | The prompt offers **Add** and **Dismiss**, and shows amount, direction, and matched container | 1.2 |
| FR-11.7 | **Add** opens the capture form pre-filled; the user confirms or corrects, then saves | 1.2 |
| FR-11.8 | Label action buttons on the prompt for the three most-used labels, saving in one tap | 2.0 |
| FR-11.9 | A confirmed suggestion creates a Transaction with `capture_source = 'Sms'`, through the normal capture path | 1.2 |
| FR-11.10 | Match the alert's last-4 to a Container; leave it unset and ask when the match is absent or ambiguous | 1.2 |
| FR-11.11 | Suppress duplicates: one real payment produces one suggestion, however many times its alert arrives | 1.2 |
| FR-11.12 | Suggestions expire after a configurable window (default 7 days) and are destroyed | 1.2 |
| FR-11.13 | Dismissing a suggestion destroys it immediately | 1.2 |
| FR-11.14 | An in-app inbox of pending suggestions, so a missed notification is recoverable | 1.2 |
| FR-11.15 | Settings: list Parse Rules, enable or disable each, per sender | 1.2 |
| FR-11.16 | Settings: paste a message and see exactly what it would parse to, or that it would not match | 1.2 |
| FR-11.17 | Users can add and edit their own Parse Rules for a bank EleFi does not ship | 1.2 |
| FR-11.18 | Ship builtin rules for the major Indian issuers (HDFC, ICICI, SBI, Axis, Kotak, plus UPI handles) | 1.2 |
| FR-11.19 | Builtin rules never match an OTP or verification message (`SM4`), covered by tests against real formats | 1.2 |
| FR-11.20 | The SMS permission is **optional**. Refusing or revoking it costs nothing but suggestions (`SM10`) | 1.2 |
| FR-11.21 | Explain, before the permission prompt, exactly what is read, what is kept, and that nothing is transmitted | 1.2 |
| FR-11.22 | **Paste-a-message fallback**, working with no SMS permission at all: paste a bank alert, get the same pre-filled form | 1.2 |
| FR-11.23 | **Share-sheet fallback**: share a bank SMS into EleFi from the messaging app, get the same pre-filled form | 1.2 |
| FR-11.24 | A one-tap toggle disabling SMS reading entirely, without uninstalling or revoking in system settings | 1.2 |
| FR-11.25 | Notification-listener ingest for an allow-list of Payment Apps (GPay, PhonePe, Paytm, Amazon Pay, CRED), producing suggestions under the same rules as SMS (ADR-0014, `SM12`) | 1.2 |
| FR-11.27 | When a bank SMS and a Payment App notification describe the same payment, merge them into one suggestion: the bank's container, the app's payee name, note, and Payment App (`SM14`) | 1.2 |
| FR-11.28 | A separate in-app switch for Payment App notifications, off by default, which opens the system notification-access screen when turned on | 1.2 |
| FR-11.29 | ~~The dashboard shows how many payments are waiting to be confirmed~~ Superseded by ADR-0015: the dashboard shows how many transactions need review, linking to the list filtered to them | 1.2 |
| FR-11.30 | Parsed alerts are recorded straight away as transactions flagged Needs Review, with a prompt to open or delete each (ADR-0015) | 1.2 |
| FR-11.31 | Teach EleFi a message: paste a message, see what is read, record it, or mark its values so EleFi learns a rule from one example; taught rules can be switched off or forgotten | 1.2 |
| FR-11.32 | Deleted transactions are listed in Settings with Restore and Restore all; restored ones are flagged for review | 1.2 |
| FR-11.33 | Teaching a message with no last four digits can capture the account's name instead; with no digits, the one container the name or the sender's bank fits is used (`SM18`) | 1.2 |
| FR-11.34 | A recorded alert takes the time it arrived, unless the message dates it to another day (`SM19`) | 1.2 |
| FR-11.26 | Parse Rules are bundled with the app. No rule is ever fetched from a network | 1.2 |

**FR-11.22 and FR-11.23 are not consolation prizes.** They are the requirements that keep
this feature shippable if the restricted-permission declaration is refused, and they are
built **first**, before any code touches the SMS API. They also carry the whole feature on
iOS, where reading SMS is impossible and always will be.

**FR-11.26 is a deliberate limit.** Fetching rules would make new bank formats a data push
instead of a release, which is genuinely better. It also creates a channel into the app
that decides how the app reads the user's messages. That trade needs an ADR, not a
convenience decision.

**User stories**

49. As a user, I want a notification saying "₹450 to Zomato from HDFC?" seconds after I pay, so recording it is one tap while I still remember what it was.
50. As a user, I want the app to never invent a transaction from a text message, so a promotional SMS can never silently corrupt my balances.
51. As a user, I want to refuse the SMS permission and still use everything else, so the feature is an offer rather than a toll.
52. As a user, I want to paste a bank message and have it parsed, so I get the benefit even where the permission is unavailable.
53. As a user, I want my OTPs to be untouched and unstored, so granting SMS access does not mean handing over my second factor.
54. As a user, I want to add a rule for my own bank when EleFi does not know its format, so I am not waiting on a release.
55. As a user, I want to see, before I grant anything, exactly what will be read and what will be kept, so the decision is informed rather than hopeful.

---

## FR-12 · About, licence, and support

EleFi is free. Every feature, permanently. There is no paid tier, no unlock, and no
supporter build. See [ADR-0012](../adr/0012-free-forever-voluntary-support.md).

| ID | Requirement | Release |
|---|---|---|
| FR-12.1 | An About screen naming the version, the MIT licence, and a link to the source | 1.0 |
| FR-12.2 | A support link on the About screen, pointing at the shared support page with `?from=elefi-app` | 1.0 |
| FR-12.3 | Wording beside the link stating plainly that it buys nothing: no features, no priority, no say over the app | 1.0 |
| FR-12.4 | The link opens the **system browser**, never the app's own WebView | 1.0 |
| FR-12.5 | No analytics on the link. EleFi records nothing about who followed it or whether anyone did | 1.0 |
| FR-12.6 | The support link is never a modal, an interstitial, a nag, or a badge. One line on one screen | 1.0 |
| FR-12.7 | No feature, screen, or export is ever gated, degraded, or delayed by whether the user has supported | 1.0 |
| FR-12.8 | A link to the privacy summary, matching what [SECURITY.md](../../SECURITY.md) states | 1.1 |

**FR-12.4 is not a preference.** Under Blazor Hybrid a plain `<a href>` navigates the host
`BlazorWebView`, which strands the user inside the app with no browser chrome and no way
back. It goes through `Browser.OpenAsync(url, BrowserLaunchMode.External)`, and the Android
manifest needs the `https` intent query declared or the call fails silently. The same rule
keeps a future store listing out of trouble, because a payment page inside an app web view
reads as billing that bypassed the store.

**FR-12.6 and FR-12.7 are the whole point.** A tip that buys something is a sale, and a sale
brings GST, store payment rules, and an obligation to the person who paid. A tip that nags
is an advertisement. Neither belongs in an app whose pitch is that it is not working against
you.

**User stories**

56. As a user, I want to know the app is free and stays free, so I am not waiting for the feature I need to move behind a paywall.
57. As a user, I want to chip in if I feel like it, once, without being asked twice.
58. As a user, I want the support link to open my actual browser, so I can see where it goes before I decide anything.
59. As a user, I want to be told the tip buys nothing, so I am not confused about what I just did.

---

## FR-13 · Contacts

The people and businesses money goes to and comes from. A contact is the external party a
transaction already names ([ADR-0016](../adr/0016-contacts-merge-with-aliases.md)).

| ID | Requirement | Release |
|---|---|---|
| FR-13.1 | Settings > Contacts lists every contact with its transaction count, searchable by name | 1.1 |
| FR-13.2 | A contact can be added by name, or picked from the phone's contacts with no contacts permission | 1.1 |
| FR-13.3 | A contact's page shows what was paid to it and received from it, and opens the transaction list filtered to it and its linked contacts | 1.1 |
| FR-13.4 | Phone numbers, emails, addresses and other details, each with an optional label, checked before they are kept (`CT2`) | 1.1 |
| FR-13.5 | Notes on a contact | 1.1 |
| FR-13.6 | Link two contacts as the same, or as another account of the same person or business (`CT3`) | 1.1 |
| FR-13.7 | Likely duplicates have their own page, behind a button beside New that shows how many wait. Each pair offers Merge, Same and Different, explained by an "i" | 1.1 |
| FR-13.8 | Merge one contact into another: everything moves, the old name becomes an alias, and the undo arrow reverses it (`CT4`) | 1.1 |
| FR-13.9 | Renaming can keep the old name as an alias, so alerts that still use it land on the contact | 1.1 |
| FR-13.10 | Contacts, their details, links, notes and aliases are in the JSON backup and come back on restore | 1.1 |

**User stories**

60. As a user, I want "ZOMATO LTD", "Zomato" and "zomato@hdfcbank" to be one contact, so spending at one place reads as one place.
61. As a user, I want my landlord's number and account details beside the rent I pay them, so I do not hunt through chats for them.
62. As a user, I want Rahul's two UPI accounts linked, so I can see everything I have sent him in one list.

---

## FR-14 · Ele, undo, and the Planner

| ID | Requirement | Release |
|---|---|---|
| FR-14.1 | Undo and Redo arrows at the top of every page that changes things: transactions, deleted, capture, quick capture, containers, labels, contacts, duplicates, plans, goals. Each reaches only its own kind of change, runs through the same checked path as the change, and is greyed out when there is nothing to do | 1.1 |
| FR-14.2 | Ele is the only messenger: every confirmation, undo and problem, including a required field left empty, is something Ele says. No toasts, no inline error banners | 1.1 |
| FR-14.3 | Ele is pleased when something worked, calm for a deletion or an undo, and cross for a problem. A newer message pushes the one showing down and out | 1.1 |
| FR-14.4 | A short synthesised sound with each message, matching its tone. Settings mutes and unmutes it with a round speaker button, not a switch | 1.1 |
| FR-14.5 | A weekly plan can fall on chosen days; a monthly plan on the last day of every month; any repeat can end on a date or after a number of times | 1.1 |
| FR-14.6 | An occurrence can be skipped; a repeating plan then rolls on, and reopening undoes the skip | 1.1 |
| FR-14.7 | A plan's labels go onto the transaction it becomes when that has none | 1.1 |
| FR-14.8 | A reminder notification on the due day, at the plan's time or 9:00, with Mark done and Snooze to tomorrow. Put back after a restart | 1.1 |
| FR-14.9 | Every change to a plan is in its history | 1.1 |
| FR-14.10 | Ele's guide: a short walk through every major feature, over the app itself. Each step opens its page behind a dim overlay and lights its tab; Ele says a sentence or two, each on its own line, with her sound; Previous and Next beside her, a solid Skip at the top; back steps back. It starts on its own once for a new user with no containers, and Settings > Help > Quick guide by Ele starts it any time | 1.1 |
| FR-14.11 | Back always goes to the page that was open before, in the order they were opened, including from an open transaction. With nothing behind, the first press has Ele say "Press back again to close EleFi." and only a second press within 2.5 seconds closes the app. One press never closes it | 1.1 |
| FR-14.12 | Coming back to the app from the recent apps reads every list and figure again (Home, transactions, containers, contacts, plans, goals, notifications, deleted, duplicates). Forms are never read again, so nothing half typed is lost | 1.1 |
| FR-14.13 | The open tab is marked by its colour and one soft pill behind its icon, which glides from the tab that was open to the new one as the page changes. Quick has no pill; the pill fades there and glides on from where it was | 1.1 |

### Ele's moods

**What Ele looks like.** A cheerful young elephant in the spirit of Duolingo's Duo: a bright
violet, round head, two big eyes close together with large pupils and two sparkles each, soft
puffed cheeks, round ears held out, a short chubby trunk that curls up at the tip, and two
small tusks. No mouth and no brows: the eyes' lids carry the feeling, backed by the trunk and
the ears. Confetti comes when she turns pleased, or when a pleased message or guide step
arrives, never on a picture that simply opens pleased (About, Settings). One drawing,
`wwwroot/js/elefi-ele-shape.js`, makes every Ele: the live one, and
the app icon, splash, start-up screen and brand icon, which `docs/brand/generate.mjs` writes
from it.

| Face | Eyes | Trunk and ears |
|---|---|---|
| At rest | Wide open | Trunk hanging, tip curled up; ears out |
| Pleased | Lower lids rise into a smile | Trunk raised up the right side of the face to eye level, its lighter ridged underside showing, its end turned to the front with both nostrils; ears spread; a burst of confetti |
| Cross | Upper lids slant in | Trunk droops a little; ears held back |
| Sad | Upper lids droop at the outer corners | Trunk hangs straight; ears low |
| Hiding amounts | Covered | Both big ears flop down over the top of the face from its edges, in an eased 0.6 s fold that reverses smoothly if tapped mid-way. A page that opens with amounts already hidden shows her already hiding |

Ele has three moods for messages. Each has its own face, movement and sound, so the mood reads
at a glance even with the sound off. Built in `EleReactions` (`EleTone`) and `EleOverlay`; the
sounds are synthesised in `wwwroot/js/elefi-sound.js`.

| Mood | Face | Movement | Sound | Stays | Meaning |
|---|---|---|---|---|---|
| **Pleased** | Pleased | Hops twice, then floats; confetti | A short trumpet, then three bright twinkles | 4 s | Something worked |
| **Calm** | At rest | Only floats | One warm bell with a long, quiet tail | 4 s | Something neutral: a deletion, an undo, an archive |
| **Cross** | Cross | Shakes its head twice, then floats; the line is in the warning colour | Two short low trunk blasts, never a buzzer | 5 s, long enough to read | Something needs fixing |

Ele's guide uses the pleased sound for its first and last steps, and the calm bell for every
step between.

Every mood arrives from the right above the tab bar and leaves by sliding down and fading
over 0.42 s, whether it timed out, its × was tapped, or a newer message replaced it. Only
the × takes a tap; everything under Ele keeps working. Settings > Ele's sound silences all
three sounds. Ele itself cannot be switched off.

**Which mood each action brings**

| Where | Pleased | Calm | Cross |
|---|---|---|---|
| Capture, Quick capture | Recorded | | An empty or zero amount; no Paid to or Received from; a container that no longer exists; recorded but not added to the goal |
| An open transaction | Saved changes; Looks right | Deleted | The same checks as Capture |
| Deleted transactions | Restored, or Restore all | | |
| Undo and Redo arrows, any page | | Undone, or Redone, naming what | The step could not run, and why |
| All transactions | Export written | | Export did not finish |
| Containers | Added; updated | Deleted; archived | No name; a name taken; an opening balance that is not a number; delete refused because it has transactions |
| Labels | Added; renamed | Deleted | No name; a name taken |
| Contacts, a contact's page | Added; renamed; detail added; linked; linked to a phone contact; merged | Notes updated; detail removed; unlinked | A name taken; a detail that is not what it says (an email that is not one) |
| Possible duplicates | Merge; Same | Different | The merge or link was refused |
| Plans | Planned; updated; ticked off | Deleted; skipped; back on the list | No title; an amount that is not a number; an end date before the due date; a count under one |
| Goals | Added; updated | Archived or brought back; deleted | No name; a target, saved or monthly amount that is not a number; following a balance with no container |
| Home | A streak milestone: 4, 12, 26 or 52 weeks, once each | | |
| Teach EleFi a message | Recorded; learned | Forgot a rule | A value copied that is not in the message; nothing recorded |
| Settings | Backup written; restored; turn on notification access | Everything wiped | SMS permission refused; not an EleFi backup; restore or export stopped |

**How to see and hear each one.** Nothing here changes data you keep: every step is undone
by the next.

1. **Cross:** Capture, tap Record with the amount empty. Ele: "Enter an amount greater than
   zero."
2. **Replacement:** type an amount, leave Paid to empty, tap Record. The first Ele slides
   down and fades as the second asks "Who was it paid to?"
3. **Pleased:** fill in Paid to ("Ele test"), tap Record.
4. **Calm:** tap the Undo arrow at the top. Ele: "Undone: recording ₹... to Ele test." The
   test transaction is soft-deleted and waits in Settings > Deleted like any other.
5. For no trace at all: Settings > Edit labels, Add "Test" (pleased), then Undo (calm).

The sounds follow the phone's media volume.

**User stories**

63. As a user, I want to take back the last thing I did on any page, so a wrong tap costs nothing.
64. As a user, I want one voice telling me what happened, so a confirmation and an error never compete.
65. As a user, I want my gym plan on Monday and Thursday and my rent on the last day, so plans match my real life.
66. As a user, I want a reminder I can snooze from the notification, so a busy day does not mean a missed bill.

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
- [ ] Exporting with no filter yields every transaction; exporting with a filter yields exactly the rows on screen, verified row-for-row against the list.
- [ ] A 50,000-row export completes without freezing the UI, and opens in Excel with the rupee sign intact.
- [ ] Biometric lock engages on resume.
- [ ] **The app has been used for real, daily, for two weeks without a data-correctness bug.**
