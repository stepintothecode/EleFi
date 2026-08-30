# EleFund - Original brief and decision log

Grilling session held 2026-08-01 (`/grill-with-docs`). This file is the durable record of
what was asked and what was decided, so nothing depends on chat history surviving.

> **The project was renamed EleFund → EleFi on 2026-08-29, and the stack changed from
> Expo/TypeScript to C#/.NET MAUI on the same day.** Nothing below is edited: the name and
> the decisions are recorded as they were made, because a log that gets rewritten to match
> the present is not a log. For what changed and why, see
> [2026-08-29-elefi-pivot.md](2026-08-29-elefi-pivot.md) and
> [ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md).
>
> In particular, decision 4 below (Expo over C#) was reversed. See
> [ADR-0005](../adr/0005-expo-typescript-over-csharp.md), which is kept in full.

Resulting documents: [CONTEXT.md](../CONTEXT.md) ·
[docs/requirements/](../requirements/) · [docs/adr/](../adr/) ·
[docs/roadmap.md](../roadmap.md)

---

## 1. The original brief (verbatim, unedited)

> /grill-with-docs I want to build a full fledged hosted mobile app or web app (something that is platform independent) for money tracking. Name of the app/brand- EleFund. An elephant will be the mascot. I want the animations in the app to be sleak like Duolingo, perfect animations and nice UI UX. The features of this app should be as follows-
> 1. Most important- data filling. Which means, the user can enter data of their money transaction... Like any Credit, Debit, or SelfTransfer.
> 2. The data filling should be very user friendly and usable i.e. users should just get an option to enter their data even without opening the app, e.g. a small floating icon on screen of phone or a notification tab or a home screen widget. This feature is very very important.
> 3. User should be able to add their accounts (bank accounts with account number, bank name, and account type), cards (credit card especially), RD accounts, FD accounts, PPF account, etc. and wallets i.e. unlike bank accounts and cards, other money containers like wallet (cash),  or piggy bank, or whatever physical money container.
> 4. I would like the transactions to be tracked with following info- (columns of the table) Type (Credit/Debit/SelfTransfer), Labels (e.g. Food, Travel, Shopping, etc.), To, From, Description, Amount, Relavant Portal/App (if any, e.g. Uber, Zomato, Blinkit, Swiggy, Rapido, etc.), Payment Portal/App (e.g. GPay, HDFC Net Banking, Cash etc.), Date.
>   - Keep the column names meaningful, these are just my suggestions
> 5. For the previously filled data, e.g. Name of a person, this should be stored in database, because I want auto-suggestions and a drop down with all previously filled values, for each column. (Mix the data of To and From columns, and of Relavant Portal/App and Payment Portal/App columns)
> 6. I want financial goals to be set. A goal will have a target amount, and a target period to achieve it. And account(s) or whatever from point no. 3 above, the user wants to link to that financial goal. e.g. from this account, or this SIP, x amount is for engagement ring purchase, every month, and I want to achieve Rs. 3Lakhs by Aug 2028.
> 7. Should we have trails... like any activity that happens on one transaction, the user can track it. Like JIRA tickets, any edit or any status change should be trackable.
> 8. A dashboard describing users their total wealth/savings/debt, and all details on expenditures like which label the user wants to see. And financial goals, if they are meeting it or not. Monthly report too..
>
> Also, remember that I would like to go step by step. So the plan should be one foundational feature at a time. Not everything at once. If for example, widget or floating icon thing is a separate and add on like, then it can be done in later versions also. But the bare metal app should be made first.

---

## 2. Decision log

`†` = superseded by the local-first pivot (Q15b). `⤵` = full text in §3.

| # | Question asked | Your answer |
|---|---|---|
| 1 | Who is EleFund for at v1? | Just me, but multi-tenant-safe - `user_id` on every row, real auth from day one † |
| 2 | Which platforms, and what phone do you carry? | All three equally from day one - **plus: use C# for backend** † |
| 3 | Frontend framework? | Expo / React Native + RN Web |
| 4 | Core transaction shape? | One row, typed source + destination; kind derived ⤵ |
| 5 | How deeply to model container types? | One table + `kind` + a few kind-specific fields |
| 6 | Marketplaces vs payment rails? | One `App` entity used in two roles, shared suggestion pool |
| 7 | How should labels work? | One hierarchical label (2 levels) + unlimited free tags |
| 8 | Stored or derived balances? | Always derive from transactions |
| 9 | Multi-currency? | **Full multi-currency from day one** |
| 9b | How to carry FX amounts? | Separate source & destination amounts |
| 9c | Where do rates come from? | Nightly fetch → own rate table + manual override † |
| 10 | How much must work offline? | Offline writes + cached reads † |
| 11 | What does a fast capture create? | A real transaction, defaulted, flagged `needs_review` |
| 11b | Order of quick-entry surfaces? | v1 in-app → v2 notification + tile + widget → v3 bubble / iOS |
| 12 | How is goal progress measured? | Per-goal funding mode: container balance **or** contributions |
| 13 | How deep should the audit trail go? | Generic audit log + soft delete † *(interceptor → SQLite triggers)* |
| 14 | v1 dashboard scope, and budgets? | Lean v1: wealth tiles + this-cycle spend + goals. **No budgets** |
| 15 | Authentication? | ASP.NET Core Identity + JWT, self-hosted † |
| 15b | Where does backend + DB run? | **PIVOT - local DB on device, Google sync, paid CSV export** ⤵ |
| 16 | What happens to the C# backend? | No backend at all ⤵ |
| 16b | So what's the stack now? | Expo / React Native + TypeScript |
| 17 | Backup or true sync in v1? | Backup/restore in v1, true sync in v2 |
| 18 | Encrypt the Drive backup? | Encrypted by default + one-time recovery code |
| 19 | Public paid product, and when? | Private v1, public + paid later |
| 20 | How rich should filters be? | Composable filters + saved presets |
| 21 | Which platform first? | **Android first - I carry Android** |
| 22 | Where's the v1 cut line? | Daily-driver v1 |

---

## 3. Verbatim answers you typed

**Q2 - platforms**

> All three, equally, from day one. But if possible, use C# for backend, as I am more comfiortable with that

**Q4 - transaction shape**

> One row, typed source + destination. I am fine with this first option. But please do not implement or start coding right now. I want you to plan, plan very properly. I want md file docs listing all the requirements tht we are deciding. List down all functional and non-functional requirements in each md files. And a software development requriements in another md file. Everything with its own concerns, and documenting all kinds of details as much as possible. So that implementation will be then planned accordingly. Use some relavant skills also for this if needed, they are present in /.agents/skills location

**Q15b - the pivot**

> Wait wait wait. Design like this- every time the database is local, in the device. And if sync is on, then use the user's Google account to sync. Also, a button in case the user wants to export to csv (I will make this export feature as a paid feature).  This export feature will be like this- user will be able to first filter in the wall transactions list, with whatever filter user wants, and then they can click on the above export to csv button to export. e.g. a user can select all transactions from 1st Apr 2025 to  31st Mar 2026, and only credit and debit, no selfTransfer, and all related to one specific bank. And then export.

**Q16 - what C# meant**

> No backend at all. SQLite on device, google drive for sync. FX rates pulled from free keyless public API. When I said I want C# in backend, I meant for the app in general, like I would like to have the apps full backend where all the classes are present and etc. to be in C#. But if it doesn't make sense, then to keep simple we can skip C# also, I am fine, just that I am more fluent in C# than other tech stacks

---

## 4. What the pivot changed

The local-first pivot at Q15b invalidated five earlier answers. Each was re-decided on the
new facts, not quietly dropped:

| Was | Became | Why |
|---|---|---|
| ASP.NET Core backend + PostgreSQL | No server at all | Nothing left for a server to hold |
| ASP.NET Identity + JWT | No app account; Google Sign-In only for backup | Nothing to log in to |
| Managed hosting | None - zero running cost | No server |
| Offline writes + cached reads | Everything local by construction | Simpler *and* better |
| Audit via EF Core interceptor | Audit via SQLite triggers | EF Core doesn't run on the device |
| All three platforms in v1 | Android first | Without a server, the web build needs its own storage stack (~2-3 weeks) |
| C# for the whole app | TypeScript | No server ⇒ all-C# or none. Chose animation quality over C# fluency |

The last one is recorded in [ADR-0005](../adr/0005-expo-typescript-over-csharp.md)
specifically so a future reader doesn't "correct" it back.

---

## 5. Final shape

**Stack** - Expo (React Native) + TypeScript · SQLite on device via `op-sqlite` + Drizzle ·
Google Drive (`drive.appdata`) for encrypted backup · FX from a free keyless API · **no server**.

**Model** - Transactions have a typed source and destination party (internal container or
external person/merchant); Credit / Debit / SelfTransfer is derived, never stored. Balances
are always computed. Money is integer minor units. One label per transaction plus free tags.

**v1.0** - Containers · capture · list with filters · derived balances and net worth · audit
trail with undo · lean dashboard · encrypted Drive backup · biometric lock. Goals, CSV export,
and FX features follow in v1.1-v1.2.

**Deliberately excluded** - budgets, split transactions, double-entry postings, interest
accrual, recurring transactions, shared accounts, statement import, SMS parsing.
Rationale for each is in [domain-model.md §15](../requirements/domain-model.md).
