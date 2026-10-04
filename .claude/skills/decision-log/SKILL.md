---
name: decision-log
description: Capture a session's prompts and decisions into a durable markdown log committed to the repo, so nothing depends on chat history surviving. Use when the user asks to document/record/capture what was decided, at the end of a planning or grilling session, or when a session has produced decisions that aren't written down anywhere else.
argument-hint: "Optional: topic name or path to an existing log to update"
---

# Decision Log

Write the session's **prompts and decisions** to a markdown file in the repo. Chat history
is not a record - this file is.

## When to write one

Write or update a log when a session has produced **decisions that aren't captured
anywhere else**. Typically: a planning session, a grilling session, an architecture
discussion, a scoping conversation.

Do **not** write one for sessions that only produced code, or where the reasoning already
lives in ADRs, specs, or commit messages. This log records *what was asked and chosen*,
not *what was built*.

## Where it goes

1. If the user named a file, use it.
2. If a log already covers this thread of work, **update it** rather than creating a
   second one. Look in `docs/prompts/`, `docs/decisions/`, and `docs/sessions/`.
3. Otherwise create `docs/prompts/YYYY-MM-DD-<topic-slug>.md`.

Never put it in a temp directory. The whole point is that it lives in the repo.

## Rules

- **The user's own words are reproduced verbatim.** Never paraphrase, tidy, correct
  spelling, or "improve" anything the user typed. Typos stay. This is the part that has
  to survive.
- **Every question gets its answer.** A decision with no visible question is unreadable in
  six months.
- **Reversals are shown, not erased.** When a later answer invalidated an earlier one,
  mark the earlier one superseded and record what changed and why. The reversals are
  usually the most valuable thing in the file.
- **Don't duplicate other documents.** If an ADR, spec, or requirements doc already
  explains a decision, link to it rather than restating it.
- **Redact secrets** - API keys, tokens, passwords, personal identifiers.
- **Stay minimal.** A table beats prose. Long verbatim quotes go in their own section so
  the table stays scannable.
- Convert relative dates ("next month") to absolute ones.

## Structure

Adapt as the session warrants; skip sections that would be empty.

```md
# <Project/topic> - brief and decision log

<Session date and how it started, e.g. "/grilling session, 2026-08-01">
<Links to the documents this session produced>

## 1. The original brief (verbatim, unedited)

> <the user's opening prompt, exactly as typed>

## 2. Decision log

`†` = superseded later. `⤵` = full text in §3.

| # | Question asked | Your answer |
|---|---|---|
| 1 | <the question, short> | <the decision> |

## 3. Verbatim answers you typed

<Only the answers the user typed out by hand, quoted exactly.
Skip answers that were a plain menu selection.>

## 4. What changed mid-session

<Before/after table of reversals, with the reason each one flipped.
Omit if nothing reversed.>

## 5. Final shape

<A few lines: what was settled, what's explicitly out of scope.>
```

## After writing

Tell the user the path and what's in each section. Offer to commit it - this file is only
useful if it's in version control.
