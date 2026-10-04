---
status: accepted
---

# Audit trail written by SQLite triggers, with soft deletes everywhere

Every create, update, delete, restore, and review is recorded in an append-only
`AuditEvent` table written by **database triggers**, not application code. Deletes are
soft (`deleted_at`) across every table. We chose triggers because no code path can bypass
them - including future bulk imports, restores, and native widget code - whereas an
application-layer audit is only as complete as the discipline of whoever writes the next
repository method.

## Considered options

- **EF Core `SaveChanges` interceptor.** This was the plan while the app was going to be
  C#, and it was the cheapest option available. It became void when
  [ADR-0005](0005-expo-typescript-over-csharp.md) chose TypeScript - and then available
  again when [ADR-0009](0009-dotnet-maui-over-expo-typescript.md) reversed that.

  **It is still rejected, now on its merits rather than by circumstance.** An interceptor
  sees only writes that pass through the `DbContext`, and this app has code paths that will
  not: a restore that swaps the database file, a seeded performance dataset, a future bulk
  import, and anything written from the Android SMS or widget services outside the normal
  scope. A trail with holes in exactly those places is worse than no trail, because it
  looks complete.
- **Application-layer audit in the repository.** Easier to write rich, user-meaningful
  events, but bypassable, and bypassed audit is worse than none because it looks complete.
- **Timestamps only, no history.** Rejected: a direct "no" to a stated requirement, and it
  makes deletion irreversible.

## Consequences

- The trail is complete by construction, which is the property that makes it worth
  trusting at all.
- Triggers must be maintained alongside every schema migration - a real ongoing cost, and
  the main argument against this approach.
- **Triggers are created in exactly one place: `DatabaseInitialiser`, after migrating.** They
  are dropped and recreated on every startup, so their text cannot drift from the schema the
  way it does when it is pinned inside the migration that first created a table. A migration
  may drop triggers by name; it must never call `AuditTriggers`, because that runs today's
  definition against an older schema. This was learned the expensive way: when the trigger
  set grew a pair on the `TransactionLabels` join table, the first migration started failing
  on a fresh database with "no such table", three migrations before that table exists.
- **A relationship in a join table needs its own triggers.** A trigger on `Transactions`
  cannot see rows written to `TransactionLabels` in the same save, and the `Updated` trigger
  will not even fire, because it requires a tracked column on `Transactions` to differ. Its
  triggers write against the *transaction*, so the entry lands in the timeline being read,
  and they resolve the label's name at trigger time so the trail still makes sense after that
  label is renamed or deleted. The matching cost: any code that rewrites such a collection
  must apply a difference rather than clearing and re-adding, or every save writes "removed
  X, added X".
- Triggers see rows, not intent, so user-meaningful actions (Reviewed, Restored) are
  distinguished by the columns that changed, and `capture_source` is written onto the row
  so the trigger can record *how* a change was made.
- Soft delete means every query must exclude `deleted_at IS NOT NULL`. This is applied in
  **one** base query builder rather than remembered per query - a missed filter would leak
  deleted transactions into a balance.
- `deleted_at` doubles as the tombstone that makes v3 sync possible
  ([ADR-0006](0006-google-drive-encrypted-backup.md)).
- Audit events grow with use, but are bounded by human typing speed; compactable after 24
  months while retaining create and delete records.
