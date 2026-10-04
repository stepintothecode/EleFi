# Security

EleFi holds a complete record of one person's finances on their phone. This document says
what exists, where it goes, and what it is protected by. The requirements behind it are
NFR-5 in
[docs/requirements/non-functional.md](docs/requirements/non-functional.md).

## Where data lives

| Data | Location | Protection |
| --- | --- | --- |
| Transactions, containers, labels, goals, audit trail | SQLite on the device | SQLCipher, key in Android Keystore |
| Encryption keys | Android Keystore | Never in app storage; never in a managed `string` |
| Backups | The user's own Google Drive, `drive.appdata` | AES-256-GCM, encrypted before upload, key derived by Argon2id from the user's passphrase. **Not built yet** |
| Local backup file | Wherever the user saves it, through the share sheet | **None. Plaintext JSON.** See below |
| CSV export | Wherever the user saves it, through the share sheet | **None. Plaintext.** See below |
| SMS message bodies | **Nowhere** | Never written to disk in any form |

## The one automatic egress path

Nothing leaves the device unless the user asks for it, and there is exactly one path the
app will ever take on its own: an encrypted backup to the user's own Google Drive. Google
receives ciphertext and holds no key. That path is not built yet.

There is no server, no analytics, no telemetry, and no crash reporting that carries user
data. **Any change that creates a second automatic egress path requires an ADR** (NFR-5.1),
which is the strongest process rule in the project.

## Files the user asks for are not encrypted

Two features hand a plaintext file to the OS share sheet: the CSV export, and the local JSON
backup that stands in until Drive backup exists. Both are deliberate, and both are the
user's own copy of their own data, which is the entire point of them: a money app that
encrypts the exit is holding its user hostage.

But they are worth being blunt about, because the file is a complete financial history in
readable text:

- Whatever the user picks in the share sheet decides where it goes. If that is a cloud
  drive, it is in that cloud drive unencrypted.
- On Android the file passes through the app's `cache/` directory, which is app-private, and
  is not deleted afterwards; it goes when the OS clears the cache or the app is uninstalled.
- The app says "this file is not encrypted" at the point of export rather than in a settings
  page nobody opens.

Encrypting the local backup was considered and rejected for now: it needs the passphrase and
recovery-code machinery that S12 brings, and shipping a half version of that would teach
users a passphrase habit the real feature then changes.

## Google Drive scope

Only `drive.appdata` is requested. It is limited to a private folder the app creates, not
visible in the user's Drive UI and not readable by other apps. Broader Drive scopes
(`drive`, `drive.readonly`, `drive.metadata`) are restricted by Google and are forbidden
here regardless.

Sign-in exists solely to enable backup. There is no EleFi account.

## SMS handling

On Android, EleFi can read incoming SMS to offer transactions back as suggestions. The
rules are absolute:

- Only messages whose **sender** matches an enabled Parse Rule are read past the receiver.
  Everything else is discarded without its body being examined.
- **OTP and verification messages are never parsed, stored, or surfaced**, including from a
  matching sender. A shared corpus of real OTP formats is tested against every rule.
- **A message body is never persisted, logged, added to the audit trail, backed up, or
  exported.** It is a `ref struct` that cannot be assigned to a field, boxed, captured, or
  serialised, so this is a compiler guarantee rather than a convention.
- No message or anything derived from one is transmitted anywhere. SMS access adds no
  egress path.
- The permission is optional, independently revocable, and there is an in-app switch that
  disables reading without a trip to system settings.
- A parsed message never becomes a transaction without explicit user confirmation.

See [ADR-0010](docs/adr/0010-on-device-sms-assisted-capture.md).

## Account numbers

Only the **last 4 digits** of an account or card number are ever stored. No code path
accepts a full number. It cannot help the user and it is the single most damaging field to
leak.

## Device access

Biometric or device-credential lock on cold start and on resume after a configurable
delay. Balances and amounts are hidden from the OS app-switcher preview.

## What this does not protect against

Stated plainly, because a security document that only lists strengths is not useful:

- **A compromised device.** Malware with root, or an attacker with an unlocked phone past
  the biometric lock, can read the database through the running app. Local-first means the
  device is the security boundary.
- **A lost passphrase.** If the backup passphrase and the recovery code are both lost, the
  backup is unrecoverable. There is no reset and no support path, by design - the
  alternative is a key we hold, which is a different product.
- **A wrong parse.** SMS parsing can misread a message. This is why it produces a
  suggestion rather than a transaction.

## Reporting a vulnerability

This is a personal project with no public release yet. Open a GitHub issue for anything
non-sensitive.

For something exploitable, please use GitHub's private vulnerability reporting on this
repository rather than a public issue, and allow a reasonable window before disclosing.
