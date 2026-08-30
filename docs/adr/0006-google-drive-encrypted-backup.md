---
status: accepted
---

# Backup to the user's Google Drive, client-side encrypted; sync deferred to v3

v1 uploads encrypted snapshots of the local database to the user's own Google Drive using
only the `drive.appdata` scope. It is **backup, not sync** - restoring on a second device
replaces that device's data. We chose this because losing a phone must not lose the
ledger, while true multi-device merge is ~2-3 weeks of work that complicates every
feature built afterwards, for a benefit a single user may never need.

Encryption is on by default (AES-256-GCM, key derived by Argon2id from a user passphrase,
cached in the OS keystore), because choosing local-first for privacy is undone if the
backup is readable by the storage provider.

## Considered options

- **True multi-device sync from v1** via per-device change logs merged by
  `(uuid, updated_at)`. The right end state, but it delays first real use substantially.
- **Backup only, permanently.** Rejected: it would make the future web build read-mostly.
- **Unencrypted backup.** Rejected: simpler recovery, but it puts a complete financial
  history in readable form behind nothing but a Google account.

## Consequences

- **Verified:** `drive.appdata` and `drive.file` are **non-sensitive** OAuth scopes
  requiring only basic verification - no security assessment, no annual fee. Broader
  Drive scopes (`drive`, `drive.readonly`, `drive.metadata`) are restricted and must
  never be requested.
- Backups are invisible in the user's Drive UI, so they can't be deleted by accident, and
  they are removed if the app is uninstalled.
- **A lost passphrase and recovery code means an unrecoverable backup.** No reset exists.
  This must be stated bluntly in the UI at setup, not buried.
- v2's sync is additive rather than a migration, because the schema already carries
  client-generated UUIDs, `updated_at`, and `deleted_at` tombstones on every table. Those
  three columns are the entire reason sync stays cheap later.
- Using a second device before sync ships risks silent data loss on restore, so restore
  must always warn explicitly.
