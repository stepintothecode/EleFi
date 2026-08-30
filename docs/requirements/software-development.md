# EleFi - Software Development Requirements

How the software gets built: stack, structure, testing, tooling, workflow, and what
"done" means. Complements [functional.md](functional.md) (what it does) and
[non-functional.md](non-functional.md) (how well).

**Rewritten 2026-08-29** for the move from Expo/TypeScript to C# on .NET MAUI. The
decision and its costs are in
[ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md). Requirement IDs in the other
documents are unaffected; this document describes means, not requirements.

---

## 1. Stack

| Layer | Choice | Rationale |
|---|---|---|
| Language | **C# 14** on **.NET 10** (LTS) | The developer's fluent language. ADR-0009 |
| App framework | **.NET MAUI**, Android first | Single C# codebase reaching every Android native surface |
| UI layer | **Blazor Hybrid**, components in `EleFi.Ui` | HTML/CSS is a far better substrate for a bespoke design system than XAML, and it makes the v2 web target near-free. [ADR-0011](../adr/0011-blazor-hybrid-over-native-xaml.md) |
| UI presentation | **MVVM** via `CommunityToolkit.Mvvm` source generators | Observable view models, no hand-written `INotifyPropertyChanged` |
| Motion | One shared motion module: Web Animations API + CSS compositor properties under Blazor; `SKCanvasView` interpolation under native MAUI | NFR-2, and NFR-2.3a keeps constants in one place |
| Mascot | **Rive** via a JS module in `wwwroot/js/`, behind `IMascotService` | State-machine driven reactions. Lottie is the fallback runtime; the interface hides which is in use |
| Vector | Inline SVG and CSS | Icons and celebration moments. No component kit |
| Local database | **SQLite** + **SQLCipher** via `SQLitePCLRaw.bundle_e_sqlcipher` | Encrypted at rest. NFR-5.10 |
| Data access | **EF Core 10** with `Microsoft.Data.Sqlite` | Model is the source of truth; migrations generated from it. NFR-8.6 |
| Migrations | EF Core migrations, forward-only, applied at startup | §6 |
| Money | `readonly record struct Money(long Minor, Currency Ccy)` | Integer minor units, never `decimal`, never `double`. NFR-3.1 |
| Decimals (FX only) | `decimal` inside `Domain.Fx` only | FX rates are ratios, not money. FX1 |
| Dates | `DateOnly` for `occurred_on`, `DateTimeOffset` (UTC) for instants | NFR-10.6. The type distinction enforces §1 of domain-model.md |
| Auth | Google OAuth 2.0 authorization code + **PKCE** via `WebAuthenticator`, custom URI scheme | Installed-app flow, no client secret on device. Backup only - no app account exists |
| Cloud | `Google.Apis.Drive.v3`, `drive.appdata` scope only | ADR-0006 |
| Crypto | `System.Security.Cryptography.AesGcm` (BCL) + `Konscious.Security.Cryptography.Argon2` | AES-256-GCM and Argon2id. NFR-5.2. Argon2 is not in the BCL |
| Key storage | Android Keystore via `SecureStorage` | NFR-5.3 |
| SMS ingest | Android `BroadcastReceiver` on `SMS_RECEIVED`, behind `ISmsAlertSource` | Android only. ADR-0010, NFR-10.7 |
| Notifications | `NotificationCompat` + `RemoteInput` (AndroidX), behind `INotificationSurface` | Suggestion Prompts and quick capture share one abstraction |
| Background work | AndroidX **WorkManager** for backup and expiry sweeps | Survives OEM battery managers better than a bare service. NFR-10.3 |
| Testing | **xUnit v3**, **CsCheck** (properties), **bUnit** (Razor), **NSubstitute** | §5 |
| Architecture tests | **NetArchTest** | §2's dependency rule, enforced not trusted |
| Build/release | `dotnet publish -f net10.0-android`, GitHub Actions | §7 |
| Lint/format | `dotnet format` + Roslyn analyzers, `.editorconfig` as the single style source | §4 |
| Package versions | `Directory.Packages.props`, central management, no floating versions | §4 |
| Shared MSBuild | `Directory.Build.props` at the root, plus one each in `src/` and `tests/` | §2 |
| Solution | `EleFi.slnx` (XML solution format) | Readable and mergeable, unlike `.sln` |

**No backend.** No server, no hosted database, no API. If a future feature appears to
need one, that requires an ADR - it would reverse
[ADR-0001](../adr/0001-local-first-no-backend.md), which is the load-bearing decision of
the whole project.

**Every third-party package with network access needs an ADR** (NFR-5.11). Today the list
is exactly one: `Google.Apis.Drive.v3`.

---

## 2. Solution structure

Clean Architecture, five production projects. Project granularity is the point: a
reference that should not exist becomes a compiler error rather than a review comment.

**This section describes what is on disk.** `dotnet build` and `dotnet test` both pass
against it today.

```
EleFi.slnx                          XML solution format, /src/ and /tests/ folders
global.json                         pins the SDK
Directory.Build.props               settings shared by every project
Directory.Packages.props            every package version, decided once
.editorconfig                       the single source of code style

docs/                               CONTEXT.md, requirements/, adr/, prompts/, roadmap.md, setup.md

src/
  Directory.Build.props             production-only settings: XML docs required
  EleFi.Domain/                     PURE. references NOTHING but the BCL
    Money/                          minor units, currency exponents, parse, format
    Transactions/                   kind derivation, invariants T1-T9
    Containers/                     kind traits, balance rules, INV-CC
    Labels/  Goals/  Filters/  Fx/
    Alerts/                         Parse Rule evaluation: string in, suggestion out
  EleFi.Application/                use cases, and the ports the outside world implements
    Abstractions/                   ITransactionRepository, IBackupStore, IClock, ...
    Transactions/  Containers/  Export/  Suggestions/
  EleFi.Infrastructure/             every adapter. the only project that does I/O
    Persistence/                    EleFiDbContext, configurations, migrations, triggers
    Persistence/Repositories/       ONLY place that touches the database
    Drive/  Crypto/  Fx/  Export/
  EleFi.Ui/                         Razor components and view models. net10.0, no platform
    Components/                     routable pages, layout, design-system primitives
    ViewModels/                     CommunityToolkit.Mvvm
  EleFi.App/                        MAUI host shell. net10.0-android
    Platforms/Android/Sms/          TransactionAlertReceiver : BroadcastReceiver
    Platforms/Android/Notifications/
    Platforms/Android/Widgets/      v2 surfaces: widget, tile, bubble
    wwwroot/                        index.html, app.css, js/ (interop modules)
    Resources/                      icons, splash, fonts

tests/
  Directory.Build.props             test-only settings: xUnit usings, XML docs waived
  EleFi.Domain.Tests/               fast, pure, no I/O
  EleFi.Application.Tests/          use cases against fakes
  EleFi.Infrastructure.Tests/       real in-memory SQLite; triggers and migrations
  EleFi.Ui.Tests/                   bUnit, for components with real logic
  EleFi.Architecture.Tests/         guards this section and the layout below
  EleFi.Properties.Tests/           CsCheck universal properties (section 5.2)
```

### Why EleFi.Ui is separate from EleFi.App

`EleFi.App` targets `net10.0-android`, and a test project cannot reference it on a desktop
runner. Every Razor component and view model therefore lives in `EleFi.Ui`, which targets
plain `net10.0`, so bUnit can reach it without a device or an emulator.

`EleFi.App` keeps only what is genuinely platform-bound: the MAUI shell, `MauiProgram`,
`wwwroot`, resources, and `Platforms/Android`. If something there is worth testing, that is
the signal it belongs in `EleFi.Ui`.

### Layout rules, enforced by tests

`EleFi.Architecture.Tests` fails the build on any of these:

| Rule | Test |
|---|---|
| Every project in `src/` has `tests/<name>.Tests`, or is listed with a reason | `Every_src_project_has_a_matching_test_project` |
| Every test project matches a project in `src/`, or is listed as cross-cutting with a reason | `Every_test_project_matches_a_src_project` |
| `tests/X.Tests/Foo/BarTests.cs` has a matching `src/X/Foo/Bar.cs` or `Bar.razor` | `Every_test_file_mirrors_the_path_of_the_file_it_covers` |

**The mirror rule is one-directional on purpose.** Every test file must map to a source
file, so finding the tests for a file never needs a search. The reverse is not required:
not every file warrants its own test file, and demanding one produces empty tests written
to satisfy a rule.

Two exemption lists exist, both in `RepositoryLayoutTests`, both requiring a written
reason: `ProjectsWithoutTests` (currently `EleFi.App`) and `CrossCuttingTestProjects`
(currently `EleFi.Architecture`, `EleFi.Properties`).

### Other languages

The `src/` and `tests/` split applies to every language, not only C#. JavaScript for
Blazor interop lives at `src/EleFi.App/wwwroot/js/`, which is already under `src/`; its
tests go at `tests/EleFi.App.Tests/js/` when there are any. A language that needs its own
toolchain gets its own project folder under `src/`, never a folder at the repository root.

### Dependency rule

```
EleFi.App  ->  EleFi.Ui  ->  EleFi.Application  ->  EleFi.Domain
     |                              ^
     +---->  EleFi.Infrastructure --+   (implements Application's ports)
```

- **`EleFi.Domain` references nothing but the BCL.** No EF Core, no MAUI, no
  `SQLitePCLRaw`, no `Android.*`. It compiles and tests on a desktop runner in
  milliseconds, and that property is what keeps correctness cheap to verify.
- **`EleFi.Application` defines the interfaces; `EleFi.Infrastructure` implements them.**
  Dependencies point inward. `EleFi.App` composes them at startup and is the only project
  that knows both.
- **`EleFi.App` never references `EleFi.Infrastructure` types directly**, only its
  registration extension method. A view model that can see `DbContext` will eventually
  use it.
- **`EleFi.Ui` never references `EleFi.Infrastructure` at all.** It talks to abstractions
  from `EleFi.Application`, which is what makes bUnit tests possible without a database.

Enforced by `EleFi.Architecture.Tests`, not by discipline. Discipline is what erodes at
week six.

> **Naming trap, already hit.** `EleFi.Application` collides with MAUI's `Application`
> type inside `EleFi.App`, so `App.xaml.cs` writes
> `Microsoft.Maui.Controls.Application` in full. Qualifying it is the smallest fix;
> renaming the layer would be the larger one.

---

## 3. Module design

Following the `codebase-design` vocabulary: prefer **deep modules** - a small interface
over substantial functionality.

**Seams - the intended test boundaries.** Keep them few:

| Seam | Interface | What it isolates |
|---|---|---|
| Persistence | `ITransactionRepository`, `IContainerRepository`, ... | All database access. Real in-memory SQLite in tests, not a mock |
| Drive | `IBackupStore` | All network. Fake in tests |
| Crypto | `IBackupCipher`, `IKeyVault` | Key handling. Deterministic double |
| FX | `IRateSource` | Rate fetching. Fixture rates |
| SMS | `ISmsAlertSource` | The Android receiver. Tests feed message strings directly |
| Notifications | `INotificationSurface` | Prompt posting. Recording double |
| Clock | `IClock` | `DateTimeOffset.UtcNow`. Nothing in the app calls it directly |
| Export | `ITransactionExporter` | Serialisation. Tested against the list's own query |
| Mascot | `IMascotService` | The Rive runtime and all `IJSRuntime` interop. A recording double in bUnit tests |
| Outbound links | `ILinkOpener` | `Browser.OpenAsync`. Keeps FR-12.4 testable and stops an `<a href>` hijacking the WebView |
| `EleFi.Domain.*` | none needed | Pure functions and values, tested directly |

Most tests should exercise **the repository seam and above**, against a real in-memory
SQLite. That gives near-integration confidence at unit-test speed and avoids mocking the
database - mocking a database mostly tests the mock.

**Rules**

- No view model, component, or page constructs SQL or touches `DbContext`. Repositories
  only.
- Repositories return domain types, never EF entities that could lazy-load from a view.
- `user_id` scoping and `deleted_at IS NULL` are applied in **one** global query filter,
  never remembered per query.
- `Money` is a value type with no implicit conversion to a numeric type. Arithmetic on it
  lives in `EleFi.Domain.Money` and nowhere else.
- Parse Rules are **data**. Supporting a new bank is a row and a test, never a class
  (NFR-8.11).
- **Nothing in `EleFi.Domain.Alerts` can perform I/O**, which is what makes SMS parsing
  testable without a phone, a SIM, or a permission.
- **No Razor component touches `IJSRuntime` directly.** Interop lives behind an interface
  in `EleFi.Application/Abstractions`, implemented once, and every implementation is
  `IAsyncDisposable` because a `BlazorWebView` leaks handles readily.
- **No outbound link is an `<a href>`.** Under Blazor Hybrid that navigates the host
  WebView and strands the user in the app. Everything goes through `ILinkOpener`.
- New domain vocabulary lands in [CONTEXT.md](../CONTEXT.md) in the same commit.

---

## 4. Code quality gates

Enforced in CI; failing any of these fails the build. Style lives in `.editorconfig` and
nowhere else, so there is one answer rather than an argument.

| Gate | Rule |
|---|---|
| Nullability | `<Nullable>enable</Nullable>` solution-wide in `Directory.Build.props` |
| Warnings | `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisLevel>latest-all</AnalysisLevel>` |
| Suppressions | No `#pragma warning disable` and no `SuppressMessage` without a comment naming the reason |
| Layer boundaries | `EleFi.Architecture.Tests` (NetArchTest) enforcing §2 |
| No float money | Analyzer: `double`/`float` in any type or signature under `Domain.Money`, `Transactions`, or `Containers` is an error |
| Money encapsulation | Analyzer: arithmetic operators on `Money` outside `Domain.Money` are an error |
| No logging PII | Analyzer banning amounts, party names, container names, and any SMS body member in a log call (NFR-5.9) |
| **No SMS retention** | Analyzer: the SMS body type is a `ref struct` and cannot be assigned to a field, boxed, captured, or serialised (`SM1`, NFR-5.13) |
| Formatting | `dotnet format --verify-no-changes` |
| Dependency audit | `dotnet list package --vulnerable --include-transitive`, failing on any high or critical |
| Package versions | `Directory.Packages.props` central management; no floating versions |
| APK size | Fails on >10% growth without an explicit override |

**The SMS retention gate is the one worth building carefully.** `SM1` is absolute, so it
should be enforced by the type system rather than by review. A body that cannot escape the
stack cannot be logged, stored, or backed up by accident.

---

## 5. Testing strategy

The distribution is deliberate: **most confidence comes from the domain layer**, because
that is where correctness actually lives.

### 5.1 Domain unit tests - xUnit v3

Pure functions, no I/O, milliseconds. Target ≥ 90% coverage.

Every invariant in [domain-model.md](domain-model.md) has a test named for its ID
(`T4`, `INV-CC`, `GL3`, `SM2`, `X1`, ...), so a failure names the rule it broke.

### 5.2 Property-based tests - CsCheck

Reserved for the properties that must hold universally:

```
∀ transactions:  Σ spend_by_label(f) == total_spend(f)          -- NFR-3.9
∀ containers:    replaying all transactions == balance(c)        -- NFR-3.2
∀ transactions:  same currency ⟹ source_amount == dest_amount    -- T4
∀ transactions:  kind(t) is derivable and never INVALID          -- T1
∀ card cycles:   spend then pay bill ⟹ net worth unchanged       -- INV-CC
∀ money:         parse(format(m)) == m                           -- round-trip
∀ backups:       decrypt(encrypt(db, k), k) == db                -- FR-9.3
∀ filters:       rows(export(f)) == rows(list(f)), in order      -- X1, NFR-3.11
∀ suggestions:   no unconfirmed suggestion moves any balance     -- SM2, NFR-3.12
```

These are the tests most likely to catch the bug that matters - the one nobody thought to
write an example for. The last two are new, and `X1` is the reason the export cannot drift
from the list.

### 5.3 Infrastructure tests

Real SQLite, in memory, with SQLCipher active. Cover migrations, constraints, triggers,
global query filters, and the scoping rules. **Audit triggers are tested here**, since
they are database behaviour and cannot be verified from application code.

### 5.4 Parse Rule corpus tests

A fixture corpus of real bank message formats, per issuer, checked in **with amounts and
identifiers redacted**. Every builtin rule is tested against it for:

1. **Matches** - the messages it should parse, and the exact suggestion each produces.
2. **Non-matches** - messages from the same sender it must ignore.
3. **OTP safety** - a shared set of real OTP formats no rule may ever match (`SM4`,
   NFR-5.15). This runs against *every* rule, builtin or user-defined, not just its own.

A new bank is a corpus addition plus a rule row. That is the whole cost, by design.

### 5.5 Razor component tests - bUnit

Only for components with real logic: the filter bar, the amount field, the export button's
count. Not for layout.

### 5.6 E2E - Appium

Small and stable. Only the flows whose breakage would be catastrophic:

1. Onboarding → first container → first transaction
2. Capture Debit / Credit / Self Transfer; verify all affected balances
3. Filter, save the filter, reapply it
4. **Filter, export, and diff the CSV against the on-screen list** (`X1` end to end)
5. Edit → verify timeline; delete → undo → verify balances restored
6. Enable backup → back up → wipe → restore → verify data identical
7. Capture in aeroplane mode
8. Feed a test SMS via `adb`; confirm the prompt, then the transaction; verify a dismissed
   suggestion leaves no trace

### 5.7 Manual test matrix

Per release, on real hardware: one mid-range Android, one OEM-skinned device (Xiaomi or
Oppo - see NFR-10.3), at 200% font scale, and with animations disabled at OS level. The
SMS receiver is exercised on the OEM device specifically, because that is where it dies.

### 5.8 Performance regression

Benchmarks from NFR-1 run in CI against the 50,000-row seeded dataset. A >20% regression
fails the build. **Cold start (NFR-1.1) is measured on a real device every release**, not
on an emulator, because an emulator will not tell you the truth about a WebView.

---

## 6. Database migrations

| Rule | Why |
|---|---|
| Forward-only, EF Core migrations, immutable once released | Down-migrations on user devices are a fiction - nobody can test the rollback of data they never saw |
| Every migration has a test asserting data preservation | A migration that loses data is unrecoverable on a user's device |
| Audit triggers are created **in** the migration that creates the table | A table that exists for one release without its trigger has a permanent hole in its history |
| `schema_version` recorded in every backup | Restoring a newer backup into an older app is refused (FR-9.12) |
| Wide changes use expand → migrate → contract | Keeps every intermediate state runnable |
| Migrations run at startup, before the first render, inside a transaction | A half-migrated database must never be reachable |
| A pre-migration local snapshot is taken for any destructive migration | Last line of defence |
| The SQLCipher key is supplied before any migration runs | An unkeyed connection against an encrypted file reports corruption, not a wrong password |

---

## 7. CI/CD

```
on: pull_request
  restore → build (warnings as errors) → format check
  → domain tests → application tests → architecture tests
  → infrastructure + migration tests → property tests
  → parse-rule corpus tests → vulnerability audit → APK size check

on: push to main
  everything above → dotnet publish -f net10.0-android (Release)
  → Appium E2E on the emulator + one attached device

on: tag v*
  everything above → signed APK → GitHub Release asset
```

**Distribution is direct sideloading**, not Google Play: a signed APK attached to a GitHub
Release on a semantic version tag, consumed via Obtainium and IzzyOnDroid. That is what
keeps hosting and distribution at zero cost, and it is why the SMS permission risk in §11
is conditional rather than blocking. Going to Play later is additive - it costs an AAB
target and a Permissions Declaration (NFR-11.2a), not a rework.

- Trunk-based development on `main`; short-lived branches.
- `main` is always releasable.
- Conventional Commits, referencing FR/NFR IDs where applicable.
- Semantic versioning; a schema change bumps the minor version at minimum.
### Release secrets

The tag-triggered release job needs these repository secrets. The workflow itself is
written separately; this is the contract it depends on.

| Secret | What it holds |
|---|---|
| `KEYSTORE_BASE64` | The production keystore, base64 encoded |
| `KEYSTORE_PASSWORD` | Keystore password |
| `KEY_ALIAS` | Signing key alias |
| `KEY_PASSWORD` | Signing key password |

- **The keystore never enters the tree**, in any form, at any point. It is decoded to a
  temporary file inside the job and deleted in a step that runs even when the build fails.
- **Losing the keystore means losing the ability to update the app.** Android refuses an
  update signed by a different key, and sideloaded users would have to uninstall and lose
  their data. Back it up somewhere that is not this repository and not this machine.
- The Android OAuth client ID is checked in, because it is public by design and the
  installed-app flow has no client secret.
- `GITHUB_TOKEN` is provided by Actions; no personal access token is needed to publish a
  release.

---

## 8. Definition of Done

A slice is done when **all** of these hold. Not most.

- [ ] Meets its stated FR IDs
- [ ] Domain logic is pure and unit tested; new invariants have named tests
- [ ] Property tests added where a universal property exists
- [ ] Infrastructure tests cover new queries, constraints, and triggers
- [ ] Architecture tests still pass - no new reference crosses a layer
- [ ] Migration written, tested for data preservation, and run against a real backup
- [ ] Works offline - verified in aeroplane mode
- [ ] Animations meet NFR-2, including reduced-motion behaviour
- [ ] Accessibility: labelled for screen readers, 200% font scale, AA contrast
- [ ] Both light and dark themes verified
- [ ] Empty, loading, and error states designed and implemented
- [ ] No new warnings, no new suppressions, no new nullable-oblivious code
- [ ] Performance benchmarks still within NFR-1
- [ ] New vocabulary added to `CONTEXT.md`
- [ ] An ADR written if the decision was hard to reverse
- [ ] Manually exercised on a real device
- [ ] Demoable end-to-end on its own

**If the slice touches SMS, additionally:**

- [ ] No new path can retain a message body (`SM1`) - verified by the analyzer and by
      inspection
- [ ] The OTP corpus still passes against every rule (`SM4`)
- [ ] With the permission denied, nothing is worse than manual capture (`SM10`)

---

## 9. Development environment

```
.NET SDK      10.0 (pinned via global.json)
Workloads     maui-android
JDK           17
Android SDK   via Android Studio; one physical mid-range device for testing
IDE           Visual Studio 2026 or JetBrains Rider; VS Code with C# Dev Kit works
Xcode         only when the iOS target begins
```

Everything is reproducible from a clean clone with `dotnet restore && dotnet build`. Any
step that cannot be scripted gets written into [setup.md](../setup.md) rather than living
in someone's memory.

---

## 10. Platform surfaces

Android-specific code lives under `EleFi.App/Platforms/Android/` and is reached only
through an interface declared in `EleFi.Application/Abstractions`. Nothing above that
boundary knows Android exists.

| Surface | Android API | Release | Notes |
|---|---|---|---|
| SMS receive | `BroadcastReceiver` on `SMS_RECEIVED`, `RECEIVE_SMS` | 1.2 | **Restricted permission.** See §11 and ADR-0010 |
| Suggestion Prompt | `NotificationCompat` | 1.2 | Needs only `POST_NOTIFICATIONS` |
| Notification, inline reply | `NotificationCompat` + `RemoteInput` | 2.0 | |
| Quick Settings tile | `TileService` | 2.0 | API 24+ |
| Home widget | `AppWidgetProvider` + `RemoteViews`, or Glance | 2.0 | Needs its own layout work |
| Floating bubble | `WindowManager` + `SYSTEM_ALERT_WINDOW` | 3.0 | Special permission; foreground service; NFR-10.3 |
| Share target | Intent filter | 1.2 | Also the FR-11.23 fallback |
| Outbound link | `Browser.OpenAsync(..., BrowserLaunchMode.External)` | 1.0 | FR-12.4. Needs the `https` intent `<queries>` entry in the manifest or it fails silently |

**Platform surfaces write to the same database through the same repositories.** A receiver
that writes its own SQL would bypass audit triggers and invariant checks - the exact class
of bug that produces silently wrong balances.

**The SMS receiver does no work on the broadcast thread.** It matches the sender and hands
off to WorkManager. A `BroadcastReceiver` that opens an encrypted database inline is an
ANR waiting for a slow morning.

---

## 11. Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| **Abandonment** - the project stalls before daily use | Fatal | Ship the thinnest daily-usable slice first; every slice demoable; v1 scoped to ~6-8 weeks |
| **Play refuses the `RECEIVE_SMS` declaration** | Low today, High if the app ever goes to Play | Not a risk under the current sideload-via-GitHub-Releases plan - the policy governs Play distribution, not the app. Kept mitigated regardless: the paste and share-sheet fallbacks (FR-11.22-11.23) are built **first** and ship independently, and a Play build would compile SMS out until approved (NFR-11.2a). ADR-0010 |
| **MAUI cold start misses NFR-1.1** | High | Measured on a real device in S1, before any screen is built. A miss reopens the Blazor-vs-XAML decision, not the budget |
| **Motion quality below the Reanimated baseline** | High, product-level | NFR-2 rebaselined honestly; NFR-2.4a cuts an interaction rather than shipping it laggy; ADR-0009 records the trade |
| **A Parse Rule produces a wrong transaction** | Fatal to trust | `SM2` - nothing is booked without confirmation. A bad parse costs a dismissed notification, not a wrong balance |
| Bank message formats change | Medium, ongoing | Rules are data (NFR-8.11); users can add their own (FR-11.17); a miss costs one manual entry |
| Silent balance corruption | Fatal to trust | Derived balances, DB-level invariants, property tests, audit trail |
| Backup unrecoverable (lost passphrase) | Severe, user-facing | Recovery code at setup, blunt warnings, key cached in keystore |
| OEM battery managers kill the receiver or bubble | Medium | WorkManager over bare services; visible failure not silent (NFR-10.8); tested on an OEM device |
| Scope creep from an 8-point brief | High | Roadmap with explicit cut lines; "deliberately absent" lists in the docs |
| MAUI ecosystem thinner than React Native's | Medium | Prefer BCL and AndroidX over community packages; every network-capable package needs an ADR |
| SQLCipher and EF Core together are an unusual pairing | Medium | Proven end to end in S1 before anything depends on it |
| Google changes Drive API or scope policy | Medium | `drive.appdata` only; local encrypted export (FR-9.14) as an independent path |
| **Signing keystore lost** | Severe, unrecoverable | Sideloaded users cannot update without uninstalling, which destroys their local data. Backed up off this repository and off this machine. There is no recovery path from Google because there is no Play Console |
| Rive runtime or asset pipeline disappoints | Low | `IMascotService` hides the runtime; Lottie is a drop-in alternative behind the same interface, and NFR-2.7 keeps the mascot off every critical path |

---

## 12. Documentation maintenance

| Document | Updated when |
|---|---|
| `CONTEXT.md` | New domain vocabulary - same commit, always |
| `docs/requirements/*` | A requirement changes. Amend, never silently rewrite: keep IDs stable |
| `docs/adr/*` | A hard-to-reverse decision is made. Superseding ADRs link both ways |
| `docs/roadmap.md` | A slice completes or is re-cut |
| `docs/prompts/*` | A session produces decisions - via the `decision-log` skill |
| `README.md` | The elevator description or the quick-start steps change |
| `CONTRIBUTING.md` | Layout, tooling, or conventions change |

Requirement IDs are permanent. A superseded requirement is marked superseded and a new ID
is issued - so a test, ticket, or commit referencing `FR-2.13` always means the same
thing. FR-7.13 is the worked example: struck through, kept in place, and pointing at
FR-7.17.
