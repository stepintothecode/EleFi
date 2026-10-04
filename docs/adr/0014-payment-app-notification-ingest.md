---
status: accepted
date: 2026-10-04
---

# Payment App notifications as a second alert channel, merged with SMS

EleFi reads notifications posted by an allow-listed set of Payment Apps (GPay, PhonePe,
Paytm, Amazon Pay, CRED) through Android's `NotificationListenerService`, parses them with
the same Parse Rule machinery as bank SMS, and turns a match into a **Capture Suggestion**,
exactly as [ADR-0010](0010-on-device-sms-assisted-capture.md) does for SMS. When a bank SMS
and a Payment App notification describe the same payment, they are merged into one
suggestion.

Nothing about `SM2` changes. A notification is evidence, not authority, and it records no
money until a human presses Add.

This is roadmap slice S40, which ADR-0010 deferred until it had its own ADR. This is that
ADR.

## Why

The user asked for it directly, for two reasons ADR-0010 alone cannot meet:

1. **Bank SMS do not know who was paid.** A UPI debit alert names a VPA
   (`zomato@hdfcbank`), a merchant code, or nothing. The Payment App knows the payee's real
   name and the note the user typed. Without it, every SMS suggestion needs its payee fixed
   by hand, which is most of the work the feature exists to save.
2. **Some payments produce no SMS.** Wallet-funded and some card-on-file payments are
   reported only by the app.

The bank's SMS still matters for the other half: it carries the last four digits, which is
the only honest way to know which container paid (`SM11`). The app never says which
account it debited. Neither channel is complete; together they usually are.

## The decisions

- **Allow-list first, read second.** The listener sees every notification on the device.
  The posting package name, which Android supplies and an app cannot forge, is compared with
  `PaymentApps` *before* the notification's title or text is touched. Everything else, from
  chats to OTPs from any app, returns on the first line unread. This is `SM3` restated for a
  second channel.
- **Same rule engine, separate channel.** A `ParseRule` gains a `Channel` (`Sms` or
  `PaymentApp`) and, for app rules, the `AppName`. A rule only ever sees alerts from its own
  channel, so a crafted SMS sender cannot be matched by an app rule or the reverse. Adding
  an app is a line in `PaymentApps`, a rule in `BuiltInParseRules`, and a corpus test: the
  same treadmill, bounded the same way.
- **Merge, do not duplicate.** One UPI payment usually raises both alerts within seconds.
  An alert completes a pending suggestion when it has the same amount to the paisa, the same
  currency and direction, comes from the *other* channel, and arrives within 15 minutes.
  The bank supplies the container; the app supplies the payee's name, the note, and the
  Payment App, which becomes the transaction's *Paid with*. A suggestion takes at most one
  alert per channel, so two genuine ₹20 payments never collapse into one.
- **Card bills are Self Transfers.** Paying a credit card through CRED or GPay is moving the
  user's own money; read as a Debit it would count every purchase on the card a second time,
  breaking `D1`. A card-bill rule runs ahead of the generic paid rule for every app, its
  suggestion is a Self Transfer, it pairs with the bank's Debit SMS, and the inbox asks which
  card was paid (`SM16`).
- **Settled payments only.** Any notification mentioning failed, pending, declined, reversed,
  refunded or requested produces nothing, wherever in the text that word appears.
- **A late alert for a payment already added is absorbed.** Pairing looks at confirmed
  suggestions as well as pending ones, so the bank's SMS arriving after the user added the
  app's alert is not offered again.
- **Duplicates stay suppressed after a merge.** Apps repost a notification when they update
  it. The merged-in alert's fingerprint is kept (`CorroboratingFingerprint`) so a repost is
  recognised as already offered (`SM6`), and a notification is fingerprinted by its own
  `When` rather than its post time, which changes on every repost.
- **Off by default, its own switch.** A separate in-app switch from the SMS one
  (FR-11.24). Turning it on opens the system screen where notification access is granted,
  because Android offers no runtime prompt for it.
- **No WorkManager hand-off.** Both receivers parse in-process (`goAsync` for SMS, a task
  for the listener) rather than queueing work, because WorkManager would serialise the
  message text to disk to survive process death, which `SM1` forbids.

## Considered options

- **No app notifications; SMS only.** Rejected: leaves every suggestion with a VPA for a
  payee and misses wallet payments, which is the gap the user named.
- **Read all notifications and filter by content.** Rejected without much debate. Content
  filtering means reading every chat and email on the device to decide whether to ignore it.
  Package gating means never reading them at all.
- **An Accessibility Service scraping the payment app's screen.** More data (the full
  receipt), far more invasive, against Play policy, and broken by every UI change. Rejected.
- **Use the app's notification alone and drop the SMS when both exist.** Rejected: the app
  does not say which account paid, and guessing a container from history is exactly what
  `SM11` forbids.
- **Book app-reported payments as Needs Review transactions.** Raised by the user's original
  request ("record a To be reviewed transaction") and declined in favour of keeping `SM2`,
  for the reasons ADR-0010 gives. A Payment App notification for a payment that then
  *failed* or was reversed is a real case, and booking it would corrupt balances silently.

## Consequences

- **A second invasive permission.** Notification access is broader than `RECEIVE_SMS`: the
  service is *delivered* every notification, even though it reads only allow-listed ones.
  The allow-list check is the first statement in the callback and is covered by tests at
  both the domain and the application layer.
- **Restricted settings on sideloaded builds.** On Android 13 and later, notification
  access for an app not installed from a store is blocked until the user allows restricted
  settings from the app's info screen. The UI says how; nothing tries to bypass it.
- **Wording is guesswork, and it drifts.** No Payment App documents its notification text.
  The shipped patterns accept the common phrasings and refuse anything saying failed,
  pending, or requested. A phrasing they miss produces nothing (FR-11.4), which costs one
  manual entry. Real-device samples should be added to the corpus as they are seen.
- **`SM1` covers notification text too.** It travels as the same `SmsBody` ref struct
  inside the parser, is never logged (receiver failures log the exception *type* only), and
  no column can hold it. A test asserts the suggestion table has no body or sender column.
- **`CaptureSource.PaymentApp`** is new, for a confirmed suggestion only an app described.
  One both channels described records `Sms`, because the bank's message names the account.
- **Play distribution** (S30) would need a notification-listener declaration and review on
  top of the `RECEIVE_SMS` one. Under sideloaded distribution this costs nothing today.
