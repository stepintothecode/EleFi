---
status: accepted
date: 2026-10-05
supersedes: SM2 of ADR-0010, and the "suggestion, not transaction" parts of ADR-0014
---

# Record parsed alerts straight away, flagged Needs Review

A bank SMS or a Payment App notification that a Parse Rule reads is now **recorded at once
as a real Transaction, flagged Needs Review**, exactly as a Quick Capture is. It counts in
every balance immediately. The "To confirm" inbox of Capture Suggestions is removed.

This reverses `SM2` ("a parsed SMS never becomes a transaction without explicit user
confirmation"), which [ADR-0010](0010-on-device-sms-assisted-capture.md) called load-bearing
and which [ADR-0014](0014-payment-app-notification-ingest.md) carried over to app
notifications. Both ADRs are left as written; this one says what changed and why.

## Why

The user, who is the only user, chose this explicitly on 2026-10-05, having chosen the
opposite on 2026-10-04 and lived with it for a day:

> I dont like the idea of payments to confirm in the settings page. just add the payment in
> the all transactions page, with "needs a review" tag, remove this whole "To confirm" kind
> of page.

> Always record the SMS parsed payments and event listened payments of GPay and other
> payment apps via notifications, record them under transactions with "needs a review" tag.

The inbox put a second place to look between a payment and the ledger. In practice that is
the gap the whole app exists to close: an unconfirmed payment is as invisible to the
balances as an unrecorded one, and confirming each one by hand is the chore that killed the
previous attempts at this app. Needs Review already means "real, counted, check it later",
and the user would rather fix the occasional wrong one than confirm every right one.

## What still holds

- **`SM1`**: no message text is stored, logged, audited, backed up or exported. Unchanged.
- **`SM3`, `SM4`, `SM12`, `SM13`**: the sender and allow-list gates, the OTP refusal, and
  channel separation. Unchanged. A message no rule reads still produces nothing at all.
- **`SM6`**: one payment, one transaction. The Capture Suggestion row survives as the link
  between the alerts and the transaction they made, created already confirmed, so a
  repeated or reposted alert is still recognised, and the bank's SMS and the app's
  notification for one payment still become one transaction, in either order (`SM14`).
- **`SM8`**: every recorded transaction goes through `CaptureService` or
  `EditTransactionService`. T1 to T9 apply. There is no privileged write.
- **`SM11`**: only an exact last-four match is treated as knowing the account.
- **`D1`**: a card bill read from an app is recorded as a Self Transfer, never as spending.

## The decisions this forced

- **There is always a container.** A transaction needs a Source. When the alert names none
  (every Payment App notification, and some SMS), it is guessed, in order: the Payment App's
  default container, the container last used with that app, the first container in picker
  order (skipping cards for a card bill). The Needs Review flag is what says it was a guess.
  With no containers at all, nothing is recorded and the user is told why.
- **There is always a payee.** An alert naming nobody is recorded against the Payment App's
  name, or "Unknown payee", rather than not at all.
- **A late alert completes, it does not overwrite.** The second alert for a payment updates
  the transaction the first one recorded (the bank's card, the app's payee and note) only
  while it is still flagged. Once the user has reviewed it, their answer stands.
- **The prompt says "recorded".** The notification is now a statement with Open and Delete.
  Delete is a soft delete, for a promotional message that parsed or a payment that later
  failed, and is restorable.
- **Deletes became recoverable in the UI.** Soft delete always kept the row; a Deleted
  screen in Settings now lists them with Restore and Restore all. A restored transaction
  comes back flagged for review.
- **Teaching replaces pasting.** The permission-free path is now "Teach EleFi a message":
  paste a message, see what EleFi reads, record it, or mark the values in it so EleFi builds
  a Parse Rule from that one example (`RuleInducer`), checked against the example before it
  is saved.

## Considered options

- **Keep `SM2` and improve the inbox.** Rejected by the user after using it.
- **Record, but exclude Needs Review transactions from balances until reviewed.** A third
  state that is neither counted nor gone; every report would need to explain it, and it is
  the inbox again under another name.
- **Record only when both channels agree.** Safer, but most payments produce one alert, so
  most payments would still go unrecorded.

## Consequences

- **A wrong parse moves a balance until it is fixed.** The first time a promotional message
  parses as a ₹50,000 debit, net worth is wrong until the user deletes it. Mitigations: the
  rules refuse failed, pending, declined, reversed, refunded and requested wording; the
  prompt arrives within seconds with a Delete button; the dashboard shows how many
  transactions need review; and nothing is ever destroyed.
- **`CLAUDE.md`'s non-negotiable is rewritten**, from "never becomes a transaction without
  confirmation" to "is recorded as Needs Review, never silently".
- **Glossary**: Capture Suggestion is now internal, the link between alerts and a
  transaction; the Suggestion Inbox is gone; the Suggestion Prompt becomes the Alert Prompt;
  a Taught Rule is new.
- **Play distribution** (S30), if ever wanted, now has a feature that books money from SMS
  automatically, which is the variant most likely to draw policy scrutiny. Under sideloaded
  distribution this costs nothing today.
