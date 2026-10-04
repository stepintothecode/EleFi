# EleFi

A local-first money tracker for Android. Your ledger lives in an encrypted SQLite database
on your phone, and the only place it ever goes is an encrypted backup in your own Google
Drive.

There is no server, no account to create, and no bank connection. Nothing to pay for, and
nothing to keep running.

## What it does

- **Capture a transaction in about three seconds**, offline, with everything
  autocompleted from what you have typed before.
- **Models money properly.** Bank accounts, credit cards, cash, wallets, FDs, RDs and PPF
  are all containers. Balances are derived from transactions, never stored, so back-dating
  and editing are always correct. A credit card bill payment moves money without counting
  as spending.
- **Reads your bank's SMS and offers you the transaction** (Android). It parses the alert
  on the device and asks. It never records anything by itself, and it never keeps the
  message.
- **Exports what you are looking at.** Filter the transaction list however you like, tap
  Export, and the CSV contains exactly those rows. No filters means everything.
- **Labels like tags.** Attach as many as fit, or none. The breakdown reports its total
  separately from the per-label figures, because a transaction with two labels counts under
  both and the app says so rather than quietly disagreeing with itself.
- **Remembers what changed.** Every edit, delete and restore is in a per-transaction
  timeline, and deletes are undoable.
- **Backs up to a file you keep.** One JSON export of everything, and a restore that puts it
  back. Plaintext, so the app tells you that before you save it.
- **Tracks savings goals** against real container balances or attributed contributions.

## Status

Usable, and not finished. Containers, capture, the filtered list, CSV export, editing with
an audit trail, labels, and local JSON backup all work on a real phone. Not built yet:
Google Drive backup, savings goals, currency conversion, biometric lock, and the Android SMS
receiver (pasting a bank message works today). See [docs/roadmap.md](docs/roadmap.md) for
the slice plan.

## Built with

C# 14 on .NET 10, .NET MAUI Blazor Hybrid, EF Core over SQLCipher-encrypted SQLite.
Android first, with a web build planned. No backend of any kind.

## Getting started

Requires the .NET 10 SDK, the `maui-android` workload, JDK 17, and the Android SDK.

```
dotnet restore
dotnet build
dotnet test
dotnet build src/EleFi.App -t:Run -f net10.0-android
```

`dotnet test` passes with no device attached. Full setup, including the Google OAuth
client needed for backup, is in [docs/setup.md](docs/setup.md).

## Layout

```
src/     EleFi.Domain -> EleFi.Application -> EleFi.Infrastructure, EleFi.Ui, EleFi.App
tests/   one test project per src project, mirroring its folder tree
docs/    glossary, requirements, decision records, roadmap
```

## Documentation

| Document | For |
|---|---|
| [docs/CONTEXT.md](docs/CONTEXT.md) | The domain glossary. Start here - this vocabulary is used everywhere |
| [docs/requirements/](docs/requirements/) | Domain model, functional and non-functional requirements, how it gets built |
| [docs/adr/](docs/adr/) | Decisions that are hard to reverse, and what they cost |
| [docs/roadmap.md](docs/roadmap.md) | Slices, dependencies, and cut lines |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Layout, conventions, how to run the tests |
| [SECURITY.md](SECURITY.md) | What data exists, where it goes, and how to report a problem |

## Privacy

Your financial data leaves the device in exactly one way: an AES-256-GCM encrypted backup
to your own Google Drive, using only the `drive.appdata` scope, encrypted before it is
uploaded with a key Google never sees. There is no analytics, no telemetry, and no crash
reporting that carries your data.

If SMS reading is enabled, messages are matched and parsed entirely on the device. No
message body is ever stored, logged, backed up, exported, or transmitted, and OTP messages
are never read at all.

## Support

EleFi is free. Every feature, permanently: no paid tier, no unlock, no supporter build.
See [ADR-0012](docs/adr/0012-free-forever-voluntary-support.md).

If you want to chip in anyway:
**[stepintothecode.github.io/support](https://stepintothecode.github.io/support/?from=elefi-repo)**

A voluntary tip, not a purchase. It buys no features, no priority support, and no say over
the app.

## Licence

[MIT](LICENSE).
