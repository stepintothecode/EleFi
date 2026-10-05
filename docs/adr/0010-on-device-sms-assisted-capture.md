---
status: accepted, SM2 superseded by ADR-0015 (2026-10-05)
date: 2026-08-29
---

> **Superseded in part.** `SM2`, the rule that a parsed SMS never becomes a transaction without
> confirmation, was reversed by [ADR-0015](0015-record-alerts-as-needs-review.md): parsed alerts are
> now recorded straight away, flagged Needs Review. The rest of this ADR stands.

# SMS-assisted capture: parse on device, suggest, never book

EleFi reads incoming SMS on Android, matches the sender against bundled Parse Rules,
parses matching messages into a **Capture Suggestion**, and raises a local notification
offering to record it. Confirming creates a normal Transaction. **Nothing is ever recorded
without that confirmation.**

Two paths that need no SMS permission at all - paste a message, and share a message into
the app - are built first and ship independently of the SMS receiver.

We chose this because the gap between money moving and money being recorded is the thing
that killed every previous attempt at this app (the problem statement in
[functional.md](../requirements/functional.md)), and for a large share of Indian
transactions the bank has already sent a message describing exactly what happened, to a
device the app is running on. Not using it is leaving the cheapest possible capture on the
table. Reading it and *believing* it, however, is how a money tracker silently becomes
wrong.

## The decision that matters: suggestion, not transaction

`SM2` is the load-bearing rule. A parsed message produces a `CaptureSuggestion`, which is
a different entity from a `Transaction`. It contributes to no balance, no aggregate, no
report, no export, and no backup. It becomes real only when a human agrees.

This was not the obvious choice. The app already has `needs_review`, a flag meaning "this
was captured fast, check it later", and reusing it would let SMS create real transactions
immediately with far less code.

It is rejected because the two flags assert different things. `needs_review` means *you*
recorded this and should check it. A suggestion means *a machine thinks this happened*.
Collapsing them puts unverified pattern-matching output directly into balances, and the
first time a promotional message parses as a ₹50,000 debit, every number in the app is
wrong and the user has no way to know why. The distinction costs one table and one screen,
and it converts an entire class of silent-corruption failure into a dismissed notification.

## Considered options

- **Parse and auto-create, flagged `needs_review`.** Less code, faster capture, and the
  variant most competing Indian apps ship. Rejected above. The app's central claim is that
  its balances can be trusted; an automated writer that is right 95% of the time destroys
  that claim more effectively than any missing feature.
- **No SMS at all, manual capture only.** The position the docs held until today, recorded
  in domain-model.md's "deliberately absent" list on Play-policy grounds. Rejected because
  the policy risk is real but is a distribution problem, and it turns out to have a clean
  answer: build the permission-free paths first, so the feature degrades rather than
  vanishing.
- **Notification-listener access instead of SMS.** `BIND_NOTIFICATION_LISTENER_SERVICE`
  reads bank *app* notifications rather than messages. Broader coverage for issuers that
  have stopped sending SMS, and it avoids the restricted SMS permission. Deferred to S40
  rather than rejected: it is an even more invasive permission (it sees every notification
  on the device), it carries its own policy review, and it needs its own ADR.
  *Update 2026-10-04:* adopted alongside SMS rather than instead of it, for an allow-list of
  Payment Apps, in [ADR-0014](0014-payment-app-notification-ingest.md). `SM2` is unchanged.
- **Remote-updatable Parse Rules.** New bank formats would become a data push rather than a
  release, which is genuinely better operationally. Rejected for v1.2: it opens a network
  channel that controls how the app reads the user's messages, which is a security decision
  disguised as a convenience. Rules ship bundled (FR-11.26, NFR-5.17), and users can write
  their own (FR-11.17).
- **Statement import instead.** Already on the roadmap as S36 and unaffected by this. It
  solves history, not the three-second capture problem.

## The Play policy risk, stated plainly

`RECEIVE_SMS` is a **restricted permission** under Google Play's SMS and Call Log
Permissions policy. Access is limited to an enumerated set of permitted use cases, and an
app requesting it must file a Permissions Declaration and be approved.

**Whether personal finance transaction tracking qualifies must be verified against the
current policy text before any Play release depends on it.** It is not safe to assume it
does, the enumerated cases have historically centred on default SMS/phone handlers and a
short list of exceptions, and this policy has changed more than once. Treat any recollection
of how other Indian finance apps handled this as a lead to check, not as precedent.

**This risk is largely moot under the current distribution plan.** The intended route is
direct sideloading via signed APKs on GitHub Releases, consumed through Obtainium and
IzzyOnDroid, with no Play Console involvement. Google's restricted-permission policy governs
*Play distribution*, not what an Android app may do; a sideloaded build declares
`RECEIVE_SMS` and installs. F-Droid-family channels have their own requirements - chiefly
reproducibility and an anti-features label for a permission like this - but nothing
resembling a Permissions Declaration review.

So the policy risk is **conditional on a decision not yet made**: it costs nothing unless
EleFi later goes to Play. The mitigation below is kept anyway, because it is cheap, because
it is the only version of this feature iOS can ever have, and because it keeps the Play
door open at no ongoing cost.

**The mitigation is the ordering, not a workaround.** Slice S37 builds parsing plus the
paste and share-sheet paths, with no SMS permission anywhere in it. Slice S38 adds the
receiver. If the declaration is refused, or the policy turns out to exclude this use case,
S38 is compiled out of the Play build (NFR-11.2a) and the feature survives at reduced
convenience. The same ordering is what gives iOS a version of this feature at all, since
iOS will never allow reading messages.

## Consequences

- **A new entity and a new lifecycle.** `CaptureSuggestion` and `ParseRule`, with hard
  delete on dismiss and expiry - a deliberate exception to the soft-delete rule, because
  soft delete protects user-entered data and a suggestion is machine output the user
  rejected. See `SM5`.
- **`SM1` is absolute and enforced by the type system, not by review.** An SMS body is a
  `ref struct` that cannot be assigned to a field, boxed, captured by a lambda, or
  serialised. It cannot be logged, stored, audited, backed up, or exported because there is
  no expressible code path that does so. A single exception would make the rule
  unauditable, which is why there are none.
- **No new egress path.** Nothing derived from a message is transmitted anywhere.
  [NFR-5.1](../requirements/non-functional.md), the product's central promise, is exactly as
  strong as it was. This ADR does not touch it.
- **OTPs are the sharpest hazard.** Granting SMS access hands the app the user's second
  factor. `SM4` forbids parsing them, and the corpus test that enforces it runs against
  *every* rule including user-written ones, not just builtins.
- **A maintenance treadmill, bounded on purpose.** Bank message formats change. Rules are
  data (NFR-8.11), users can add their own, and a miss costs exactly one manual entry -
  which is the baseline the app already lives at. This is a feature that degrades to
  nothing worse than the status quo.
- **Android only, permanently.** NFR-10.7 requires the feature to be absent rather than
  broken elsewhere: no screen, setting, or empty state mentioning it appears on a platform
  that cannot do it.
- **The permission is optional and independently revocable** (`SM10`, FR-11.20), with an
  in-app kill switch that does not require a trip to system settings (FR-11.24). A feature
  this invasive has to be an offer.
- **The receiver is a reliability liability.** OEM battery managers kill background
  receivers (NFR-10.3), so it runs through WorkManager and must fail visibly (NFR-10.8). A
  silently dead receiver is worse than no feature, because the user stops recording things
  believing the app is watching.
- **This partly reverses a "deliberately absent" entry** in
  [domain-model.md](../requirements/domain-model.md) §16. That entry is not deleted: it is
  rewritten to forbid the thing that is actually dangerous - automatic *booking* - while
  permitting parsing. The original concern was right about the risk and wrong about which
  half of the feature carried it.
