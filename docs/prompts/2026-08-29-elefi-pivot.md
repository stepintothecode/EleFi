# EleFi - Rename, stack pivot, SMS capture, filtered export

Session held 2026-08-29. Step 1 of a three-step brief: documentation first, architecture
second, implementation third. This file is the durable record of what was asked and what
was decided, so nothing depends on chat history surviving.

Supersedes parts of [2026-08-01-foundation-grilling.md](2026-08-01-foundation-grilling.md),
which is kept unedited.

Documents produced or rewritten:
[CONTEXT.md](../CONTEXT.md) · [requirements/](../requirements/) ·
[ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md) ·
[ADR-0010](../adr/0010-on-device-sms-assisted-capture.md) · [roadmap.md](../roadmap.md) ·
`README.md` · `CONTRIBUTING.md` · `SECURITY.md` · [setup.md](../setup.md)

---

## 1. The brief (verbatim, unedited)

> ROLE
> Act as an Expert Software Architect and Senior C# .NET Developer. You strictly adhere to SOLID principles, clean architecture, and separation of concerns. You write highly maintainable, well-documented, and elegant code.
>
> GOAL
> Update the project documentation to reflect new core requirements, define a serverless local-first architecture, and incrementally guide me through implementing the application step-by-step.
>
> CONTEXT
> We are building a personal finance application.
>
> Project Name: Rename the app from "EleFund" to "EleFi" across all files.
>
> Tech Stack: C# (.NET 10) using .NET MAUI (Blazor Hybrid or native MAUI) targeting Android primarily.
>
> Architecture: Zero-server, fully client-side.
>
> Data Layer: Local database (SQLite) encrypted using SQLCipher.
>
> Cloud Sync (Zero-Cost): Implement cloud backup via the user's personal Google Drive account (Google Drive API) so they own their data and I pay $0 for hosting.
>
> New Feature 1 (SMS Parsing): The app must parse incoming SMS messages to identify financial transactions and trigger a local notification prompting the user to add the new transaction.
>
> New Feature 2 (Dynamic CSV Export): The "All Transactions" page must include a CSV export feature. This export must strictly respect whatever UI filters the user currently has applied (e.g., if no filters are applied, export everything; if filtered by month, export only that month).
>
> ACTION
> Please execute the following steps sequentially. Do not move to the next step until I confirm.
>
> Step 1: Documentation Update
> Review CONTEXT.md and any other standard documentation files you recommend for a new repo. Rewrite them to reflect the "EleFi" name, the serverless encrypted local architecture, the Google Drive sync strategy, the SMS parsing requirement, and the filtered CSV export requirement.
>
> Step 2: Architectural Skeleton & Tech Decisions
> Propose the folder structure and architectural design (e.g., MVVM or Clean Architecture). Explain how we will separate the UI layer, the local database/encryption layer, the SMS background service layer, and the Google Drive sync service.
>
> Step 3: Incremental Implementation
> Once the architecture is approved, provide the implementation steps one by one. Start with the domain models and encrypted SQLite setup, followed by the core services, and finally the UI. Ensure every piece of code provided follows SOLID principles and includes necessary interface abstractions.

---

## 2. Decision log

Decisions taken in Step 1. `†` marks one that reverses a previously accepted decision.

| # | Question | Decision |
|---|---|---|
| 1 | Rename EleFund → EleFi where? | Every doc, plus `.claude/CLAUDE.md`. **Not** inside the 2026-08-01 log's verbatim quotes - a log rewritten to match the present is not a log. GitHub repo and local folder still say EleFund; both are the user's to rename |
| 2 | How to handle the stack reversal? | † New [ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md) superseding [ADR-0005](../adr/0005-expo-typescript-over-csharp.md). 0005 kept in full and unedited, with a banner. Its animation argument was correct and 0009 accepts the cost rather than disputing it |
| 3 | Why reverse, given 0005's reasoning still holds? | 0005 optimised for the wrong risk. It traded daily fluency for animation quality and named abandonment as the risk it created. For a solo project whose value depends on daily use, that is the risk that matters. SMS also makes every native surface first-party C# instead of Kotlin behind a bridge |
| 4 | Blazor Hybrid or native XAML? | **Not decided.** Recommended Blazor Hybrid, recorded as open ADR-0011, gated on a cold-start measurement in slice S1. Deciding it by preference before measuring is how it gets discovered at week nine |
| 5 | What does the pivot cost? | NFR-2 rebaselined: mechanism rules restated for MAUI, feel rules unchanged. NFR-1.1 cold start raised 2.0 s → 2.5 s. Both written into the docs rather than discovered later |
| 6 | Does C# returning revert the audit trail to an EF Core interceptor? | No. Triggers stay, now rejected on merits rather than by circumstance: an interceptor misses file-swap restores, seeded data, bulk import, and background-service writes. [ADR-0008](../adr/0008-audit-via-sqlite-triggers.md) amended |
| 7 | Does a parsed SMS create a transaction? | **No.** New `CaptureSuggestion` entity, `SM2`. A suggestion holds no balance and enters no report until a human confirms it. The single most important decision in the SMS feature |
| 8 | Why not reuse `needs_review` and auto-create? | Less code, but the flags assert different things: "you recorded this fast" vs "a machine thinks this happened". Collapsing them puts pattern-matching output into balances, and one bad parse makes every number wrong with no visible cause |
| 9 | What about Google Play's restricted SMS permission? | Real, but **conditional on a decision not yet made**. Two of your own prompt files added to `docs/prompts/` during this session state that distribution is signed APKs on GitHub Releases via Obtainium and IzzyOnDroid, with no Play Console. Play's policy governs Play distribution, not the app - so a sideloaded build simply declares the permission. Flagged as needing verification only *if* Play is ever wanted. Mitigation kept anyway (see #9a) |
| 9a | Keep the fallback paths given #9? | Yes. Slice S37 builds parsing plus permission-free paste and share-sheet paths **first**, S38 adds the receiver. Cheap, keeps the Play door open, and it is the only version of this feature iOS can ever have |
| 10 | Where do SMS bodies get stored? | Nowhere. `SM1` is absolute and enforced by the type system - the body is a `ref struct` that cannot be assigned to a field, boxed, captured, or serialised, so no code path can log, store, back up, or export it |
| 11 | Does SMS access weaken the egress promise? | No. Nothing derived from a message is transmitted. NFR-5.1 is untouched, and NFR-5.18 says so explicitly |
| 12 | Does CSV export need new modelling? | Almost none. CONTEXT.md already said "the export is the filtered view, serialised". Added `X1`-`X5` invariants and a property test `rows(export(f)) == rows(list(f))`, which turns the intention into a guarantee |
| 13 | Export moves to which release? | † v1.1 → v1.0, as slice S15 keeping its ID. It reuses S7's query untouched, so it is cheap where it now sits |
| 14 | What about FR-7.13, which paywalled filtered export? | † Superseded by FR-7.17. Struck through and kept in place per the docs' own ID-stability rule. Filtering is what makes an export useful. **Whether something else carries a price later is left open for the user** |
| 15 | Which new repo files? | `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, `docs/setup.md`, `docs/adr/README.md`, `.editorconfig`, `.gitignore` (was empty). No `LICENSE` - that is a choice, not a default |
| 16 | Repo-conventions rules that conflict with the repo | Two, both resolved toward existing repo style, flagged rather than silently applied: em dashes (the convention bans them; every existing doc uses them) and lower-case-dashed folders (C# projects are PascalCase by convention) |

---

## 3. What changed from the 2026-08-01 decisions

| Was | Now | Why |
|---|---|---|
| Expo / React Native / TypeScript | C# 14, .NET 10, .NET MAUI | ADR-0009. Fluency beats animation ceiling for a solo daily-use project |
| Drizzle ORM | EF Core 10 | Follows the language |
| Reanimated worklets (NFR-2.1) | MAUI motion module, NFR-2 rebaselined | The real cost of the pivot, written down |
| Cold start ≤ 2.0 s | ≤ 2.5 s | .NET runtime plus WebView startup |
| Vitest / fast-check / Maestro | xUnit v3 / CsCheck / bUnit / Appium | Follows the language |
| ESLint boundaries plugin | Project references + NetArchTest | A forbidden dependency is now a compiler error |
| SMS parsing "deliberately absent" | v1.2 feature, suggestion-only | The original entry was right about the risk, wrong about which half of the feature carried it. Automatic *booking* stays forbidden |
| CSV export in v1.1, filtering paid | v1.0, core, free | FR-7.13 superseded by FR-7.17 |
| v1.0 ~6-8 weeks | ~8-10 weeks | Export moved in; S1 now carries an unfamiliar stack and two gating spikes |

---

## 4. Read from your own files, not asked

Two prompt files appeared in `docs/prompts/` during this session - *Automated Zero-Cost
GitHub Actions CI* and *Mascot & Interactive Animations*. They were treated as context, and
three facts in them were written into the docs. **Confirm or correct these**, since they
were inferred rather than instructed:

| Fact taken from those files | Where it landed |
|---|---|
| Distribution is sideloading via GitHub Releases, Obtainium, IzzyOnDroid; no Play Console | ADR-0010's policy risk downgraded to conditional; software-development.md §7 releases an APK to a GitHub Release rather than a Play track; S30 renamed and marked optional |
| The project is open source and the repo public | setup.md warns against putting real bank SMS in the checked-in parse corpus |
| Blazor Hybrid and Rive are assumed | Noted, but **not** used to close ADR-0011 - that decision stays gated on the S1 cold-start measurement, because a prompt written before the measurement is a preference, not a result |

## 5. Second round: answers to the open questions

The user answered on 2026-08-29, and the answers settled every open question plus four new
decisions. Their words, verbatim:

> Yes, rename the repo and folder.
> There will never be any paid tier, but I will have a support page link for users to contribute by supporting only if they want- refer to other projects e.g. K:\Projects\ytclipnshare and refer here also- K:\Projects\support.
> I would keep MIT License, hope that is enough
> Em dashes should not be present, if found replace them with either normal dashes or just three dots (...), but not with the chracter with three dots merged into it.
> Lastly, please also surely consider the other 2 newly added prompts in the docs/prompts folder, I plan to run those prompts next, once all the implementation work is done. But yes, in the implementation work, consider those items too as much as possible or whatever is relevant.
>
> Lastly,  keep a folder "src" and a folder "tests" in this repo parallel to the "doc" folder. And a slnx file, Directory.Packages.props for central package versioning. And even a  Directory.Build.props for all common configs like docstrings for all public APIs and classes. Add all projects in src folder, and all test projects (as well as test files) in the same hierarchy in tests folder. Apart from C#, if this codebase will contain any other programming language/tech stack code, then it should also follow the same convention of src and tests. Make sure navigation is very easy in this repo, I have OCD with coding beautifully.

| # | Question | Decision |
|---|---|---|
| 17 | Rename the repo and folder? | Yes. Docs and code all say EleFi. **The GitHub rename and the folder rename need the user's hands**: `gh` is not installed and a process cannot rename its own working directory. Commands provided |
| 18 | Monetisation? | **Never.** [ADR-0012](../adr/0012-free-forever-voluntary-support.md): free permanently, no paid tier, no perks. A voluntary support link only, following the pattern already used by `ytclipnshare` and the shared `support` repo |
| 19 | Which support pattern? | The existing one, unchanged: `stepintothecode.github.io/support/?from=<surface>`, one `from` per surface (`elefi-app`, `elefi-repo`), a `.github/FUNDING.yml`, and the "buys nothing" wording. Needs one line added to `assets/projects.js` in the support repo |
| 20 | Licence? | MIT, matching `ytclipnshare`. `Copyright (c) 2026 stepintothecode` |
| 21 | Em dashes? | Purged. 305 em dashes, 57 en dashes, 2 ellipsis characters and 2 minus signs replaced with ASCII across 24 files. **No verbatim quote contained one**, so no user text was altered. Added as a standing rule in `CLAUDE.md` |
| 22 | Blazor Hybrid or XAML, given the user's own next prompts assume Blazor? | † Closed as **Blazor Hybrid** ([ADR-0011](../adr/0011-blazor-hybrid-over-native-xaml.md)), on design substrate and the web target rather than on the measurement. The S1 cold-start spike is kept, but now *verifies* NFR-1.1 rather than deciding the UI layer |
| 23 | Where do Razor components live? | `src/EleFi.Ui`, a separate `net10.0` library, **not** in the MAUI app. `EleFi.App` targets `net10.0-android`, which no desktop test runner can reference, so components there would be untestable. This was not asked for; it is forced by wanting bUnit tests at all |
| 24 | How is "navigation is very easy" enforced? | By tests, not convention. `RepositoryLayoutTests` fails the build if a src project has no test project, if a test project has no src project, or if `tests/X.Tests/Foo/BarTests.cs` has no `src/X/Foo/Bar.cs`. Two exemption lists, both requiring a written reason |
| 25 | Mirror rule in both directions? | **One direction only.** Every test file maps to a source file; not every source file needs a test file. The reverse produces empty tests written to satisfy a rule |
| 26 | How are docstrings enforced? | `GenerateDocumentationFile` in `src/Directory.Build.props` plus warnings-as-errors, so CS1591 (missing XML comment on a public member) fails the build. Waived in `tests/` |
| 27 | Non-C# code? | Same split. Blazor interop JS lives at `src/EleFi.App/wwwroot/js/`, tests at `tests/EleFi.App.Tests/js/`. Nothing gets a root-level folder |

### Things the build caught, not the plan

| What | Why it matters |
|---|---|
| `EleFi.Application` collides with MAUI's `Application` type | `App.xaml.cs` must write `Microsoft.Maui.Controls.Application` in full. Recorded in software-development.md section 2 and setup.md, because it will happen again to anyone adding a file there |
| bUnit 1.40.0 pulls AngleSharp 1.2.0, which has a published advisory | The dependency-audit gate fired on the first build. Fixed by a transitive pin with a comment saying when it can go, not by lowering the gate |
| `Every_test_project_matches_a_src_project` failed on its first run | `EleFi.Architecture` and `EleFi.Properties` are cross-cutting and have no src counterpart. Made explicit in a list with reasons rather than silently allowed |

## 6. Open questions for the user

1. **Two manual steps remain**, because a tool cannot do them here: renaming the repo on
   GitHub (`gh` is not installed) and renaming the local folder (a process cannot rename its
   own working directory). Commands are in the session summary.
2. **The support repo needs one line.** `assets/projects.js` in `stepintothecode/support`
   needs `'elefi-app'` and `'elefi-repo'` entries, or the page shows generic wording.
3. **Which GitHub account?** `ytclipnshare` and `support` live under `stepintothecode`;
   EleFi is under `menonkartikeya`. The support link, `FUNDING.yml`, and the MIT copyright
   line all say `stepintothecode`. Consistent as written, but worth a deliberate choice.
4. **Package versions are pinned to plausible current releases** and all restore cleanly
   today. They are worth a `dotnet list package --outdated` pass before real work starts.
5. **Folder naming.** The `repo-conventions` skill wants lower-case-dashed; C# projects are
   PascalCase by convention. Went with .NET convention inside `src/` and `tests/`, skill
   convention for `docs/`. Flagged rather than silently chosen.

---

## 7. Final shape

Settled: the name, the .NET stack, Blazor Hybrid, the five-project Clean Architecture
boundary, SMS as suggestion-not-transaction, export as the serialised filtered list, free
forever under MIT with a voluntary support link, and the re-cut roadmap.

Built and verified: `EleFi.slnx` with five projects in `src/` and six in `tests/`,
`Directory.Build.props` x3, `Directory.Packages.props`, `.editorconfig`, `global.json`,
`LICENSE`, `.github/FUNDING.yml`. `dotnet build` and `dotnet test` both pass, 9 tests
green, 0 warnings.

Nothing about the domain is implemented. Step 1 of the brief is complete and part of Step 2
arrived early, because the user asked for the skeleton directly.

Deliberately left open: which GitHub account the project lives under.
