# Contributing to EleFi

## Read first

[docs/CONTEXT.md](docs/CONTEXT.md) is the domain glossary and it is not optional. Every
term in it has exactly one meaning, used in code, UI copy, commits, and conversation. Most
confusion in a money model comes from two words for one thing, or one word for two.

Three that matter most:

- **Container**, never "account". "Account" means a login identity, or the specific
  BankAccount container kind.
- **Source** and **Destination**, never "To" and "From". To/From were the ambiguous sketch
  terms this model exists to replace.
- **Capture Suggestion** is not a Transaction. A parsed SMS is a suggestion until a human
  confirms it.

## Setup

See [docs/setup.md](docs/setup.md). Short version:

```
dotnet restore
dotnet build
dotnet test
```

You need the .NET 10 SDK, the `maui-android` workload, JDK 17, and the Android SDK. A
physical mid-range Android device is required for anything touching performance, motion,
or SMS - an emulator will not tell you the truth about any of the three.

## How the solution is laid out

```
src/EleFi.Domain          pure C#, references nothing but the BCL
src/EleFi.Application     use cases, and the interfaces the outside world implements
src/EleFi.Infrastructure  EF Core, SQLCipher, Drive, crypto. the only project doing I/O
src/EleFi.Ui              Razor components and view models. net10.0, so bUnit can reach them
src/EleFi.App             MAUI host shell. net10.0-android, Platforms/, wwwroot/

tests/EleFi.<name>.Tests  one per src project, mirroring its folder tree
tests/EleFi.Architecture.Tests   layer boundaries and repository layout
tests/EleFi.Properties.Tests     universal properties across layers
```

**Two rules make navigation predictable, and both are tests, not conventions.**

1. Every project in `src/` has `tests/<name>.Tests`. The only exemption is `EleFi.App`,
   listed with its reason in `RepositoryLayoutTests`.
2. `tests/X.Tests/Foo/BarTests.cs` must have a matching `src/X/Foo/Bar.cs` or `Bar.razor`.
   Finding the tests for a file never needs a search.

The mirror is one-directional on purpose: every test maps to a source file, but not every
source file needs a test file. Demanding the reverse produces empty tests written to
satisfy a rule.

Dependencies point inward. `EleFi.App` never references `EleFi.Infrastructure` types
directly, only its DI registration extension, and `EleFi.Ui` never references it at all.
`EleFi.Architecture.Tests` enforces all of this with NetArchTest and assembly-reference
checks, so a forbidden reference fails the build rather than a review.

**Put a new file where its tests can find it.** If something in `EleFi.App` is worth
testing, that is the signal it belongs in `EleFi.Ui` instead.

**Other languages follow the same split.** JavaScript for Blazor interop lives in
`src/EleFi.App/wwwroot/js/`, with its tests at `tests/EleFi.App.Tests/js/`. Nothing gets a
folder at the repository root.

Full detail in
[docs/requirements/software-development.md](docs/requirements/software-development.md) §2.

## Build configuration

Three files, and nothing outside them sets these:

| File | Holds |
|---|---|
| `Directory.Build.props` | Settings shared by every project: nullable, warnings-as-errors, analyser level, dependency-audit level, identity |
| `src/Directory.Build.props` | Production-only: `GenerateDocumentationFile`, which makes a missing XML doc on a public member (CS1591) a build error |
| `tests/Directory.Build.props` | Test-only: the xUnit harness, the global `using Xunit`, and the waivers for CA1707 and CS1591 |
| `Directory.Packages.props` | Every package version, decided once. A `Version` on a `PackageReference` in a `.csproj` is a build error |

**Every public type and member in `src/` carries an XML doc comment.** Not because a tool
wants one, but because the comment is where the invariant goes: what the type guarantees,
and what would break if you changed it.

## Non-negotiables

These are invariants, not preferences. Breaking one is a bug even if the tests pass.

- Money is **integer minor units**. No `double`, no `float`, no `decimal` ever holds,
  transports, or computes money. `decimal` is permitted for FX ratios only, inside
  `Domain.Fx`.
- Balances are **always derived**, never stored. No `current_balance` column, at any layer,
  including caches.
- Transaction kind is **derived** from its two parties. It is never a stored field.
- **Self Transfers are excluded from every spend and income aggregate.** This is what stops
  credit-card bill payments double-counting.
- Deletes are **soft**. The one exception is a dismissed or expired Capture Suggestion,
  which is machine-derived data the user rejected.
- The only egress path for user data is the **encrypted Drive backup**. A new one requires
  an ADR, not a pull request comment.
- Only the `drive.appdata` scope. Broader Drive scopes are restricted and forbidden.
- Only the **last 4 digits** of an account number are ever stored.
- **An SMS body is never persisted, logged, audited, backed up, or exported.** It is a
  `ref struct` that cannot escape the receiver, so this is enforced by the compiler.

## Tests

```
dotnet test                                          everything
dotnet test tests/EleFi.Domain.Tests                 fast, pure, run these constantly
dotnet test tests/EleFi.Architecture.Tests           layer boundaries
```

- **Name a test for the invariant it covers.** `T4_SameCurrency_AmountsMustMatch`, not
  `TransactionTest3`. A failure should name the rule it broke.
- **Property tests for universal properties** (CsCheck). The list is in
  software-development.md §5.2, and it is the part of the suite most likely to catch the
  bug nobody thought to write an example for.
- **Do not mock the database.** Infrastructure tests use real in-memory SQLite with
  SQLCipher active. Mocking a database mostly tests the mock.
- **When you fix a bug, add the test that would have caught it**, and note in one line what
  used to happen.

## Code style

`.editorconfig` is the single source of style, and `dotnet format --verify-no-changes`
runs in CI. Nullable reference types are on, warnings are errors, and a
`#pragma warning disable` needs a comment naming the reason.

Write code that reads like the code around it. Comments explain *why*, not *what*; if a
comment restates the line below it, delete it.

## Commits

Conventional Commits, referencing requirement IDs where they apply:

```
feat(export): filtered CSV export from the transaction list (FR-7.17, X1)
fix(balance): exclude soft-deleted rows from container balance (D2)
docs(adr): record the move to .NET MAUI (ADR-0009)
```

Two rules that are easy to forget:

- **New domain vocabulary goes into `CONTEXT.md` in the same commit that introduces it.**
- **A hard-to-reverse decision needs an ADR.** See [docs/adr/README.md](docs/adr/README.md)
  for when and how.

## Decision logs

When a session produces decisions - planning, scoping, architecture, grilling - record the
prompts and answers with the `decision-log` skill. Logs live in
[docs/prompts/](docs/prompts/) as `YYYY-MM-DD-<topic-slug>.md`.

The user's own words are reproduced verbatim, typos included. Chat history is not a record.

## Money

EleFi is free, permanently, and MIT licensed. There is no paid tier and there will not be
one ([ADR-0012](docs/adr/0012-free-forever-voluntary-support.md)).

The one money-adjacent thing in the app is a support link. Three rules govern it, and each
one exists because breaking it turns a gift into a sale:

- **It gives nothing back.** No tier, perk, badge, or unlock.
- **It never nags.** One line on the About screen. Not a modal, not an interstitial.
- **It opens the system browser**, via `ILinkOpener`, never a plain `<a href>`. Under
  Blazor Hybrid an `<a href>` navigates the host WebView and strands the user in the app.

## Definition of Done

A slice is done when every box in software-development.md §8 is ticked. Not most of them.
The list exists because "done" in a money app has to include the migration test, the
offline check, and the real device.
