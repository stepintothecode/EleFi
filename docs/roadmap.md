# EleFund — Roadmap

Work broken into **tracer-bullet slices**. Each slice cuts a narrow but complete path
through every layer — schema, domain, repository, UI, tests — and is demoable on its own.
Each declares the slices that **block** it.

Vocabulary from [CONTEXT.md](CONTEXT.md). Requirement IDs from
[functional.md](requirements/functional.md).

**The governing constraint:** v1 must reach daily real use as fast as it responsibly can.
The largest risk to this project is not a wrong technical decision — it is the project
stalling before it becomes a habit. Every cut below serves that.

---

## Release map

| Release | Theme | Rough size |
|---|---|---|
| **v1.0** | Daily driver — capture, containers, list, balances, backup | ~6–8 weeks |
| **v1.1** | Goals and CSV export | ~2–3 weeks |
| **v1.2** | Multi-currency features and richer reporting | ~2 weeks |
| **v2.0** | Quick Capture (notification, tile, widget) + web build | ~4–5 weeks |
| **v3.0** | Bubble, iOS, true sync, billing, public release | open-ended |

---

## v1.0 — Daily driver

### S1 · Foundation
**Blocked by:** nothing

Expo app skeleton, TypeScript strict, `op-sqlite` with SQLCipher, Drizzle, migration
runner at startup, lint boundaries (§4 of software-development.md), CI pipeline, Vitest
harness. A trivial table proves the migration path end to end.

*Demoable:* app launches, creates its database, runs a migration, passes CI.

---

### S2 · Money primitives
**Blocked by:** S1

`domain/money`: integer minor units, per-currency exponents, parse, format (including
`en-IN` lakh/crore grouping), banker's rounding, arithmetic. Round-trip property tests.
The lint rule banning money arithmetic outside this module.

*Demoable:* a test suite proving `parse(format(m)) == m` across generated inputs.

> Deliberately first. Every later slice depends on money being exactly right, and this is
> the cheapest possible moment to get it wrong and find out.

---

### S3 · Containers
**Blocked by:** S2

`Container` and its `Party` row. All seven kinds with their kind-specific fields. Create,
edit, archive. Container list and detail screens. Invariants C1–C5 as constraints and
tests. Opening balance and its as-of date.
**FR-1.1–1.5, 1.9–1.11**

*Demoable:* add an HDFC account and a credit card; see both listed with opening balances.

---

### S4 · Classification entities
**Blocked by:** S2

`Label` (2-level, `applies_to`), `Tag`, `App` (with `default_container_id`), and external
`Party`. Auto-create on first use. Ranked suggestion queries. Seeded starter labels.
**FR-2.7–2.10, FR-10.8, FR-10.9**

*Demoable:* type a new party name and see it persist and reappear as a suggestion.

---

### S5 · Capture — the core slice
**Blocked by:** S3, S4

The `Transaction` entity with dual amounts, derived kind, invariants T1–T9. The capture
form: amount-first, keypad focused, kind shortcut, party pickers over the shared pool,
label picker filtered by side, optional description/apps/tags. Save and add another.
**FR-2.1–2.16**

*Demoable:* record a Debit, a Credit, and a Self Transfer, offline, in under five seconds
each.

> **The single most important slice in the project.** Everything before it is scaffolding;
> everything after it is leverage.

---

### S6 · Derived balances and net worth
**Blocked by:** S5

Balance computation, liability and liquidity handling, net worth with liquid/locked/owed.
Property test: replaying all transactions equals the computed balance. Explicit tests for
`INV-CC` (card spending and bill payment) and `D1` (Self Transfers excluded from spend).
**FR-1.6–1.8**

*Demoable:* spend on the card, pay the bill, watch net worth stay correct throughout.

---

### S7 · Transaction list and filters
**Blocked by:** S5

Virtualised grouped list, the composable `TransactionFilter`, every filter dimension,
search, saved filters, filtered count and total. Container filter matches **either end**.
**FR-4.1–4.13, FR-4.16**

*Demoable:* filter to one bank, this FY, credits and debits only — the account statement.

---

### S8 · Edit, delete, audit trail
**Blocked by:** S5

Audit triggers on every table, `AuditEvent`, soft delete, timeline view, undo, recently
deleted with restore.
**FR-4.14–4.15, FR-8.1–8.7**

*Demoable:* edit an amount, see the before/after in the timeline; delete and restore.

---

### S9 · Dashboard
**Blocked by:** S6, S7

Net worth headline, three tiles, spend-by-label for the cycle with drill-down, income vs
spend, needs-review count, cycle selector. Charts follow the `dataviz` skill.
**FR-5.1–5.7, FR-3.9**

*Demoable:* open the app and understand your position in three seconds.

---

### S10 · Design system and motion
**Blocked by:** S1 · *runs alongside S3–S9*

Tokens, typography, light/dark themes, the elephant mascot, Reanimated primitives (spring
presets, shared-element transitions, list animations), haptics, skeleton loaders,
reduced-motion handling.
**NFR-2 in full**

*Demoable:* the capture flow at 60 fps with spring physics, and the same flow correct with
animations disabled at OS level.

> Sequenced alongside rather than after. Retrofitting motion onto finished screens is how
> apps end up feeling assembled rather than designed.

---

### S11 · Security and settings
**Blocked by:** S3

Biometric lock, auto-lock delay, app-switcher masking, home currency, cycle start day,
theme, wipe-all-data, encrypted database at rest.
**FR-10.1–10.6, FR-10.12, NFR-5.6–5.7, NFR-5.10**

*Demoable:* background the app, return, get a fingerprint prompt; the switcher shows no
amounts.

---

### S12 · Encrypted Google Drive backup
**Blocked by:** S5, S11

Google Sign-In (`drive.appdata` only), passphrase setup, Argon2id key derivation, recovery
code, AES-256-GCM snapshot, upload, rolling retention, automatic and manual backup,
restore with explicit replace warning, schema-version refusal.
**FR-9.1–9.13**

*Demoable:* back up, wipe the app, reinstall, restore, and verify the data is identical.

> The last thing to be cut, not the first. Every day of real use before this exists is a
> day of irreplaceable data living on exactly one device.

---

### S13 · Onboarding and hardening
**Blocked by:** S9, S10, S12

First-run flow reaching a captured transaction in under two minutes. Empty, loading, and
error states. Accessibility pass. 50,000-row performance benchmarks. Manual matrix on a
mid-range device and an OEM-skinned device. EAS internal-testing build.
**FR-10.7, NFR-1, NFR-6**

*Demoable:* **v1.0 installed on your own phone.**

---

## v1.1 — Goals and export

### S14 · Goals
**Blocked by:** S6

Both funding modes, container links, contributions, opening allocation, progress, status,
required-monthly, over-allocation warning, dashboard summary.
**FR-6.1–6.12, FR-5.8**

### S15 · CSV export
**Blocked by:** S7

Export the current filter to CSV. UTF-8 BOM, ISO dates, bare decimals, dual amounts,
selectable columns, share sheet, descriptive filename, streaming so 50k rows never block
the UI. Free JSON full export.
**FR-7.1–7.12, FR-7.14**

### S16 · Merge and tidy
**Blocked by:** S4

Merge duplicate parties and apps with transaction reassignment; privacy mode; maturity and
due-date reminders.
**FR-10.10–10.11, FR-1.13–1.14**

---

## v1.2 — Multi-currency and reporting

### S17 · FX rates
**Blocked by:** S6 · **FR-9.14, FX1–FX3**

`FxRate` table, nightly opportunistic fetch from a free keyless provider, manual override,
carry-forward, implied rates from cross-currency transactions. No fetch ever occurs for a
purely-INR user.

### S18 · Multi-currency UI
**Blocked by:** S17 · **FR-2.17–2.18**

Second currency fields when currencies differ, implied-rate display, currency-aware
aggregates everywhere.

### S19 · Richer reporting
**Blocked by:** S9 · **FR-5.9–5.11**

Net worth trend, month-over-month by label, top parties and apps.

### S20 · Reconciliation
**Blocked by:** S6 · **FR-1.15**

Enter a real balance, see the difference, create a balancing adjustment.

---

## v2.0 — Quick Capture and web

### S21 · Smart defaults and review queue
**Blocked by:** S5 · **FR-3.1, 3.7, 3.10**

Learned defaults (container by time of day, label by amount band), `needs_review`
lifecycle, swipeable review queue. **Ships before any native surface** — the surfaces are
worthless if the guesses are bad, and this is where the guesses live.

### S22 · Notification capture
**Blocked by:** S21 · **FR-3.2–3.3, 3.8**

Persistent notification with inline-reply amount and top-three-label actions. *The
highest value-to-effort item in the entire roadmap.*

### S23 · Quick Settings tile
**Blocked by:** S21 · **FR-3.4**

### S24 · Home-screen widget
**Blocked by:** S21 · **FR-3.5–3.6**

Amount field, recent-label shortcuts, net worth and cycle spend display.

### S25 · Month in review
**Blocked by:** S19 · **FR-5.12–5.13**

### S26 · Web build
**Blocked by:** S13

Storage engine for the browser behind the existing repository seam, web OAuth flow for
Drive, responsive layouts, keyboard-first bulk entry.

> Scheduled after the schema has settled. Porting a moving data layer is the expensive way
> to do this.

---

## v3.0 — Expansion

| Slice | Blocked by | Notes |
|---|---|---|
| S27 · Floating bubble | S21 | `SYSTEM_ALERT_WINDOW`, foreground service. Must fail **visibly** when an OEM battery manager kills it |
| S28 · iOS | S13 | Widget, App Intent, Control Center, share sheet. No bubble — iOS forbids overlays |
| S29 · True multi-device sync | S12, S26 | Per-device change logs, LWW merge, tombstones already in place |
| S30 · Billing and public release | S15 | One-time unlock for filtered export; full export stays free. Store listing, Data Safety, privacy policy, GST |
| S31 · Transaction status | S6 | `Pending` / `Scheduled`; splits balance into cleared and projected |
| S32 · Recurring transactions | S31 | Rent, SIPs, RD installments |
| S33 · Budgets per label | S19 | Limits, rollover, alerts |
| S34 · Attachments | S12 | Receipt photos; watch backup size |
| S35 · Voice capture | S21 | "four fifty Zomato" |
| S36 · Statement import | S7 | Per-bank formats; a maintenance treadmill |

---

## Dependency graph (v1.0)

```
S1 ─┬─ S2 ─┬─ S3 ──┬── S5 ──┬── S6 ──┬── S9 ──┐
    │      │       │        │        │        ├── S13  →  v1.0
    │      └─ S4 ──┘        ├── S7 ──┘        │
    │                       ├── S8            │
    └─ S10 ─────────────────┴─────────────────┤
                            S11 ── S12 ───────┘
```

**Critical path:** S1 → S2 → S3 → S5 → S6 → S9 → S13.

S4 and S10 run in parallel with the path. S11 needs only S3, so it can be picked up
whenever the critical path is blocked.

---

## Cut lines under pressure

If v1.0 runs long, drop in this order — the order is the point:

1. S16 (already v1.1)
2. Dashboard richness in S9 — keep net worth tiles and spend-by-label, cut the rest
3. Design polish in S10 — keep the capture flow's motion, cut celebrations elsewhere
4. Container kinds — ship BankAccount, CreditCard, Cash, Wallet; defer FD/RD/PPF

**Never cut:** S2 (money primitives), S6 (balance correctness), S8 (audit and undo), or
S12 (backup). Those four are the ones that are expensive or impossible to retrofit, and
they are what makes the data trustworthy — which is the only reason to keep entering it.
