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

## 7. Step 3: implementation, 2026-08-30

> go ahead with step 3 then. Make sure to read and take into account also the other 2 uncommited prompts in doc/prompts directory. Give me a nice working perfect solution, which first I use and then tell how to improve

Built S1 to S7 plus S15, and S37's parser with its permission-free paste path. 77 tests,
0 warnings, Android head builds.

| # | Question | Decision |
|---|---|---|
| 28 | Which slices, given "a working solution I can use"? | The critical path to a daily driver: money, containers, capture, balances, list, filters, export. Not goals, not backup, not the SMS receiver. A narrower thing that works beats a wider thing that half does |
| 29 | `Microsoft.EntityFrameworkCore.Sqlite` or `.Sqlite.Core`? | **Core.** The full package bundles `bundle_e_sqlite3`, the plain unencrypted engine. With both present the provider race decides which one keys the file, and losing it means the database opens unencrypted with nothing saying so |
| 30 | How is `SM1` actually enforced? | `SmsBody` is a `ref struct`. It cannot be a field, boxed, captured by a lambda, or held across an `await`, so there is no expressible path that stores a message body. `ToString` is overridden to return a placeholder so an accidental interpolation cannot leak one either |
| 31 | Audit trail: interceptor or triggers? | Triggers, created inside the migration that creates each table. Written as C# that emits SQL so the four trigger shapes are declared once rather than copied five times |
| 32 | How to prove `X1` rather than assert it? | A test that runs the list query and the export over the same filter and compares row for row, in order, against a real SQLCipher database |
| 33 | Where does the mascot live? | `IMascotService` in Application, `MascotService` owning `IJSRuntime` in the app, `elefi-mascot.js` in `wwwroot/js`. A hand-drawn canvas elephant ships now so the whole path works before any art does. Your mascot prompt slots in by replacing the JS and nothing else |
| 34 | Cross-container name collisions | Three hit during the build: `EleFi.Application` vs MAUI's `Application`, `FileShare` vs `System.IO.FileShare`, and Razor components whose injected property matched the component class name. All resolved by renaming, all noted in the code where they will recur |

### Bugs the tests caught, not review

| What | Why it matters |
|---|---|
| `"Rs. 450.50"` failed to parse | Stripping non-digits left `".450.50"`, two decimal points, rejected. That is the single most common format in Indian bank SMS, so the SMS feature would have silently matched almost nothing. Fixed by scanning for the number instead of stripping around it |
| `"1.2.3"` then parsed as `1.2` | The scan fix truncated instead of refusing. A plausible wrong amount is the one output a money parser must never produce, so a malformed-number guard was added |
| bUnit pulled a vulnerable AngleSharp | The audit gate fired. Pinned forward with a comment saying when the pin can go |
| The layout test failed on its own first run | `EleFi.Architecture` and `EleFi.Properties` have no `src` counterpart. Made explicit in a list with reasons |

## 8. First run on a real phone, 2026-08-30

> After installation of the apk, as soon as I open the app, it gives me "An unhandled error has occured. Reload". Btw, that also is hidden behind the bottom nav bar buttons of my phone. Also it says "Loading..." on top but is hidden behind the notification bar over it.
>
> And lastly, the icon of the app is literally ".NET" icon in purple color. Please create a new icon for this app using a grey or purple elephant in it (our mascot Ele). Keep icon images in multiple dimensions and sizes.

Four bugs, three of them things no test in the suite could have caught, because every test
ran on a desktop runner with no screen.

| # | Bug | Cause and fix |
|---|---|---|
| 35 | Crash on first open | `default(Currency)` had a null code, and `Exponent` looked it up in a dictionary, which throws on a null key. The dashboard's net-worth tiles rendered on the synchronous first pass, before `OnInitializedAsync` had replaced `default(NetWorth)`. **Fix:** a struct can always be default-constructed, so every member on one must answer for that case. `Code` is now never null, and an unspecified currency is the additive identity, because zero is zero in every currency. Two different *real* currencies still refuse to combine |
| 36 | Content behind the status bar and gesture pill | Android 15 forces edge-to-edge, so the WebView genuinely sits under both. **Fix:** `env(safe-area-inset-*)` resolved once into CSS variables and applied to the page, the tab bar, the sticky date headers, and the error bar |
| 37 | The error bar was the only thing shown, and it was half-hidden | An error at the root of the tree takes the whole app down. **Fix:** an `ErrorBoundary` in `MainLayout` contains the failure to one page, keeps the tabs working, and says the thing that matters: the data on the device is untouched. The boundary recovers on navigation, or it latches and every later page shows a stale error |
| 38 | The icon was the .NET bot | **Fix:** Ele, as two SVG layers. MAUI rasterises five Android densities from them at build |

### Found while fixing, not reported

| What | Why it mattered |
|---|---|
| `IMascotService` was registered scoped, and `MascotViewer` disposes it | Navigating away from the dashboard disposed the shared instance, and the mascot never animated again for the rest of the session. Now transient, so the component that disposes it is the component that owns it |
| Startup blocked the Android main thread on `SecureStorage` and on migrations | It happens to work today. On a slower device or a colder keystore it is a deadlock presenting as a frozen splash screen with no message. Both now go through `Task.Run` first |
| Ele sat low inside the adaptive-icon safe zone | He is drawn head-first, so his visual centre is y 293 on a 512 grid, not 256. A circular launcher mask crops around 256 and would have clipped his trunk. The foreground SVG lifts him 34 units |

**The lesson worth keeping:** 89 tests passed while the app could not open. Everything ran
on a desktop runner, and the three failures were a value type's default state, an OS layout
behaviour, and a launcher mask. A device is not an optional part of the test matrix.

### Round two: the app still would not open

The first round fixed a real crash and missed the one that mattered. Symptom the second
time: the boot spinner never resolved, which says the root component never rendered at all,
so nothing in any page could be responsible.

| # | Bug | Cause and fix |
|---|---|---|
| 39 | Router threw before rendering anything | `Routes.razor` names `NotFound` as the `NotFoundPage`, and .NET 10's `Router` validates that eagerly in `SetParametersAsync`: a type with no `RouteAttribute` throws before it renders. `NotFound.razor` had no `@page` directive, because it was hand-written rather than kept from the template. **Fix:** one line, `@page "/not-found"` |

The diagnosis came from a test, not from reading code. Every attempt to reason about it was
wrong, and three of those wrong guesses were plausible enough to have been worth a rebuild
each.

| Why the suite missed it | |
|---|---|
| `HomeTests` calls `RenderComponent<Home>()` | Renders the page directly, skipping the router and the layout |
| `MainLayoutTests` renders the shell directly | Same gap, one level up |
| Nothing rendered `Routes` | So the first component the app actually renders had no test at all |

`RoutesTests` now covers it, including a test that every tab destination has a page
claiming that route, because a typo there is a dead tab nobody notices until they tap it.

### Diagnosis is now possible at all

An hour went into this blind, because the app has no crash reporting and never will:
NFR-5.8 forbids transmitting anything and there is no server to transmit to. That leaves
the device as the only possible source of an answer, and nothing was writing to it.

`FileErrorLog` writes error-level logs to `elefi-errors.log` in the app's data directory.
Read it with
`adb shell run-as com.stepintothecode.elefi cat files/elefi-errors.log`. It stays inside
NFR-5.9: exception type, message, and stack only, no scopes and no state.

## 9. Twenty-two pieces of feedback, 2026-08-31

First real use of the app produced 22 items. Three needed a decision before building.

| # | Question | Decision |
|---|---|---|
| 40 | Tags, which you asked me to confirm first | **Labels only for now.** Tags stay in the schema and the vocabulary, so they arrive later without a migration. Label add, rename and delete built, because you could not create one at all. *(Reversed the next day: see section 11. Tags were removed entirely and labels became the many-per-transaction concept instead.)* |
| 41 | Quick capture surface | **Widget plus Quick Settings tile**, both launching an in-app flow. The animated stepper cannot live in a widget: Android widgets draw through `RemoteViews`, which permits no custom drawing and no animation. The widget is the door, not the room |
| 42 | Drive backup | **UI placeholders only, no backend.** Settings says plainly that it is not built and that the ledger currently exists on one device |
| 43 | Cash versus Wallet | **Kept both.** Cash is physical notes; a Wallet is a balance spendable in exactly one place. Merging them would put "money I can hand anyone" and "money locked in one app" into one number. The picker now shows a description per kind |
| 44 | Container with no amount | **Left as is.** A new wallet or a just-cleared card genuinely holds zero, and demanding a number invites a made-up one |
| 45 | Investment kinds | Added `MutualFund`, `Stocks`, `Nps`, all Locked. **Their balance is cost basis, not market value**: balances are derived from transactions and there is no price feed. Said so in the UI rather than letting the user assume otherwise |
| 46 | Time on a transaction | A separate nullable `TimeOnly`, not folded into the date. Combining them makes the transaction an instant, and an instant shifts across timezones. Nullable because a back-dated entry has no honest time |

### The credit-card bug

Reported as "repaying makes it say I owe more". Two things were wrong.

`ContainerBalance.AmountOwed` took `Abs()` of the ledger balance, throwing away the sign
that distinguishes debt from credit. Paying 2,000 onto a card with nothing on it reported
"2,000 owed" when the issuer in fact owed you, and the number moved the wrong way as you
repaid. Owed is now the negation of the balance, keeps its sign, and the UI says "in credit"
when it goes the other way.

The second was that nothing explained the model. The Containers page now spells out that
spending is a Debit **from** the card, repayment is a Self Transfer **to** it, and that a
negative amount is never the answer because direction comes from which container you pick.

### The migration nearly bricked the app

Making container names unique meant a unique index, and existing databases already contain
duplicates. A naive `CREATE UNIQUE INDEX` fails, and a migration that throws leaves the app
unable to open with the user's data still inside it.

The de-duplication is written into the migration: rank duplicates, rename all but the oldest.
The first attempt used a correlated count, which is evaluated per row against rows already
updated, so the second and third "HDFC" both became "(2)" and the index still failed. A
window function computed once fixes it. `MigrationSafetyTests` builds a database at the old
schema, inserts the duplicates, and migrates forward.

### Messages nobody could see

Confirmations and errors rendered at the top of the page, so pressing Save at the bottom of
a long form appeared to do nothing and invited a second press. They are toasts now, above
the tab bar where the thumb already is. Errors do not auto-dismiss: a confirmation can slide
away unread at no cost, but the message telling you what went wrong is the only thing
standing between you and repeating it.

## 10. Scrolling, editing, and the trail that recorded nothing, 2026-09-01

| # | Issue | Cause and fix |
|---|---|---|
| 47 | **No page scrolled at all** | Mine, from the previous round. `html, body { overflow-x: hidden }` was added to stop the dashboard sliding sideways. `overflow-x: hidden` forces the computed `overflow-y` to `auto` and turns the element into a scroll container; setting it on both `html` and `body` in an Android WebView loses the viewport scroll entirely. **Fix:** `overflow-x: clip` on `body` alone, which does not create a scroll container. The sideways problem was already solved by the tile `minmax(0, 1fr)` |
| 48 | No way out of quick capture | It opens from a widget, so it is often the only thing on screen and the system back gesture was the only exit. Cancel button added |
| 49 | The confirmation never cleared | "Added successfully" sat there until the next save, over an empty form, with no telling whether it meant the thing just typed or the one before. Clears itself after 3.2s, keyed so a fast second save is not wiped by the first one's timer |
| 50 | **Transactions could not be edited** | The review flag marked rows nobody could fix, which made it a permanent stain rather than a nudge. Full edit page at `/transactions/{id}`, reachable by tapping any row |
| 51 | No delete | Now at the bottom of the edit page, in red, behind a confirmation. Soft: it leaves every balance and export at once (T9) and stays restorable |

### The audit trail was recording that something changed, but not what

`FR-8.2` asks for field-level before and after. The triggers wrote `Changes = NULL`, so the
trail could say a transaction was edited and nothing more. That is a rumour, not a record.

The triggers now build a JSON array of the fields that actually differed, using `IS NOT`
rather than `<>` because the latter is not null-safe: a note going from nothing to text
compares as `NULL`, which is not true, so the single most common edit would have recorded
silence. The `WHEN` clause also requires at least one tracked field to differ, so saving a
form without touching it adds no line.

**A design problem surfaced while doing it.** Trigger SQL names the columns it compares, so
it is derived from the schema the way an index is. Pinning its text inside the migration
that first created a table means every column added later is silently untracked, and nothing
fails when someone forgets: the trail just quietly stops mentioning that field. Triggers are
now dropped and recreated from their current definition on every startup, which makes drift
impossible for a few milliseconds a launch.

**On storing values.** The trail holds amounts and names, which is not a breach of NFR-5.9.
That rule is about logs. This is user-facing history inside the encrypted database, and it
is the entire reason a surprising number can be explained six months later.

## 11. Labels become tags, and a local backup, 2026-09-01

Seven pieces of feedback, all from the same instinct: fewer concepts, fewer words, and a
dialog where a dialog belongs.

| # | Asked | Done |
|---|---|---|
| 52 | The "Used for" text on a label is inconsistent, and why have it at all? A transaction's Kind is enough | `AppliesTo` removed from the model, not just from the screen. A label no longer declares a side, so nothing has to be kept consistent |
| 53 | "Rename a label" and "New label" should be a popup, not something you scroll past every label to reach | New `Modal` bottom sheet, used by both, plus a colour palette. It also carries the delete confirmations added below |
| 54 | Drop "Inside". Standalone labels, and **any transaction can have multiple, like tags in JIRA**. Forget tags as a separate feature | The reversal of ADR-0007, recorded as [ADR-0013](../adr/0013-multiple-flat-labels.md). Hierarchy gone, Tag entity gone, `TransactionLabel` join table in |
| 55 | Deleting a container should be as cautious as deleting a transaction | Confirmation sheet naming the container, its balance, and its transaction count. In use, it offers Archive rather than failing after the user has committed |
| 56 | Until Drive backup exists, give me export and import for local backup. JSON, keep it simple | `BackupFile` and `LocalBackup`: one JSON document, enums by name, ids preserved. Restore replaces rather than merges, and refuses a newer schema version |
| 57 | Replace quick capture's Cancel button with a cross, maybe with "cancel" as subtext | A cross glyph with the word beneath it, top right where a close control is looked for |
| 58 | Settings needs an About section like ytclipnshare, linking to the support page. Heart, GitHub and YouTube **icons**, not lots of text | A three-icon grid. Only the support heart is coloured, because it is the only one that asks anything of the user. The identity row above it opens the longer About page |

### The arithmetic problem ADR-0007 was built to avoid

0007 allowed exactly one label precisely so `Σ spend_by_label == total_spend` would hold.
Multiple labels breaks that, and the objection was correct: a 2,000 shop labelled both Food
and Household puts 2,000 in each bucket, so the buckets sum to 4,000 against 2,000 of real
money.

It is now handled rather than avoided. `SpendByLabelAsync` returns
`SpendBreakdown(ByLabel, TotalMinor)`, where `TotalMinor` is computed **from the
transactions, each counted once**, and never by summing the buckets. That is a structural
fix rather than a rule to remember: there is no code path that can add the buckets up by
accident, because the total arrives already computed. The chart scales its bars to the
largest label instead of to the total, and says on the same screen that a transaction with
two labels counts under both. NFR-3.9 was rewritten to assert what is actually true now: no
bucket exceeds the total, and the total matches an independent sum over transactions.

**Unlabelled became a real state.** There is no `Uncategorised` row any more, so quick
capture invents nothing, no seeded label is undeletable, and the breakdown carries an
explicit unlabelled bucket so that money stays visible rather than disappearing.

### The migration was the risky part

`MultipleFlatLabels` runs against real data on a phone. The scaffolded version dropped
`Transactions.LabelId` before creating the join table, which would have thrown every
existing categorisation away. Hand-written instead, in this order: create `TransactionLabels`,
copy every non-system `LabelId` into it, delete the system `Uncategorised` label, de-duplicate
names that the old per-parent uniqueness had allowed, then drop the columns and the Tag
tables. Tag assignments are deliberately **not** promoted to labels: a tag was documented as
never affecting arithmetic, so turning them into labels would silently change every
historical chart.

**A test-harness bug surfaced on the way.** `TestDatabase.DisposeAsync` called
`SqliteConnection.ClearAllPools()` so the temp file could be deleted. That pool is
process-wide, so a finishing test was closing pooled connections belonging to tests still
running in parallel, and one would fail to reopen at random. It now clears only its own
pool. Turning pooling off entirely was tried first and was worse: SQLCipher re-derives the
key on every open, and the suite went from ninety seconds to minutes.

### Moving labels out of the transaction row broke the audit trail, twice

Labels live in a join table now, and a trigger on `Transactions` cannot see rows written to
another table in the same save. Worse, the `Updated` trigger's `WHEN` requires a tracked
column on `Transactions` to differ, and `UpdatedAt` is not one, so **changing only a
transaction's labels left no trace at all.** `TransactionLabels` has its own pair of triggers
now, writing against the transaction so the entry lands in the timeline the user is looking
at, and resolving the label's name at trigger time so the trail still reads correctly after
that label is renamed or deleted.

That exposed a second problem in the edit path. It cleared the label collection and re-added
it, which with insert and delete triggers underneath means opening a form and pressing save
without touching anything writes "removed Food, added Food". It applies the difference now.

**And a third, which was the design mistake.** `InitialSchema` created the triggers by
calling `AuditTriggers.CreateStatements`. That is a migration depending on today's code, and
it broke the moment the trigger set grew a pair referencing `TransactionLabels`: replaying
the first migration on a fresh database failed with "no such table", three migrations before
that table exists. Every test in the class went red at once, which is the useful kind of
failure. Migrations now only ever drop triggers, by name; `DatabaseInitialiser` is the single
place that creates them, and it always sees the finished schema. The rule is written on
`AuditTriggers` itself so the next person does not rediscover it.

## 12. Final shape

All three steps of the brief are done.

**Built and verified.** 136 tests across six projects, 0 warnings, the whole solution
including the Android head builds. The app runs: create containers, capture Debit, Credit
and Self Transfer with any number of labels, see derived balances and net worth split three
ways, filter the list every way, export exactly what is filtered, back the whole thing up to
a JSON file and restore it, and paste a bank SMS to get a suggestion that records nothing
until you confirm it. The database is SQLCipher-encrypted with a key from the Android
keystore, migrations run at startup, and the audit trail is written by triggers.

**Not built, deliberately.** Goals (S14), Drive backup (S12), FX (S17), the Android SMS
receiver (S38), and biometric lock (S11). The next two prompts already queued, CI release
and the mascot, have their seams waiting: the release workflow is written and the
`IMascotService` boundary plus its JS module are in place. The full list of what is left,
entity by entity, is in section 13.

**Deliberately left open:** which GitHub account the project lives under, and the cold-start
measurement that NFR-1.1 needs from a real device.

---

## 13. What is still missing, entity by entity

Written against [domain-model.md](../requirements/domain-model.md) so the gap between the
model and the build is visible at a glance, rather than having to be rediscovered. Each row
names the slice in [roadmap.md](../roadmap.md) that closes it.

### Entities in the model with no table yet

| Entity | What is missing | Consequence today | Slice |
|---|---|---|---|
| **Goal** and **GoalContainer** | The whole thing: entity, `funding_mode`, linked containers, allocation warnings, `GL1`-`GL6`. `Transaction.GoalId` exists as a column but nothing writes it | No savings goals. Net worth is unaffected either way, since `GL3` keeps goals out of it by design | S14 |
| **FxRate** | Table, provider, and the manual-rate path | Aggregates skip foreign-currency containers rather than converting them. Dual amounts are stored correctly per `ADR-0004`, so nothing is lost, but a USD account does not appear in net worth | S17 |
| **UserSettings** | Home currency, cycle start day, theme, auto-lock delay | All four are hard-coded: INR, calendar month, system theme, no lock. Changing them means a rebuild | S11 |

### Columns that exist but nothing populates

| Column | State | Slice |
|---|---|---|
| `Container.InterestRateBps`, `MaturityDate`, `TenureMonths`, `InstallmentMinor` | On the entity and in the schema; no form collects them and nothing displays "matures in 3 months". Interest is never synthesised, which is deliberate | unscheduled |
| `Container.Ifsc`, `Icon`, `SortOrder` | Stored, never edited from the UI. Sort order is always insertion order | S11 |
| `Transaction.GoalId` | Waiting on Goal | S14 |
| `Transaction.Status` | Ships with `Cleared` only, deliberately reserved so that adding `Pending` and `Scheduled` is not a migration over real data | v2 |
| `Party.UsageCount`, `LastUsedAt` | Written and ranked on, so suggestions already improve with use. No gap | done |
| `App.DefaultContainerId` | Populated and used to auto-fill the source container. No gap | done |

### Behaviour the model requires that is not wired

| Rule | Missing piece | Slice |
|---|---|---|
| `SM3`-`SM6`, `SM9`-`SM11` | The Android `BroadcastReceiver`, the runtime permission flow, and the notification prompt. Parsing itself works today via paste, and `SmsBody` is already a `ref struct` so `SM1` is a compiler guarantee whichever path arrives | S38 |
| `FR-9.*` | Google Drive: OAuth with PKCE, Argon2id key derivation, the recovery code, AES-256-GCM, WorkManager upload, rolling retention. The UI placeholder is in Settings and says plainly that nothing is built | S12 |
| `FR-10.1`-`10.3` | Biometric lock, auto-lock, app-switcher masking. The database is encrypted, but an unlocked phone is an unlocked app | S11 |
| `FR-10.10` | Merging duplicate parties or apps. They can be created, never consolidated | S16 |
| `FR-5.9`-`5.13` | Net worth trend, month-over-month per label, top parties, month-in-review | S19, S25 |
| `FR-3.*` | Notification-based quick capture, and learned defaults by time of day and amount band | S21, S22 |
| `NFR-1.1` | The cold-start budget has never been measured on a real device. It is a number, and it is currently a guess | S11 |

### Known gaps that are not entities

- **The mascot is a placeholder.** `IMascotService` and its JS module are wired and called
  on save, but the drawing is a stand-in. The queued mascot prompt replaces it.
- **The release workflow has never run.** `.github/workflows/` is written and the secrets are
  documented, but no tag has been pushed, so it is untested.
- **Parse Rules cover one message shape.** They are data, so a new bank is a rule plus a
  corpus test, but the corpus is currently thin.
- **Restore is destructive by design and has no undo.** Exporting first is the only safety
  net, which is why the Settings copy says so before the button.
