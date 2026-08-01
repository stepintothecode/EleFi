# EleFund

Local-first money tracker. Expo (React Native) + TypeScript, SQLite on device, no backend.

## Read these first

| Document | For |
|---|---|
| [docs/CONTEXT.md](../docs/CONTEXT.md) | Domain glossary. **Use this vocabulary everywhere** — code, UI copy, commits, conversation |
| [docs/requirements/domain-model.md](../docs/requirements/domain-model.md) | Entities, fields, numbered invariants |
| [docs/requirements/functional.md](../docs/requirements/functional.md) | Numbered FRs and user stories |
| [docs/requirements/non-functional.md](../docs/requirements/non-functional.md) | Performance, animation, security budgets |
| [docs/requirements/software-development.md](../docs/requirements/software-development.md) | Stack, structure, testing, Definition of Done |
| [docs/adr/](../docs/adr/) | Decisions that are hard to reverse — check before contradicting one |
| [docs/roadmap.md](../docs/roadmap.md) | Slices and their blocking edges |

## Conventions

**Always maintain the decision log.** When a session produces decisions — planning,
scoping, architecture, grilling — record the prompts and answers using the `decision-log`
skill ([.claude/skills/decision-log/](skills/decision-log/SKILL.md), invoke with
`/decision-log`). Logs live in [docs/prompts/](../docs/prompts/) as
`YYYY-MM-DD-<topic-slug>.md`. If a log already covers the thread of work, update it rather
than creating a parallel file. Chat history is not a record.

**Say Container, not account.** "Account" means a login identity or the specific
BankAccount kind. Saying it for the general concept is how this model gets muddled.

**Say Source and Destination, never To and From.** To/From were the ambiguous sketch terms
that this model exists to replace.

## Non-negotiables

These are invariants, not preferences. Breaking one is a bug even if tests pass.

- Money is **integer minor units**. No float ever holds, transports, or computes money.
- Balances are **always derived**, never stored — no `current_balance` column, at any layer.
- Transaction kind is **derived** from its two parties. It is never a stored field.
- **Self Transfers are excluded from every spend and income aggregate.** This is what stops
  credit-card bill payments double-counting.
- Deletes are **soft**. No user-entered data is destroyed by normal operation.
- The only egress path for user data is the **encrypted Drive backup**. A new one requires
  an ADR, not a PR comment.
- Only `drive.appdata` scope. Broader Drive scopes are restricted and forbidden.
- Only the **last 4 digits** of an account number are ever stored.

## Working here

- `domain/` imports nothing from React, SQLite, or `expo-*`. Keep it pure and testable.
- Only `repositories/` touch the database.
- New domain vocabulary goes into `CONTEXT.md` in the same commit that introduces it.
- Charts follow the `dataviz` skill.
