---
status: accepted
---

# Local-first: SQLite on device is the source of truth, with no backend

EleFund stores all user data in SQLite **on the device**, with no server, no hosted
database, and no API. Google Drive holds an encrypted backup, and that is the only place
user data goes. We chose this because capture must never fail for want of a network,
financial data is safest where it is never transmitted, and a single-user app carries no
running cost worth paying.

## Considered options

- **Server-first (ASP.NET Core + PostgreSQL), device as cache** — the design this
  project started with. Rejected once the requirement became "database is local". It put
  a network round-trip on the critical path of the one feature that matters most.
- **Local-first with a thin service** for purchase validation and FX proxying. Rejected
  as premature: FX comes from a free keyless API the device can call directly, and there
  is nothing to validate until the app is public.

## Consequences

- **This is the load-bearing decision of the project.** Reversing it means rebuilding
  persistence, sync, and auth. Any proposal that needs a server must supersede this ADR.
- Everything works offline, permanently — not as a feature, but as a property.
- Multi-device is *not* free. It becomes an explicit sync problem
  ([ADR-0006](0006-google-drive-encrypted-backup.md)) rather than something the server
  handles implicitly.
- There is no server-side analytics, no remote debugging, and no ability to fix a user's
  data. Correctness has to be enforced on the device or not at all — hence the emphasis
  on database-level invariants and property tests.
- Hosting cost is zero, and stays zero at any number of users.
