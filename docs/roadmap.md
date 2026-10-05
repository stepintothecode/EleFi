# EleFi - Roadmap

Work broken into **tracer-bullet slices**. Each slice cuts a narrow but complete path
through every layer - schema, domain, repository, UI, tests - and is demoable on its own.
Each declares the slices that **block** it.

Vocabulary from [CONTEXT.md](CONTEXT.md). Requirement IDs from
[functional.md](requirements/functional.md).

**The governing constraint:** v1 must reach daily real use as fast as it responsibly can.
The largest risk to this project is not a wrong technical decision - it is the project
stalling before it becomes a habit. Every cut below serves that.

**Status 2026-10-05.** Alerts are now recorded straight away as Needs Review transactions
([ADR-0015](adr/0015-record-alerts-as-needs-review.md)); the To confirm inbox is gone, replaced by
Teach EleFi a message (taught Parse Rules, the first part of S39) and a Deleted screen with Restore.
The list gained date and time ranges, a running total and return-to-where-you-were; the dashboard
gained period tabs and a hide-amounts switch; the theme moved to lavender. See
[the decision log](prompts/2026-10-05-ledger-ux-and-auto-recorded-alerts.md).

**Status 2026-10-04.** S38 and S40 are built (SMS receiver, Payment App notifications,
merging, the "To confirm" inbox), pending device verification, and capture had a usability
pass: grouped container pickers, type-to-find payees over the whole history, labels in one
row ordered by use, explanations behind info buttons, sheets for container editing, a
back gesture that steps back instead of closing the app, and "Add other details" on quick
capture. See [the decision log](prompts/2026-10-04-capture-ux-and-payment-alerts.md).

**Status 2026-08-30.** S1 to S7 and S15 are implemented and green: 77 tests, 0 warnings,
`dotnet build EleFi.slnx` and `dotnet test` both pass, and the Android head builds. What is
implemented is marked below. S37's parser and its permission-free paste path landed early,
because the parser is pure and the corpus tests cost almost nothing once the domain existed.

**Re-cut 2026-08-29.** Three changes: the stack is C#/.NET MAUI
([ADR-0009](adr/0009-dotnet-maui-over-expo-typescript.md)), so S1 is rebuilt and gains a
cold-start spike; CSV export moves from v1.1 into v1.0 as S15; and SMS-assisted capture
arrives in v1.2 as S37-S39. Slice numbers are stable - S15 kept its ID when it moved.

---

## Release map

| Release | Theme | Rough size |
|---|---|---|
| **v1.0** | Daily driver - capture, containers, list, balances, **filtered export**, backup | ~8-10 weeks |
| **v1.1** | Goals, merge and tidy | ~2 weeks |
| **v1.2** | **SMS-assisted capture**, multi-currency, richer reporting | ~4-5 weeks |
| **v2.0** | Quick Capture (notification, tile, widget) + web build | ~4-5 weeks |
| **v3.0** | Bubble, iOS, true sync, wider distribution | open-ended |

v1.0 grew from 6-8 weeks to 8-10. Two reasons, both deliberate: export moved in, and the
first slice now carries a stack the project has not built on before.

---

## v1.0 - Daily driver

### S1 · Foundation and stack proof

> **Done**, except the cold-start measurement and the release pipeline's first real run.
**Blocked by:** nothing

**Partly done already.** The solution skeleton is on disk and green: `EleFi.slnx` with
five projects in `src/` and six in `tests/`, `Directory.Build.props` (nullable,
warnings-as-errors, XML docs required in `src/`), `Directory.Packages.props`,
`.editorconfig`, and `EleFi.Architecture.Tests` enforcing both the dependency rule and the
`src`-to-`tests` mirror. `dotnet build` and `dotnet test` pass.

Still to do in this slice: EF Core 10 over SQLite with **SQLCipher active from the first
migration**, the startup migration runner, the CsCheck harness in anger, and the GitHub
Actions pipeline. A trivial table proves the migration path end to end.

**Two spikes, both gating, both before any screen exists:**

1. **Cold start on the reference device** (NFR-1.1, ≤ 2.5 s) with a Blazor Hybrid shell.
   Missing it reopens the Blazor-vs-XAML decision - that is what ADR-0011 in Step 2 is
   for, and it is why the measurement comes first.
2. **SQLCipher through EF Core**, keyed from Android Keystore, surviving a restart and a
   migration. An unusual pairing, and everything else sits on top of it.

**FR-none · NFR-1.1, NFR-5.10, NFR-8.1, NFR-8.5**

*Demoable:* app launches on a real phone, creates an encrypted database, runs a migration,
reports its own cold start, and passes CI.

> The spikes are the slice. Building thirteen slices on an unmeasured assumption is how a
> stack decision gets discovered at week nine instead of week one.

---

### S2 · Money primitives

> **Done.** `Money`, `Currency`, `MoneyText`, round-trip property tests over four exponents.
**Blocked by:** S1

`EleFi.Domain.Money`: `Money` as a readonly record struct over integer minor units,
per-currency exponents, parse, format (including `en-IN` lakh/crore grouping), banker's
rounding, arithmetic. Round-trip property tests. The analyzers banning float money and
banning `Money` arithmetic outside this namespace.

*Demoable:* a test suite proving `parse(format(m)) == m` across generated inputs.

> Deliberately first. Every later slice depends on money being exactly right, and this is
> the cheapest possible moment to get it wrong and find out.

---

### S3 · Containers

> **Done.** All seven kinds, the Party row created with each container, the containers screen.
**Blocked by:** S2

`Container` and its `Party` row. All seven kinds with their kind-specific fields. Create,
edit, archive. Container list and detail screens. Invariants C1-C5 as constraints and
tests. Opening balance and its as-of date.
**FR-1.1-1.5, 1.9-1.11**

*Demoable:* add an HDFC account and a credit card; see both listed with opening balances.

---

### S4 · Classification entities

> **Done.** Labels seeded, parties and apps auto-created on first use, ranked suggestions.
**Blocked by:** S2

`Label` (flat, many per transaction, ADR-0013), `App` (with `default_container_id`), and
external `Party`. Auto-create on first use. Ranked suggestion queries. Seeded starter
labels, none of them system-owned. **FR-2.7-2.10, FR-10.8, FR-10.9**

*Demoable:* type a new party name and see it persist and reappear as a suggestion.

---

### S5 · Capture - the core slice

> **Done.** T1 to T7 enforced in `CaptureService` and again as CHECK constraints.
**Blocked by:** S3, S4

The `Transaction` entity with dual amounts, derived kind, invariants T1-T9. The capture
form: amount-first, keypad focused, kind shortcut, party pickers over the shared pool,
multi-select label chips, optional description and apps. Save and add another.
**FR-2.1-2.16**

*Demoable:* record a Debit, a Credit, and a Self Transfer, offline, in under five seconds
each.

> **The single most important slice in the project.** Everything before it is scaffolding;
> everything after it is leverage.

---

### S6 · Derived balances and net worth

> **Done.** `INV-CC` and `D1` both covered by tests against a real database.
**Blocked by:** S5

Balance computation, liability and liquidity handling, net worth with liquid/locked/owed.
Property test: replaying all transactions equals the computed balance. Explicit tests for
`INV-CC` (card spending and bill payment) and `D1` (Self Transfers excluded from spend).
**FR-1.6-1.8**

*Demoable:* spend on the card, pay the bill, watch net worth stay correct throughout.

---

### S7 · Transaction list and filters

> **Done.** Every dimension, container matching either end, paging.
**Blocked by:** S5

Virtualised grouped list, the composable `TransactionFilter`, every filter dimension,
search, saved filters, filtered count and total. Container filter matches **either end**.
**FR-4.1-4.13, FR-4.16**

*Demoable:* filter to one bank, this FY, credits and debits only - the account statement.

---

### S15 · Filtered CSV export

> **Done.** `X1` is a test, not an intention.
**Blocked by:** S7 · *moved here from v1.1*

The export control on the All Transactions page, exporting exactly the current filtered
set. UTF-8 BOM, ISO dates, bare decimals, dual amounts, selectable columns, share sheet,
descriptive filename, streamed so 50k rows never block the UI. The live row count on the
button.

The `X1` property test - `rows(export(f)) == rows(list(f))` for every generated filter -
is the slice's real deliverable. It is what makes "the export is the filtered view" a
guarantee rather than an intention.
**FR-7.1-7.12, FR-7.17-7.22, FR-4.17, NFR-1.9, NFR-3.11**

*Demoable:* filter to one bank and one financial year, tap Export, open the CSV in Excel,
and count the rows against the screen.

> Moved into v1.0 because the list without an exit is a worse product than it looks. It is
> also cheap here: it reuses S7's query untouched, which is the entire point of `X1`.

---

### S8 · Edit, delete, audit trail
**Blocked by:** S5

Audit triggers on every table, created inside the migration that creates the table.
`AuditEvent`, soft delete, timeline view, undo, recently deleted with restore.
**FR-4.14-4.15, FR-8.1-8.7**

*Demoable:* edit an amount, see the before/after in the timeline; delete and restore.

---

### S9 · Dashboard
**Blocked by:** S6, S7

Net worth headline, three tiles, spend-by-label for the cycle with drill-down, income vs
spend, needs-review count, cycle selector. Charts follow the `dataviz` skill.
**FR-5.1-5.7, FR-3.9**

*Demoable:* open the app and understand your position in three seconds.

---

### S10 · Design system and motion
**Blocked by:** S1 · *runs alongside S3-S9*

Tokens, typography, light/dark themes, the shared motion module (spring presets,
shared-element transitions, list animations) with every constant defined once, haptics,
skeleton loaders, reduced-motion handling.

The elephant mascot ships behind `IMascotService`: a Rive state machine driven from a JS
module in `wwwroot/js/`, with the C# side owning `IJSRuntime` and implementing
`IAsyncDisposable`. A placeholder SVG animation stands in until the real art exists, so
nothing downstream waits on assets. Per NFR-2.7 the mascot is never on the critical path
of an interaction.
**NFR-2 in full**

*Demoable:* the capture flow at 60 fps, and the same flow correct with animations disabled
at OS level.

> Sequenced alongside rather than after. Retrofitting motion onto finished screens is how
> apps end up feeling assembled rather than designed.
>
> This slice carries the cost of ADR-0009. If motion is going to disappoint, it disappoints
> here, early enough to act on.

---

### S11 · Security and settings
**Blocked by:** S3

Biometric lock, auto-lock delay, app-switcher masking, home currency, cycle start day,
theme, wipe-all-data. Database encryption already proved in S1; this slice makes the key
lifecycle and lock behaviour real.

Also the About screen: version, MIT licence, source link, and the support link through
`ILinkOpener` so it opens the system browser rather than the host WebView, with the
wording that says plainly it buys nothing.
**FR-10.1-10.6, FR-10.12, FR-12.1-12.7, NFR-5.6-5.7, NFR-5.10**

*Demoable:* background the app, return, get a fingerprint prompt; the switcher shows no
amounts. Tap the support link and land in Chrome, not inside EleFi.

---

### S11b · Local JSON backup and restore

> **Done.** `BackupFile` and `LocalBackup`, wired to the share sheet and the file picker.
**Blocked by:** S5

Export every entity to one plain JSON file, and restore from one. Enums travel by name and
identifiers are preserved, so a restored transaction still points at the same container and
the same labels. Restore **replaces** rather than merges, says so before it starts, and
refuses a file written by a newer schema version. **FR-10.14, FR-10.15**

*Demoable:* export, wipe, restore, and see the same balances.

> The stopgap that makes S12 non-urgent rather than critical. Until Drive backup exists,
> this is the only thing standing between a lost phone and a lost ledger, which is why it
> shipped before the encrypted path rather than waiting for it. It is deliberately the
> simplest thing that works: plain JSON, unencrypted, moved by the user, with the app saying
> so at the point of export.

---

### S12 · Encrypted Google Drive backup
**Blocked by:** S5, S11, S11b

OAuth 2.0 with PKCE via `WebAuthenticator` (`drive.appdata` only), passphrase setup,
Argon2id key derivation, recovery code, AES-256-GCM snapshot, upload via WorkManager,
rolling retention, automatic and manual backup, restore with explicit replace warning,
schema-version refusal.
**FR-9.1-9.13**

*Demoable:* back up, wipe the app, reinstall, restore, and verify the data is identical.

> The last thing to be cut, not the first. Every day of real use before this exists is a
> day of irreplaceable data living on exactly one device.

---

### S13 · Onboarding and hardening
**Blocked by:** S9, S10, S12, S15

First-run flow reaching a captured transaction in under two minutes. Empty, loading, and
error states. Accessibility pass. 50,000-row performance benchmarks on a real device.
Manual matrix on a mid-range device and an OEM-skinned device. The tag-triggered release
pipeline: signed APK published as a GitHub Release asset for sideloading.
**FR-10.7, NFR-1, NFR-6**

*Demoable:* **v1.0 installed on your own phone.**

---

## v1.1 - Goals and tidy

### S14 · Goals
**Blocked by:** S6

Both funding modes, container links, contributions, opening allocation, progress, status,
required-monthly, over-allocation warning, dashboard summary.
**FR-6.1-6.12, FR-5.8**

### S16 · Merge and tidy
**Blocked by:** S4

Merge duplicate parties and apps with transaction reassignment; privacy mode; maturity and
due-date reminders. Full JSON export moved forward into S11b and is done.
**FR-10.10-10.11, FR-1.13-1.14**

> Privacy mode is done (hide amounts, 2026-10-05). Merge and reminders are not.

### S41 · Planner
**Blocked by:** S5, S8 · *added 2026-10-05; absorbs S32 (recurring transactions)*

A tab for money you expect to move: SIPs, rent, the card bill, an insurance premium, paying
a friend back. Shaped like Microsoft To Do, but every item is a **Plan**: a title, an
optional amount, and the transaction it should become.

- **A Plan is not a Transaction.** It never touches a balance, a report or an export until
  it is done, so `D2` and `X1` are untouched. What it adds is foresight: "₹38,500 still
  planned this month" on the dashboard.
- **Fields:** title, due date (optional time), amount, kind, Source and Destination (a
  container and a payee, or two containers for a SIP into a mutual fund), labels, note,
  and a **repeat rule**: daily, weekly on chosen days, monthly on a day or the last day,
  yearly, every N of any of these, with an optional end date or count. Same shape as To Do.
- **The list:** open plans grouped Overdue, Today, This week, Later, in due-date order;
  **Completed** collapsed underneath, newest first. A circle on the left ticks a plan off.
- **Ticking off does the bookkeeping.** EleFi first looks for a transaction that already
  records it: same amount (within a tolerance the plan sets, default exact), same container
  where the plan names one, within a few days of the due date, not already claimed by
  another plan. One match is linked. Several are offered to choose from. None opens the
  capture form pre-filled from the plan, so one more tap records it. A plan can also be
  ticked as "done without a transaction", for the ones that are not money.
- **Plans watch the ledger.** When an SMS or payment-app alert records a transaction that
  matches an open plan, the plan is ticked and linked automatically and the Alert Prompt
  says so ("SIP to Axis Bluechip, done"). The SIP whose SMS never came is the one left
  open on the due date, which is exactly the one worth a reminder.
- **Repeats roll forward on completion,** not on the calendar, so a missed month stays
  overdue instead of silently being replaced by the next one. Skipping an occurrence is an
  explicit action.
- **Reminders:** a local notification on the due date for plans not yet done, with "Mark
  done" (link or record) and "Snooze to tomorrow".
- **Audited** like transactions, soft-deleted like transactions.

*Demoable:* create a monthly SIP plan from SBI to the mutual fund; the SIP's SMS arrives and
the plan ticks itself; next month the SMS does not come, the plan is overdue on the 6th, one
tap records the transaction and rolls the plan to the following month.

> Absorbs S32 rather than sitting beside it. A recurring transaction that books itself is
> SM2's old worry in a new form: money recorded with nobody checking. A plan that ticks
> itself only when a real transaction appears, and otherwise asks, keeps the ledger honest.

### S42 · Steady ledger (consistency streak) - proposed
**Blocked by:** S9, S41 · *proposed 2026-10-05, not yet agreed*

Duolingo's habit loop without asking for a transaction a day. The unit is the **week**, and
the habit is **keeping the ledger true**, which takes about a minute because alerts record
most of it:

- A week counts when, by Sunday night, nothing from that week still needs review and no
  plan due that week is left open. Zero transactions is fine: there was nothing to tidy.
- **Ele carries it.** Ele's mood follows the streak, and a fresh week starts with Ele asking
  for the tidy. No guilt copy, no red badges.
- **Freezes**, earned one per four clean weeks and spent automatically, so a holiday does
  not reset a year.
- **Milestones** at 4, 12, 26 and 52 weeks, celebrated by Ele, never by a store or a reward:
  ADR-0012 means there is nothing to sell or unlock.
- Optional **monthly balance check** (S20): confirm one container's balance matches the
  bank, which is what actually keeps the numbers trustworthy.

---

## v1.2 - SMS-assisted capture, multi-currency, reporting

### S37 · Alert parsing and the permission-free path
**Blocked by:** S5 · **FR-11.3-11.4, 11.7, 11.9, 11.16-11.19, 11.22-11.23, 11.26**

`EleFi.Domain.Alerts`: Parse Rules as data, rule evaluation as a pure function from
message text to `CaptureSuggestion?`, builtin rules for the major Indian issuers, and the
redacted corpus tests including the OTP-safety set (`SM4`).

The two ingest paths that need **no permission at all**: paste a message, and share a
message into EleFi from the messaging app. Both land on the same pre-filled capture form
that the SMS path will use later.

*Demoable:* paste an HDFC debit alert, get a pre-filled form, save it. Paste an OTP, get
nothing. All of it on a device that has never been asked for a permission.

> **Built before S38 deliberately.** If the Play declaration is refused, this slice is the
> feature rather than the fallback, and it is the only version of it that will ever exist
> on iOS. Ordering it first means that outcome costs a roadmap note, not a rewrite.

---

### S38 · SMS receiver and Suggestion Prompts

> **Built 2026-10-04, not yet verified on a device.** Receiver, prompts with Dismiss and
> Review, the "To confirm" inbox, the dashboard count, expiry on inbox load, and the in-app
> switch. Two departures: the receiver parses in-process under `goAsync` instead of handing
> off to WorkManager, because WorkManager would persist the message text (`SM1`); and the
> `SM1` *analyzer* is still to do, so the guarantee rests on the `SmsBody` ref struct plus a
> schema test. NFR-10.8's OEM-device test is outstanding.

**Blocked by:** S37 · **FR-11.1-11.2, 11.5-11.6, 11.10-11.14, 11.20-11.21, 11.24**

The Android `BroadcastReceiver`, sender-gated and handing straight off to WorkManager. The
`CaptureSuggestion` table with no body and no sender column. Suggestion Prompts,
duplicate suppression by fingerprint, expiry sweep, the in-app pending inbox, the
permission-rationale screen, and the in-app kill switch.

The analyzer that makes an SMS body unable to escape the receiver (`SM1`) ships here.

**FR-11.19's OTP corpus runs against every rule, and NFR-10.8's visible-failure behaviour
is tested on the OEM device.**

*Demoable:* `adb` a bank SMS to the phone, get a prompt within two seconds, tap Add, save
the transaction. Dismiss another and confirm nothing whatsoever remains.

---

### S39 · Parse Rule management
**Blocked by:** S38 · **FR-11.15, 11.17**

Settings UI listing rules, per-sender enable and disable, user-authored rules, and the
test-a-message pane sharing the S37 evaluator.

---

### S17 · FX rates
**Blocked by:** S6 · **FR-9.14, FX1-FX3**

`FxRate` table, nightly opportunistic fetch from a free keyless provider, manual override,
carry-forward, implied rates from cross-currency transactions. No fetch ever occurs for a
purely-INR user.

### S18 · Multi-currency UI
**Blocked by:** S17 · **FR-2.17-2.18**

### S19 · Richer reporting
**Blocked by:** S9 · **FR-5.9-5.11**

### S20 · Reconciliation
**Blocked by:** S6 · **FR-1.15**

---

## v2.0 - Quick Capture and web

### S21 · Smart defaults and review queue
**Blocked by:** S5 · **FR-3.1, 3.7, 3.10**

Learned defaults, `needs_review` lifecycle, swipeable review queue. **Ships before any
native surface** - the surfaces are worthless if the guesses are bad, and this is where the
guesses live.

### S22 · Notification capture
**Blocked by:** S21 · **FR-3.2-3.3, 3.8, FR-11.8**

Persistent notification with inline-reply amount and top-three-label actions. Reuses the
`INotificationSurface` that S38 already built, and adds the one-tap label buttons to
Suggestion Prompts at the same time.

### S23 · Quick Settings tile
**Blocked by:** S21 · **FR-3.4**

### S24 · Home-screen widget
**Blocked by:** S21 · **FR-3.5-3.6**

### S25 · Month in review
**Blocked by:** S19 · **FR-5.12-5.13**

### S26 · Web build
**Blocked by:** S13

Blazor WebAssembly reusing the Razor components, a browser storage engine behind the
existing repository seam, the web OAuth flow for Drive, responsive layouts, keyboard-first
bulk entry. No SMS, by nature.

> Scheduled after the schema has settled. Porting a moving data layer is the expensive way
> to do this. Under Blazor Hybrid this is a far smaller slice than it was under Expo, which
> is one of the reasons ADR-0011 leans that way.

---

## v3.0 - Expansion

| Slice | Blocked by | Notes |
|---|---|---|
| S27 · Floating bubble | S21 | `SYSTEM_ALERT_WINDOW`, foreground service. Must fail **visibly** when an OEM battery manager kills it |
| S28 · iOS | S13 | Widget, App Intent, Control Center, share sheet. No bubble, and **no SMS** - iOS forbids both. S37 is the whole feature there |
| S29 · True multi-device sync | S12, S26 | Per-device change logs, LWW merge, tombstones already in place |
| S30 · Play distribution | S15 | *Only if Play is ever wanted.* Store listing, Data Safety, the `RECEIVE_SMS` declaration (NFR-11.2a), privacy policy. **No billing work** - [ADR-0012](adr/0012-free-forever-voluntary-support.md) makes the app free permanently, so there is nothing to sell and no GST to register for |
| S31 · Transaction status | S6 | `Pending` / `Scheduled`; splits balance into cleared and projected |
| S32 · Recurring transactions | S31 | Rent, SIPs, RD installments |
| S33 · Budgets per label | S19 | Limits, rollover, alerts |
| S34 · Attachments | S12 | Receipt photos; watch backup size |
| S35 · Voice capture | S21 | "four fifty Zomato" |
| S36 · Statement import | S7 | Per-bank formats; a maintenance treadmill |
| S40 · Notification-listener ingest | S38 | **Built 2026-10-04, moved into v1.2** ([ADR-0014](adr/0014-payment-app-notification-ingest.md)). **FR-11.25, 11.27-11.29.** Allow-listed Payment Apps, merged with SMS for the same payment. Notification wording is best effort and needs real-device samples in the corpus |

---

## Dependency graph (v1.0)

```
S1 ─┬─ S2 ─┬─ S3 ──┬── S5 ──┬── S6 ──┬── S9 ──────────┐
    │      │       │        │        │                │
    │      └─ S4 ──┘        ├── S7 ──┴── S15 ─────────┼── S13  →  v1.0
    │                       │                         │
    │                       └── S8                    │
    └─ S10 ────────────────────────────────────────── ┤
                            S11 ── S12 ───────────────┘
```

**Critical path:** S1 → S2 → S3 → S5 → S7 → S15 → S13.

The critical path now runs through export rather than the dashboard, because S15 is the
last thing S13 waits on that is not already in flight. S4 and S10 run in parallel. S11
needs only S3, so it can be picked up whenever the path is blocked.

---

## Cut lines under pressure

If v1.0 runs long, drop in this order - the order is the point:

1. S16 (already v1.1)
2. Selectable export columns in S15 - ship a fixed column set; keep the filter fidelity
3. Dashboard richness in S9 - keep net worth tiles and spend-by-label, cut the rest
4. Design polish in S10 - keep the capture flow's motion, cut celebrations elsewhere
5. Container kinds - ship BankAccount, CreditCard, Cash, Wallet; defer FD/RD/PPF

**Never cut:** S2 (money primitives), S6 (balance correctness), S8 (audit and undo), or
S12 (backup). Those four are expensive or impossible to retrofit, and they are what makes
the data trustworthy - which is the only reason to keep entering it.

**S15 is cuttable in shape but not in existence.** An export that writes a fixed set of
columns is fine; an export that ignores the filters is not, because that is the whole
requirement.

**S37 is not cuttable from v1.2 while S38 remains in it.** Shipping the SMS receiver
without the permission-free path means the feature disappears entirely the moment Play
says no.
