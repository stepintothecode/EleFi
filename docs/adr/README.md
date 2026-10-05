# Architecture Decision Records

Decisions that are **hard to reverse**. Check here before contradicting one.

An ADR records what was decided, what else was considered, and what it costs. Superseded
ADRs are kept in full and never edited to look right in hindsight - the reasoning that
turned out wrong is usually the most useful thing in the file. Superseding ADRs link both
ways.

| # | Decision | Status |
|---|---|---|
| [0001](0001-local-first-no-backend.md) | SQLite on device is the source of truth, no backend | accepted |
| [0002](0002-typed-parties-derived-kind.md) | Typed source/destination parties; transaction kind is derived | accepted |
| [0003](0003-derived-balances.md) | Balances are always derived, never stored | accepted |
| [0004](0004-multi-currency-dual-amounts.md) | Each end of a transaction carries its own amount and currency | accepted |
| [0005](0005-expo-typescript-over-csharp.md) | Expo/TypeScript over C# | **superseded by 0009** |
| [0006](0006-google-drive-encrypted-backup.md) | Client-side encrypted backup to the user's Google Drive; sync deferred | accepted |
| [0007](0007-single-label-plus-tags.md) | Exactly one Label per transaction, plus unlimited Tags | **superseded by 0013** |
| [0008](0008-audit-via-sqlite-triggers.md) | Audit trail written by SQLite triggers; soft deletes everywhere | accepted |
| [0009](0009-dotnet-maui-over-expo-typescript.md) | C# on .NET MAUI, reversing 0005 | accepted |
| [0010](0010-on-device-sms-assisted-capture.md) | SMS parsed on device into suggestions, never booked automatically | accepted, **SM2 superseded by 0015** |
| [0011](0011-blazor-hybrid-over-native-xaml.md) | Blazor Hybrid rather than native XAML for the UI layer | accepted |
| [0012](0012-free-forever-voluntary-support.md) | Free forever, MIT licensed, funded only by voluntary support | accepted |
| [0013](0013-multiple-flat-labels.md) | Any number of flat Labels per transaction; Tags removed, reversing 0007 | accepted |
| [0014](0014-payment-app-notification-ingest.md) | Allow-listed Payment App notifications as a second alert channel, merged with SMS | accepted, **SM2 parts superseded by 0015** |
| [0015](0015-record-alerts-as-needs-review.md) | Parsed alerts are recorded straight away as Needs Review transactions, reversing SM2 | accepted |

## The load-bearing ones

If you only read three: **0001** (no backend) is the decision every other one sits on,
**0003** (derived balances) is what makes the numbers trustworthy, and **0015** is what lets a
text message change a balance, flagged for review rather than silently.

## Writing a new one

Number sequentially, name the file `NNNN-kebab-case-summary.md`, and open with YAML
frontmatter carrying `status`. Keep the structure the existing files use: the decision and
why, **Considered options** with a real reason each was rejected, then **Consequences**
including the ones that hurt. An ADR listing only benefits is a sales document, not a
record.

Write one when a decision would be expensive to unwind: storage, egress, schema shape,
platform, or anything touching the [non-negotiables](../../.claude/CLAUDE.md). Not for
things a pull request can undo.
