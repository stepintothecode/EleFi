# EleFi - Non-Functional Requirements

Qualities the system must have, independent of any particular feature. Every requirement
here states a **number or a rule that can be checked**, because an unmeasurable NFR is a
preference wearing a suit.

---

## NFR-1 · Performance

The governing principle: **capture must feel instantaneous, or the habit dies.** Every
budget below serves that.

| ID | Requirement | Target | How verified |
|---|---|---|---|
| NFR-1.1 | Cold start to interactive | ≤ 2.5 s (mid-range Android, e.g. Snapdragon 6-series) | Startup trace, release build |
| NFR-1.2 | Warm start to interactive | ≤ 700 ms | Startup trace |
| NFR-1.3 | Capture form open → keypad focused | ≤ 300 ms | Manual trace + instrumentation |
| NFR-1.4 | Save transaction → UI confirms | ≤ 100 ms (write is local; optimistic UI) | Instrumentation |
| NFR-1.5 | Autocomplete suggestions render | ≤ 50 ms per keystroke | Benchmark, 5,000 parties |
| NFR-1.6 | Dashboard aggregates | ≤ 400 ms at 50,000 transactions | Benchmark with seeded data |
| NFR-1.7 | Filtered list first page | ≤ 200 ms at 50,000 transactions | Benchmark |
| NFR-1.8 | List scroll | 60 fps, no dropped frames over 5 s of flinging | Perf monitor on release build |
| NFR-1.9 | CSV export, 50,000 rows | ≤ 10 s, UI never blocked | Benchmark |
| NFR-1.10 | Widget/notification capture end-to-end | ≤ 3 s from tap to saved | Manual, stopwatch |
| NFR-1.11 | SMS received → Suggestion Prompt posted | ≤ 2 s | Instrumented on a real device |
| NFR-1.12 | Suggestion Prompt tapped → pre-filled capture form | ≤ 800 ms from cold | Manual trace |
| NFR-1.13 | Export button reflects a filter change | ≤ 100 ms (the count is the list's own count) | Instrumentation |

**Seeded performance dataset:** 50,000 transactions, 40 containers, 5,000 parties, 200
labels, 300 apps, spanning 10 years. Every benchmark runs against it. Chasing performance
on 200 rows tells you nothing about year eight.

**NFR-1.1 was 2.0 s and is now 2.5 s.** The .NET MAUI runtime plus, on the Blazor Hybrid
option, a WebView init costs more at startup than a React Native bundle did. Raising the
number is the honest response; quietly missing the old one is not. It is still a hard
budget: [ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md) makes the first slice
measure real cold start on the reference device, and a failure there reopens the UI-layer
decision rather than the budget.

---

## NFR-2 · Animation and interaction quality

The brief asks for Duolingo-grade motion. That is a real engineering constraint, not a
mood, and it decomposes into rules.

**Rebaselined 2026-08-29.** The original NFR-2 was written around React Native Reanimated,
whose UI-thread worklets were the stated reason the project chose Expo over C#. Moving to
.NET MAUI ([ADR-0009](../adr/0009-dotnet-maui-over-expo-typescript.md)) gives that up. The
requirements below are what the new stack can actually be held to. The **feel** rules
(2.5-2.10) are unchanged and non-negotiable; the **mechanism** rules (2.1-2.4) are
restated in terms of what MAUI provides.

This is the largest cost of the stack change, and it is recorded here rather than
discovered later.

| ID | Requirement |
|---|---|
| NFR-2.1 | Animation runs off the .NET UI thread's per-frame work: CSS/Web Animations compositor properties under Blazor Hybrid, or `SKCanvasView`-driven interpolation under native MAUI. Never a per-frame C# callback that touches the visual tree |
| NFR-2.2 | Animate `transform` and `opacity` only. No animation of layout properties on any hot path. This rule survives the stack change unchanged and matters more now, not less |
| NFR-2.3 | Motion is **spring-based**, not duration-based, for anything the user has directly manipulated. Springs come from a shared motion module, since neither MAUI nor CSS provides real spring physics out of the box |
| NFR-2.3a | That motion module is the **only** place easing and spring constants are defined. Ad-hoc durations scattered through markup are how an app stops feeling like one app |
| NFR-2.4 | 60 fps sustained on the mid-range reference device during capture, list scroll, and sheet transitions. 120 fps where the display allows is a goal, not a budget |
| NFR-2.4a | Gesture-driven motion tracks the finger without a frame of lag. Where the stack cannot deliver this for a given interaction, that interaction is redesigned to not be gesture-driven rather than shipped feeling loose |
| NFR-2.5 | Every state change is animated - entry, exit, reorder, value change. Nothing appears or disappears abruptly |
| NFR-2.6 | Haptic feedback on save, on error, and on threshold crossings |
| NFR-2.7 | The mascot reacts to events (save, goal reached, streak) but is never on the critical path of an interaction |
| NFR-2.8 | Full respect for `prefers-reduced-motion` / "Remove animations": transitions become instant, nothing breaks |
| NFR-2.9 | No animation delays input. A tap during an animation is honoured immediately |
| NFR-2.10 | Skeleton loaders, never spinners, for anything that can take over 200 ms |

**NFR-2.9 deserves emphasis.** The single most common way "polished" apps become
irritating is animations that must finish before input is accepted. Motion decorates; it
never gates.

**NFR-2.4a is the escape valve.** Rather than promising physics the stack may not deliver
on a mid-range device and then shipping something that feels almost right, the rule is to
cut the interaction back to something that feels deliberate. A crisp transition beats a
laggy spring, and "we tried to do Reanimated in a WebView" is the most likely way this app
ends up feeling second-rate.

---

## NFR-3 · Data correctness

The highest-priority category. A money app that is fast, beautiful, and slightly wrong is
worthless.

| ID | Requirement |
|---|---|
| NFR-3.1 | No floating-point number ever holds, transports, or computes money. Integer minor units only |
| NFR-3.2 | Every balance is derived. No stored balance exists anywhere, at any layer, including caches |
| NFR-3.3 | Every domain invariant in [domain-model.md](domain-model.md) is enforced by a database constraint **and** covered by a property-based test |
| NFR-3.4 | Multi-step writes are wrapped in a SQLite transaction. No partial write survives a crash |
| NFR-3.5 | Deletes are soft. No user-entered data is ever destroyed by normal operation |
| NFR-3.6 | Self Transfers are excluded from every spend and income aggregate, verified by explicit test |
| NFR-3.7 | Credit card bill payments leave net worth unchanged, verified by explicit test |
| NFR-3.8 | Rounding uses banker's rounding, applied once at display, never during accumulation |
| NFR-3.9 | Sum of per-label spend equals total spend for the same filter, to the paisa, verified by property test |
| NFR-3.10 | A migration that could lose data is refused; migrations are forward-only and tested against real backups |
| NFR-3.11 | An export contains exactly the rows the list shows for the same filter, in the same order (`X1`), verified by property test |
| NFR-3.12 | No Capture Suggestion ever reaches a balance, an aggregate, a report, an export, or a backup (`SM2`), verified by explicit test |

---

## NFR-4 · Offline and reliability

| ID | Requirement |
|---|---|
| NFR-4.1 | 100% of functionality except Drive backup and FX fetch works with no network, indefinitely |
| NFR-4.2 | The app never shows a network error during capture, because capture never touches the network |
| NFR-4.3 | Process death mid-capture loses at most the in-flight form, never committed data |
| NFR-4.4 | Backup failure is silent-retry, surfaced only after 3 consecutive daily failures |
| NFR-4.5 | FX rate fetch failure falls back to the last cached rate and is never user-blocking |
| NFR-4.6 | Corrupt local database is detected on start and offers restore-from-backup rather than crash-looping |
| NFR-4.7 | Crash-free session rate ≥ 99.5% once public |

---

## NFR-5 · Security and privacy

| ID | Requirement |
|---|---|
| NFR-5.1 | User financial data leaves the device **only** as an encrypted backup to the user's own Drive. There is no other egress path |
| NFR-5.2 | Backups encrypted with AES-256-GCM; key derived by Argon2id from a user passphrase |
| NFR-5.3 | Encryption keys stored in Android Keystore / iOS Keychain, never in app storage. In managed memory they live in pinned buffers zeroed after use, never in a `string` |
| NFR-5.4 | Only the last 4 digits of an account number are ever stored. No code path accepts a full number |
| NFR-5.5 | Only the `drive.appdata` OAuth scope is requested. Broader Drive scopes are prohibited |
| NFR-5.6 | Biometric or device-credential lock, enforced on cold start and on resume after the configured delay |
| NFR-5.7 | Balances and amounts are hidden from the OS app-switcher preview |
| NFR-5.8 | No analytics, telemetry, or crash reporting transmits transaction data, amounts, party names, or container names |
| NFR-5.9 | Logs never contain amounts, party names, container names, account digits, or any part of an SMS body - enforced by an analyzer and a log-scrubbing layer |
| NFR-5.10 | The local database is encrypted at rest (SQLCipher or platform equivalent) |
| NFR-5.11 | No third-party SDK with network access is added without an explicit review recorded in an ADR |
| NFR-5.12 | Dependencies audited on every CI run; no known-critical vulnerabilities in a release build |
| NFR-5.13 | **An SMS body is never written to disk, to a log, to the audit trail, to a backup, or to an export.** It exists only as a local in the receiver (`SM1`) |
| NFR-5.14 | Only messages from a sender matching an enabled Parse Rule are read past the receiver boundary. Everything else is dropped without its body being examined (`SM3`) |
| NFR-5.15 | No OTP or verification message is ever parsed, stored, or surfaced (`SM4`), covered by tests against real OTP formats |
| NFR-5.16 | The SMS permission is optional and independently revocable, and an in-app switch disables reading without touching system settings (FR-11.20, FR-11.24) |
| NFR-5.17 | Parse Rules are bundled with the app; none is fetched over a network (FR-11.26). A remote rule channel requires an ADR |
| NFR-5.18 | SMS access adds **no** egress path. Nothing derived from a message is transmitted anywhere, which keeps NFR-5.1 exactly as strong as it was |

**NFR-5.1 is the product's central promise.** Any change that creates a new egress path
for user data requires an ADR, not a pull request comment.

**NFR-5.13 to NFR-5.18 exist because SMS access is the most invasive permission this app
will ever ask for.** Granting it hands the app every message on the device, including
one-time passcodes. The mitigation is not care; it is that there is no code path capable
of retaining a message. A body is matched, a suggestion is built from named captures, and
the string goes out of scope. Auditing this is a small, finite job precisely because the
rule is absolute - one exception and it becomes unauditable.

---

## NFR-6 · Usability and accessibility

| ID | Requirement |
|---|---|
| NFR-6.1 | Every interactive target ≥ 44×44 dp |
| NFR-6.2 | Text contrast meets WCAG 2.2 AA (4.5:1 body, 3:1 large) in both themes |
| NFR-6.3 | Colour is never the sole carrier of meaning - credit/debit, on-track/behind also differ by icon or label |
| NFR-6.4 | Full screen-reader support (TalkBack/VoiceOver): every control labelled, amounts read as money not digit strings |
| NFR-6.5 | Layout survives system font scaling to 200% without clipping or overlap |
| NFR-6.6 | Primary capture actions reachable one-handed in the lower half of the screen |
| NFR-6.7 | Destructive actions confirm or offer undo; deletes always offer undo |
| NFR-6.8 | Error messages state what happened and what to do - never a code, never "something went wrong" |
| NFR-6.9 | Empty states explain the feature and offer the first action |
| NFR-6.10 | Numbers formatted per locale, with Indian lakh/crore grouping under `en-IN` |

---

## NFR-7 · Portability and data ownership

| ID | Requirement |
|---|---|
| NFR-7.1 | Full JSON export of every entity is always available and always free |
| NFR-7.2 | The export format is documented well enough for a third party to write an importer |
| NFR-7.3 | The local database is a standard SQLite file; nothing proprietary blocks a technical user reading their own data |
| NFR-7.4 | Uninstalling removes local data; the Drive backup remains until the user deletes it or revokes access |
| NFR-7.5 | No feature is gated behind a network service that could disappear |

---

## NFR-8 · Maintainability

| ID | Requirement |
|---|---|
| NFR-8.1 | Domain logic is a pure C# class library with no MAUI, no EF Core, no `SQLitePCLRaw`, and no `Android.*` reference - testable on a desktop runner without a device |
| NFR-8.2 | The database is reachable only through the repository layer. No component or hook issues SQL |
| NFR-8.3 | `user_id` scoping and `deleted_at IS NULL` are applied in exactly one place, not per query |
| NFR-8.4 | All money formatting and parsing lives in one module |
| NFR-8.5 | Nullable reference types enabled solution-wide, warnings as errors, latest analysis level. No `dynamic`, and no `#pragma warning disable` without a comment naming the reason |
| NFR-8.6 | Every table's schema is defined once, in the EF Core model, and is the source of truth. Migrations are generated from it, never hand-written to diverge from it |
| NFR-8.7 | Domain-logic test coverage ≥ 90%; overall ≥ 70% |
| NFR-8.10 | Every external dependency - database, Drive, crypto, FX, SMS, notifications, clock - sits behind an interface owned by the application layer, so no test needs a device or a network |
| NFR-8.11 | Parse Rules are data, not code. Supporting a new bank is a data change plus a test, never a new class |
| NFR-8.8 | Public modules carry a doc comment stating purpose and invariants |
| NFR-8.9 | New vocabulary is added to [CONTEXT.md](../CONTEXT.md) in the same change that introduces it |

---

## NFR-9 · Storage and growth

| ID | Requirement |
|---|---|
| NFR-9.1 | 10 years of heavy use (~40,000 transactions) stays under 100 MB including audit events |
| NFR-9.2 | An encrypted backup of that dataset stays under 20 MB (Drive's free app-data quota is not a constraint at this size) |
| NFR-9.3 | Audit events are compactable after 24 months without losing create/delete records |
| NFR-9.4 | The app degrades gracefully at 10× the design dataset - slower, never broken |

---

## NFR-10 · Compatibility

| ID | Requirement |
|---|---|
| NFR-10.1 | Android 9 (API 28) minimum; target the current API level Play requires |
| NFR-10.2 | Reference device for all performance targets is mid-range, not flagship |
| NFR-10.3 | Correct behaviour on OEM Android skins (Xiaomi MIUI/HyperOS, Oppo ColorOS, Vivo Funtouch, Samsung One UI) - specifically their aggressive background-process management |
| NFR-10.4 | iOS 16+ when the iOS target arrives. **SMS-assisted capture will never exist there** - iOS gives no app access to message content. The paste and share-sheet fallbacks (FR-11.22, FR-11.23) carry the whole feature on iOS |
| NFR-10.5 | Web: last two versions of Chrome, Edge, Firefox, and Safari when the web target arrives |
| NFR-10.6 | Handles device timezone changes and travel across timezones without shifting `occurred_on` dates |
| NFR-10.7 | SMS-assisted capture is **Android only** and is absent, not broken, everywhere else. No screen, setting, or empty state referring to it appears on a platform that cannot do it |
| NFR-10.8 | The SMS receiver survives the OEM background-management behaviour in NFR-10.3, or fails visibly. A silently dead receiver is worse than an absent feature, because the user stops recording things believing the app is watching |

**NFR-10.3 is not boilerplate.** Those skins dominate the Indian market and they kill
background services aggressively. Any feature relying on a persistent service - the
floating bubble and the SMS receiver above all - must be tested on them specifically, and
must fail visibly rather than silently.

---

## NFR-11 · Legal and compliance *(applies from public release)*

| ID | Requirement |
|---|---|
| NFR-11.1 | Privacy policy at a public URL, accurately describing local-first storage and Drive backup |
| NFR-11.2 | Google Play Data Safety declaration matching actual behaviour, including that SMS content is read on-device, not collected, and not transmitted |
| NFR-11.2a | **If EleFi is ever distributed through Google Play**, a Permissions Declaration for `RECEIVE_SMS` is filed and **approved** before that build ships SMS reading; until then the Play build has the feature compiled out and the fallbacks (FR-11.22-11.23) stand in its place. Sideloaded and F-Droid-family builds are unaffected |
| NFR-11.2b | A privacy-policy section covering SMS specifically: what is read, what is kept, what is never kept, and how to turn it off |
| NFR-11.3 | Compliance with Google API Services User Data Policy for `drive.appdata` |
| NFR-11.4 | India DPDP Act 2023 obligations reviewed before public launch |
| NFR-11.5 | Free full data export satisfies portability expectations, including GDPR Art. 20 if EU users are served |
| ~~NFR-11.6~~ | ~~GST registration and invoicing for digital sales in India, before charging~~ **Inapplicable** while [ADR-0012](../adr/0012-free-forever-voluntary-support.md) stands: nothing is ever sold. Kept, not deleted, so that reversing that ADR visibly makes this live again |
| NFR-11.7 | No financial advice is given anywhere in the product. EleFi records and reports; it never recommends |
| NFR-11.8 | SMS parsing is never described to users, stores, or reviewers as bank integration. It reads a text message the bank already sent to the device, and it can be wrong |
| NFR-11.9 | Voluntary support gives nothing in return - no tier, perk, badge, or unlock - so it stays a gift rather than a sale (ADR-0012) |
| NFR-11.10 | Outbound links open in the system browser, never the app's own WebView (FR-12.4) |

---

## Explicitly not required

Stated so their absence is a decision on record:

- **High availability / uptime** - there is no server to be up.
- **Horizontal scalability** - one user, one device.
- **Real-time collaboration** - single-user by design (Q1).
- **Sub-second sync latency** - v1 backs up daily; it does not sync.
- **Regulatory financial certification** - EleFi is a personal record-keeping tool, not
  a regulated financial service, and must never present itself as one.
- **Bank or account-aggregator integration** - nothing connects to a financial
  institution. SMS parsing reads a message already delivered to the phone, which is a
  different thing and is described as such (NFR-11.8).
- **Complete SMS coverage** - no promise that every bank, every format, or every message
  is recognised. A missed alert costs one manual entry, which is the baseline anyway.
