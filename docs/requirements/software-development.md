# EleFund — Software Development Requirements

How the software gets built: stack, structure, testing, tooling, workflow, and what
"done" means. Complements [functional.md](functional.md) (what it does) and
[non-functional.md](non-functional.md) (how well).

---

## 1. Stack

| Layer | Choice | Rationale |
|---|---|---|
| Language | **TypeScript** (`strict`) | One language across app, domain, and tooling |
| Framework | **Expo** (React Native), Expo Router | One codebase → Android, iOS, web; native escape hatch via config plugins |
| UI | React Native primitives + a project design system | No heavyweight UI kit; the visual identity is the product |
| Animation | **React Native Reanimated 3** + Gesture Handler | UI-thread worklets — the only way to meet NFR-2 |
| Vector/rich motion | Lottie and/or Rive; `react-native-svg` | Mascot and celebration moments |
| Local database | **SQLite** via `op-sqlite` (SQLCipher build) | Fast, synchronous where useful, encrypted at rest |
| ORM / query | **Drizzle ORM** | Schema is the source of truth for types; SQL stays legible |
| Migrations | `drizzle-kit`, forward-only, versioned | See §6 |
| State | TanStack Query over the repository layer + Zustand for UI state | Server-state patterns work well over an async local DB |
| Forms | React Hook Form + Zod | One schema validates input and types it |
| Money | Integer minor units + a decimal library for FX only | NFR-3.1 |
| Dates | `date-fns` / Temporal polyfill, UTC-safe | NFR-10.6 |
| Auth | Google Sign-In (`@react-native-google-signin`) — **backup only** | No app account exists |
| Cloud | Google Drive REST, `drive.appdata` scope only | ADR-0006 |
| Crypto | Platform-native AES-256-GCM + Argon2id | NFR-5.2 |
| Testing | Vitest (domain/unit), fast-check (properties), Maestro (E2E) | §5 |
| Build/release | EAS Build + EAS Update | Managed signing, OTA for JS-only fixes |
| CI | GitHub Actions | §7 |
| Lint/format | ESLint (typescript-eslint strict) + Prettier | |

**No backend.** No server, no hosted database, no API. If a future feature appears to
need one, that requires an ADR — it would reverse [ADR-0001](../adr/0001-local-first-no-backend.md),
which is the load-bearing decision of the whole project.

---

## 2. Repository structure

```
elefund/
├── .claude/
│   ├── CLAUDE.md                   project instructions
│   └── skills/decision-log/        session decision-log skill
├── docs/
│   ├── CONTEXT.md                  domain glossary — read first
│   ├── requirements/
│   ├── adr/
│   ├── prompts/                    decision logs, YYYY-MM-DD-<topic>.md
│   └── roadmap.md
├── app/                            Expo Router routes — thin, UI only
│   ├── (tabs)/
│   ├── capture/
│   ├── containers/
│   ├── transactions/
│   ├── goals/
│   └── settings/
├── src/
│   ├── domain/                     PURE. no React, no SQLite, no platform
│   │   ├── money/                  minor units, formatting, parsing, rounding
│   │   ├── transaction/            kind derivation, invariants
│   │   ├── container/              kind traits, balance rules
│   │   ├── label/  goal/  filter/  fx/
│   ├── data/
│   │   ├── schema/                 Drizzle table definitions (source of truth)
│   │   ├── migrations/
│   │   ├── repositories/           ONLY place that touches the DB
│   │   └── triggers/               audit triggers as SQL
│   ├── features/                   feature-scoped hooks + components
│   ├── ui/                         design system: tokens, primitives, motion
│   ├── services/                   drive, crypto, fx, notifications, export
│   └── platform/                   android/ ios/ web/ — native surfaces
├── modules/                        Expo native modules (widget, tile, bubble)
└── e2e/
```

### Dependency rule

```
app/  →  features/  →  domain/
                    →  data/  →  domain/
          ui/       →  (nothing but React Native)
     services/      →  domain/
```

**`domain/` imports nothing from the layers above it.** It has no React import, no SQLite
import, no `expo-*` import. That is what makes it testable in milliseconds without a
device, and it is the rule most likely to erode under deadline pressure — so it is
enforced by lint (§4), not by discipline.

---

## 3. Module design

Following the `codebase-design` vocabulary: prefer **deep modules** — a small interface
over substantial functionality.

**Seams — the intended test boundaries.** Keep them few:

| Seam | What it isolates |
|---|---|
| `repositories/` | All persistence. Swap for in-memory in tests |
| `services/drive` | All network. Fake in tests |
| `services/crypto` | Key handling. Deterministic test double |
| `services/fx` | Rate fetching. Fixture rates in tests |
| `domain/*` | No seam needed — pure functions, tested directly |

Most tests should exercise **the repository seam and above**, against a real in-memory
SQLite. That gives near-integration confidence at unit-test speed and avoids mocking the
database — mocking a database mostly tests the mock.

**Rules**

- No component or hook constructs SQL or imports Drizzle. Repositories only.
- Repositories return domain types, never raw rows.
- `user_id` scoping and `deleted_at IS NULL` are applied in one base query builder.
- Money never becomes a `number` outside `domain/money`.
- New domain vocabulary lands in [CONTEXT.md](../CONTEXT.md) in the same commit.

---

## 4. Code quality gates

Enforced in CI; failing any of these fails the build.

| Gate | Rule |
|---|---|
| TypeScript | `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`. No `any` in `domain/` or `data/` |
| Import boundaries | `eslint-plugin-boundaries` enforcing §2's dependency rule |
| No float money | Custom lint rule: arithmetic on anything typed `Money` outside `domain/money` is an error |
| No logging PII | Custom lint rule banning amounts, party names, container names in `console.*` |
| Formatting | Prettier, checked not fixed |
| Dead code | `knip` |
| Dependency audit | `npm audit --audit-level=high` |
| Bundle size | Fails on >10% growth without an explicit override |

---

## 5. Testing strategy

The distribution is deliberate: **most confidence comes from the domain layer**, because
that is where correctness actually lives.

### 5.1 Domain unit tests — Vitest

Pure functions, no I/O, milliseconds. Target ≥ 90% coverage.

Every invariant in [domain-model.md](domain-model.md) has a test named for its ID
(`T4`, `INV-CC`, `GL3`, …), so a failure names the rule it broke.

### 5.2 Property-based tests — fast-check

Reserved for the properties that must hold universally:

```
∀ transactions:  Σ spend_by_label(f) == total_spend(f)          -- NFR-3.9
∀ containers:    replaying all transactions == balance(c)        -- NFR-3.2
∀ transactions:  same currency ⟹ source_amount == dest_amount    -- T4
∀ transactions:  kind(t) is derivable and never INVALID          -- T1
∀ card cycles:   spend then pay bill ⟹ net worth unchanged       -- INV-CC
∀ money:         parse(format(m)) == m                           -- round-trip
∀ backups:       decrypt(encrypt(db, k), k) == db                -- FR-9.3
```

These are the tests most likely to catch the bug that matters — the one nobody thought to
write an example for.

### 5.3 Repository tests

Real SQLite, in memory. Cover migrations, constraints, triggers, and the scoping rules.
**Audit triggers are tested here**, since they are database behaviour and cannot be
verified from application code.

### 5.4 E2E — Maestro

Small and stable. Only the flows whose breakage would be catastrophic:

1. Onboarding → first container → first transaction
2. Capture Debit / Credit / Self Transfer; verify all affected balances
3. Filter, save the filter, reapply it
4. Edit → verify timeline; delete → undo → verify balances restored
5. Enable backup → back up → wipe → restore → verify data identical
6. Capture in aeroplane mode

### 5.5 Manual test matrix

Per release, on real hardware: one mid-range Android, one OEM-skinned device (Xiaomi or
Oppo — see NFR-10.3), at 200% font scale, and with animations disabled at OS level.

### 5.6 Performance regression

Benchmarks from NFR-1 run in CI against the 50,000-row seeded dataset. A >20% regression
fails the build.

---

## 6. Database migrations

| Rule | Why |
|---|---|
| Forward-only, numbered, immutable once released | Down-migrations on user devices are a fiction — nobody can test the rollback of data they never saw |
| Every migration has a test asserting data preservation | A migration that loses data is unrecoverable on a user's device |
| `schema_version` recorded in every backup | Restoring a newer backup into an older app is refused (FR-9.12) |
| Wide changes use expand → migrate → contract | Keeps every intermediate state runnable |
| Migrations run at startup, before the first render, inside a transaction | A half-migrated database must never be reachable |
| A pre-migration local snapshot is taken for any destructive migration | Last line of defence |

---

## 7. CI/CD

```
on: pull_request
  typecheck → lint → domain tests → repository tests
  → migration tests → property tests → bundle-size check
  → dependency audit

on: push to main
  everything above → EAS Build (internal profile) → Maestro E2E on the build

on: tag v*
  everything above → EAS Build (production) → Play internal testing track
```

- Trunk-based development on `main`; short-lived branches.
- `main` is always releasable.
- Conventional Commits, referencing FR/NFR IDs where applicable.
- Semantic versioning; a schema change bumps the minor version at minimum.

---

## 8. Definition of Done

A slice is done when **all** of these hold. Not most.

- [ ] Meets its stated FR IDs
- [ ] Domain logic is pure and unit tested; new invariants have named tests
- [ ] Property tests added where a universal property exists
- [ ] Repository tests cover new queries, constraints, and triggers
- [ ] Migration written, tested for data preservation, and run against a real backup
- [ ] Works offline — verified in aeroplane mode
- [ ] Animations meet NFR-2, including reduced-motion behaviour
- [ ] Accessibility: labelled for screen readers, 200% font scale, AA contrast
- [ ] Both light and dark themes verified
- [ ] Empty, loading, and error states designed and implemented
- [ ] No new lint suppressions; no new `any`
- [ ] Performance benchmarks still within NFR-1
- [ ] New vocabulary added to `CONTEXT.md`
- [ ] An ADR written if the decision was hard to reverse
- [ ] Manually exercised on a real device
- [ ] Demoable end-to-end on its own

---

## 9. Development environment

```
Node          22 LTS  (pinned via .nvmrc)
Package mgr   pnpm    (pinned via packageManager)
Expo SDK      current stable
JDK           17
Android SDK   via Android Studio; one physical mid-range device for testing
Xcode         only when the iOS target begins
```

Everything is reproducible from a clean clone with `pnpm install && pnpm dev`. Any step
that cannot be scripted gets written into `docs/setup.md` rather than living in someone's
memory.

---

## 10. Native modules

Quick-capture surfaces (v2+) need native code, written as **Expo config plugins + native
modules** so `npx expo prebuild` stays reproducible and nothing is hand-edited inside
`android/`.

| Surface | Android API | Notes |
|---|---|---|
| Notification, inline reply | `NotificationCompat` + `RemoteInput` | No special permission beyond notifications |
| Quick Settings tile | `TileService` | API 24+ |
| Home widget | `AppWidgetProvider` + `RemoteViews`, or Glance in Kotlin | Needs its own layout work |
| Floating bubble | `WindowManager` + `SYSTEM_ALERT_WINDOW` | Special permission; foreground service; see NFR-10.3 |
| Share target | Intent filter | |

**Native surfaces write to the same SQLite database through the same repository rules.**
A widget that writes its own SQL would bypass audit triggers and invariant checks — the
exact class of bug that produces silently wrong balances.

---

## 11. Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| **Abandonment** — the project stalls before daily use | Fatal | Ship the thinnest daily-usable slice first; every slice demoable; v1 scoped to ~6–8 weeks |
| TypeScript unfamiliarity slows progress | High | Domain layer is plain functions and plain data — the least framework-dependent code to learn in |
| Silent balance corruption | Fatal to trust | Derived balances, DB-level invariants, property tests, audit trail |
| Backup unrecoverable (lost passphrase) | Severe, user-facing | Recovery code at setup, blunt warnings, key cached in keystore |
| OEM battery managers kill the bubble | Medium | Notification-first ladder; bubble deferred to v3; visible failure not silent |
| Scope creep from an 8-point brief | High | Roadmap with explicit cut lines; "deliberately absent" lists in the docs |
| SQLite in the browser (web target) | Medium | Web deferred to v2, behind the repository seam so the storage engine can differ |
| Google changes Drive API or scope policy | Medium | `drive.appdata` only; local encrypted export (FR-9.14) as an independent path |
| Play policy blocks a native surface | Low–Medium | In-app capture always works; every native surface is additive |

---

## 12. Documentation maintenance

| Document | Updated when |
|---|---|
| `CONTEXT.md` | New domain vocabulary — same commit, always |
| `docs/requirements/*` | A requirement changes. Amend, never silently rewrite: keep IDs stable |
| `docs/adr/*` | A hard-to-reverse decision is made. Superseding ADRs link both ways |
| `docs/roadmap.md` | A slice completes or is re-cut |

Requirement IDs are permanent. A superseded requirement is marked superseded and a new ID
is issued — so a test, ticket, or commit referencing `FR-2.13` always means the same
thing.
