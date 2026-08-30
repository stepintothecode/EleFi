---
status: accepted
supersedes: 0005-expo-typescript-over-csharp.md
date: 2026-08-29
---

# C# on .NET MAUI, reversing the choice of Expo/TypeScript

EleFi is built in C# 14 on .NET 10, as a .NET MAUI application targeting Android first,
with EF Core over SQLCipher-encrypted SQLite. This reverses
[ADR-0005](0005-expo-typescript-over-csharp.md), which chose Expo and TypeScript nine
months into the project's planning and before any application code existed.

We chose this because **ADR-0005 optimised for the wrong risk.** It traded the developer's
daily fluency for animation quality, and it said so explicitly, naming abandonment as the
main risk it created. That risk is the one that actually matters here: this is a
single-developer project whose value depends entirely on being used daily, and a stack
that makes every evening's work slower is the most likely way it never gets used at all.
Animation quality is worth a great deal, but it is worth less than the app existing.

The second reason is that the requirement set has moved. SMS-assisted capture
([ADR-0010](0010-on-device-sms-assisted-capture.md)) is now a v1.2 feature, and it is
Android platform code end to end: a `BroadcastReceiver`, WorkManager, and
`NotificationCompat`. Under Expo that is Kotlin behind a hand-written native module - a
second language, a second toolchain, and a bridge, for the feature most likely to need
iteration as bank formats drift. Under MAUI it is C# calling the Android SDK directly, in
the same project, debuggable in one step.

## Considered options

- **Stay on Expo/TypeScript.** No migration cost, and it keeps the best animation story of
  any option; Reanimated's UI-thread worklets remain genuinely better than anything MAUI
  offers. Rejected on the abandonment risk above, and because every native surface on the
  roadmap - SMS, widget, tile, bubble - is Kotlin work that Expo makes indirect.
- **.NET MAUI with native XAML.** Best startup time of the C# options and no WebView, so
  the least risk against NFR-1.1. Rejected as the default because XAML is a poor substrate
  for a bespoke, heavily-animated design system, and because it forfeits the v2 web target
  entirely. **Not rejected outright** - see the open sub-decision below.
- **.NET MAUI Blazor Hybrid.** Chosen, in [ADR-0011](0011-blazor-hybrid-over-native-xaml.md).
  HTML and CSS are the best tooling that
  exists for the kind of visual identity this product needs, the component work is reusable
  by a Blazor WebAssembly build for S26, and CSS compositor animation is respectable even
  if it is not Reanimated. Its cost is startup time and low-end WebView scroll performance.
- **Avalonia UI.** All C#, Skia-rendered, genuinely good animation, covers every target
  including the browser. Rejected for the same reason ADR-0005 rejected it: mobile tooling
  and ecosystem maturity, and a much smaller pool of answers when something Android-specific
  goes wrong at 11pm.
- **Kotlin Multiplatform / native Android.** Best possible Android result. Rejected: it is
  neither the developer's fluent language nor a path to the web target, so it loses on both
  counts that motivated this reversal.

## The sub-decision, now closed

This ADR originally left Blazor Hybrid versus native XAML open, to be settled by a
cold-start measurement. [ADR-0011](0011-blazor-hybrid-over-native-xaml.md) closed it in
favour of Blazor Hybrid on design-substrate and web-target grounds.

**The measurement in slice S1 still happens**, with its purpose changed: it no longer picks
the UI layer, it verifies NFR-1.1. A miss is answered by fixing the startup path, not by
accepting a slower number.

## Consequences

- **The animation ceiling drops, and NFR-2 has been rebaselined to say so.** This is the
  real, paid cost of the reversal. The mechanism requirements (NFR-2.1-2.4) are restated in
  terms of what MAUI can deliver; the feel requirements (NFR-2.5-2.10) are unchanged and
  still binding. NFR-2.4a is new and is the honest escape valve: an interaction that cannot
  be made to feel right is redesigned rather than shipped feeling loose.
- **NFR-1.1 moves from 2.0 s to 2.5 s.** The .NET runtime and, under Blazor, a WebView cost
  more at startup than a JS bundle did. Raising the number openly is better than missing the
  old one quietly.
- **The audit trail stays in SQLite triggers.** EF Core's `SaveChanges` interceptor is
  available again and is not being used, because it would see only writes that go through
  the `DbContext` - not a file-swap restore, not a seeded dataset, and not future bulk
  import. [ADR-0008](0008-audit-via-sqlite-triggers.md) records the reasoning; the return of
  C# does not change it.
- **All native surfaces become first-party code.** SMS, notifications, widget, tile, and
  bubble are C# against the Android SDK in the app project. This is the single largest
  practical gain and it compounds across the whole v1.2 and v2.0 roadmap.
- **The web target changes character.** Under Expo it was React Native Web. Under Blazor
  Hybrid it is a Blazor WebAssembly build sharing the same Razor components, which is
  considerably less work - and under XAML it would not be available at all. This was a
  genuine input into ADR-0011.
- **`domain/` becomes `EleFi.Domain`, and the boundary gets stronger.** Layer separation is
  now enforced by project references and an architecture test rather than by a lint plugin.
  A forbidden dependency is a compiler error.
- **Nothing about the data model changes.** Integer minor units, derived balances, derived
  kind, soft delete, client-generated UUIDv7, and the audit trail are all storage-level
  decisions that survive the stack change untouched. ADR-0001 through ADR-0004 and ADR-0006
  through ADR-0008 all stand.
- **The reversal costs no code**, because none had been written. That is exactly why it is
  being made now, and it is the last moment at which it is nearly free.
