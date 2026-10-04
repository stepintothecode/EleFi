# EleFi

Local-first money tracker. C# on .NET 10, .NET MAUI (Android first), EF Core over
SQLCipher-encrypted SQLite on device, no backend.

> Renamed from **EleFund** on 2026-08-29, and moved from Expo/TypeScript to C#/.NET MAUI
> in the same change ([ADR-0009](../docs/adr/0009-dotnet-maui-over-expo-typescript.md)).
> Anything still saying Expo, React Native, Drizzle, or Reanimated is stale - say so.

## Read these first

| Document | For |
|---|---|
| [docs/CONTEXT.md](../docs/CONTEXT.md) | Domain glossary. **Use this vocabulary everywhere** - code, UI copy, commits, conversation |
| [docs/requirements/domain-model.md](../docs/requirements/domain-model.md) | Entities, fields, numbered invariants |
| [docs/requirements/functional.md](../docs/requirements/functional.md) | Numbered FRs and user stories |
| [docs/requirements/non-functional.md](../docs/requirements/non-functional.md) | Performance, animation, security budgets |
| [docs/requirements/software-development.md](../docs/requirements/software-development.md) | Stack, structure, testing, Definition of Done |
| [docs/adr/](../docs/adr/) | Decisions that are hard to reverse - check before contradicting one |
| [docs/roadmap.md](../docs/roadmap.md) | Slices and their blocking edges |

## Conventions

**Always maintain the decision log.** When a session produces decisions - planning,
scoping, architecture, grilling - record the prompts and answers using the `decision-log`
skill ([.claude/skills/decision-log/](skills/decision-log/SKILL.md), invoke with
`/decision-log`). Logs live in [docs/prompts/](../docs/prompts/) as
`YYYY-MM-DD-<topic-slug>.md`. If a log already covers the thread of work, update it rather
than creating a parallel file. Chat history is not a record.

**Say Container, not account.** "Account" means a login identity or the specific
BankAccount kind. Saying it for the general concept is how this model gets muddled.

**Say Source and Destination, never To and From.** To/From were the ambiguous sketch terms
that this model exists to replace.

**Say Capture Suggestion, not draft.** A parsed SMS is a suggestion, and it is not a
transaction until a human confirms it. There are no drafts in this app.

**Say Label, never tag or category.** There is one concept, and it behaves like a tag: flat,
optional, many per transaction. A Tag entity existed and was removed in `ADR-0013`.

**Say unlabelled, not Uncategorised.** It is the absence of labels, not a label. There is no
row for it.

## Non-negotiables

These are invariants, not preferences. Breaking one is a bug even if tests pass.

- Money is **integer minor units**. No `double`, `float`, or `decimal` ever holds,
  transports, or computes money. `decimal` is for FX ratios only, inside `Domain.Fx`.
- Balances are **always derived**, never stored - no `current_balance` column, at any layer.
- Transaction kind is **derived** from its two parties. It is never a stored field.
- **Self Transfers are excluded from every spend and income aggregate.** This is what stops
  credit-card bill payments double-counting.
- Deletes are **soft**. The single exception is a dismissed or expired Capture Suggestion,
  which is machine output the user rejected, not user-entered data.
- **A parsed SMS never becomes a transaction without explicit user confirmation** (`SM2`).
- **An SMS body is never persisted, logged, audited, backed up, or exported** (`SM1`). It
  is a `ref struct` and cannot escape the receiver.
- **The export is the filtered list, serialised** (`X1`). Same rows, same order, no separate
  export configuration.
- The only **automatic** egress path for user data is the encrypted Drive backup. A new one
  requires an ADR, not a PR comment. The CSV export and the local JSON backup are files the
  user asked for, handed to the share sheet, with the UI saying they are unencrypted.
- **Labels are flat, optional, and many per transaction** (`ADR-0013`). There is no
  hierarchy, no Tag entity, and no `Uncategorised`. Unlabelled is a state. The spend
  breakdown's total is counted once per transaction and is **never** the sum of the buckets.
- Only `drive.appdata` scope. Broader Drive scopes are restricted and forbidden.
- Only the **last 4 digits** of an account number are ever stored.

- **EleFi is free permanently and MIT licensed** (`ADR-0012`). No paid tier, no unlock, no
  perks for supporting. Never propose one.

## Layout

```
src/EleFi.Domain          pure C#, BCL only
src/EleFi.Application     use cases + the ports Infrastructure implements
src/EleFi.Infrastructure  EF Core, SQLCipher, Drive, crypto. the only I/O
src/EleFi.Ui              Razor components + view models. net10.0, bUnit-testable
src/EleFi.App             MAUI host shell. net10.0-android, Platforms/, wwwroot/
tests/EleFi.<name>.Tests  mirrors its src project's folder tree
```

Versions live in `Directory.Packages.props`; shared MSBuild in the three
`Directory.Build.props` files. `EleFi.slnx` is the solution.

## Working here

- `EleFi.Domain` references nothing but the BCL - no EF Core, no MAUI, no `SQLitePCLRaw`,
  no `Android.*`. Keep it pure and testable on a desktop runner.
- `EleFi.Application` declares the interfaces; `EleFi.Infrastructure` implements them.
  Dependencies point inward, and `EleFi.Architecture.Tests` enforces it.
- **`tests/X.Tests/Foo/BarTests.cs` must match `src/X/Foo/Bar.cs`.** A layout test fails
  the build otherwise. If something in `EleFi.App` is worth testing, move it to `EleFi.Ui`.
- **Every public member in `src/` needs an XML doc comment.** CS1591 is a build error.
- **No `Version` on a `PackageReference`.** It goes in `Directory.Packages.props`.
- Only `Persistence/Repositories/` touches the database.
- No Razor component touches `IJSRuntime` directly; interop sits behind an interface and is
  `IAsyncDisposable`.
- No outbound link is an `<a href>` - it goes through `ILinkOpener`, or it hijacks the
  WebView.
- Parse Rules are **data**, not code. A new bank is a rule plus a corpus test.
- New domain vocabulary goes into `CONTEXT.md` in the same commit that introduces it.
- Charts follow the `dataviz` skill.
- **No em dashes or en dashes in anything written here.** Use a plain `-` or `...`.
