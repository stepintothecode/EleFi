---
status: accepted
---

# Expo/TypeScript, despite the developer being more fluent in C#

The whole app is TypeScript on Expo (React Native), even though the developer is
substantially more fluent in C# and an all-C# path existed (.NET MAUI Blazor Hybrid with
EF Core + SQLite, which would have covered every platform and every native surface). We
chose Expo because the brief treats Duolingo-grade animation as a primary requirement,
and Reanimated's UI-thread worklets are the difference between "smooth" and "actually
that good".

## Considered options

- **.NET MAUI Blazor Hybrid + Blazor WASM.** All C#, and it brings real advantages: EF
  Core migrations, LINQ, and a `SaveChanges` audit interceptor. Every Android quick-capture
  surface is reachable via .NET for Android bindings. Rejected on animation: a WebView
  renders CSS, so gesture-driven spring physics is materially harder and low-end Android
  feels a step behind.
- **.NET MAUI with native XAML.** Rejected: weakest animation story of the options, and no
  web target without rewriting the UI.
- **Avalonia UI.** All C#, Skia-rendered, covers every target including the browser.
  Rejected on ecosystem and mobile-tooling maturity.

## Consequences

- **This ADR exists because the decision is surprising.** A future reader who knows the
  developer's C# background will wonder why the app is in TypeScript. This is the answer:
  it was a deliberate trade of daily fluency for animation quality and ecosystem depth.
- **The main risk it creates is abandonment** — daily friction in a less-fluent language
  is the most likely cause of the project stalling. Mitigated by keeping `domain/` plain
  functions over plain data, which is the least framework-dependent code to learn in.
- The audit trail moves from an EF Core interceptor to **SQLite triggers**
  ([ADR-0008](0008-audit-via-sqlite-triggers.md)) — arguably stronger, since it cannot be
  bypassed by application code.
- Native quick-capture surfaces (widget, tile, bubble) require Kotlin behind Expo modules
  rather than being written in the app's own language.
