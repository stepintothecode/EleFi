---
status: accepted
date: 2026-08-29
---

# Blazor Hybrid rather than native XAML for the UI layer

EleFi's UI is Razor components hosted in a `BlazorWebView` inside .NET MAUI, styled with
CSS, with view models in `CommunityToolkit.Mvvm`. The alternative left open by
[ADR-0009](0009-dotnet-maui-over-expo-typescript.md) was native MAUI XAML. This ADR closes
that question.

We chose Blazor Hybrid because the product's visual identity is a stated primary
requirement, and HTML plus CSS is the best tooling that exists for building a bespoke,
heavily animated design system by hand. XAML's styling model is weaker for this, its
animation primitives are thinner, and the community component ecosystem pulls a project
toward looking like every other MAUI app. That is the opposite of what this one needs.

Three secondary reasons, each of which would be insufficient alone:

- **The web build (slice S26) becomes a port rather than a rewrite.** Blazor WebAssembly
  reuses the same Razor components behind the same repository seam. Under XAML there is no
  web target at all.
- **The mascot needs a real animation runtime.** Rive and Lottie both have mature,
  well-documented web players. The SkiaSharp equivalents are workable but considerably more
  code for a worse result.
- **CSS carries theming, font scaling, and reduced-motion for free.** NFR-6.5 (200% font
  scale) and NFR-2.8 (reduced motion) are `@media` queries rather than layout work.

## Considered options

- **Native MAUI XAML.** Faster cold start with no WebView to initialise, better raw scroll
  performance on low-end hardware, and a single rendering model to reason about. Rejected on
  design substrate and the loss of the web target. It stays the fallback if the cold-start
  measurement below fails badly enough.
- **Blazor Hybrid.** Chosen.
- **Hybrid of both**, XAML shell with Blazor for content-heavy screens. Rejected: two
  styling systems, two animation systems, and a seam down the middle of the design language.
  The worst of the trade rather than a balance of it.

## The measurement still happens

ADR-0009 gated this decision on a cold-start spike. The decision is now made on other
grounds, but **the spike in slice S1 is kept, with its purpose changed**: it no longer picks
the UI layer, it verifies NFR-1.1 (cold start under 2.5 s on the mid-range reference
device).

If it fails, the response is to fix the startup path, not to quietly accept a slower
number: trimming, startup-path AOT, deferring the `BlazorWebView` until after first paint,
and reducing what runs before the first render. Reopening this ADR is the last resort, not
the first.

Measuring before any screen exists is still the point. A startup problem found at week one
is a configuration change; the same problem at week nine is a rewrite.

## Consequences

- **JavaScript enters the codebase**, which is the real cost. It is confined to
  `src/EleFi.App/wwwroot/js/`, reached only through interop wrappers behind an interface,
  and never handles money or domain logic. The mascot runtime is the first and, for now,
  only case.
- **Every JS interop boundary needs an interface in `EleFi.Application/Abstractions`.**
  `IMascotService` is the pattern: the component talks to the interface, the implementation
  owns `IJSRuntime`, and the whole thing is swappable and testable without a WebView.
- **Interop objects must be disposed.** A `BlazorWebView` leaks `DotNetObjectReference` and
  JS module handles readily, so every interop service implements `IAsyncDisposable` and
  bUnit tests assert it.
- **External links must not open in the app's own WebView.** This is a Blazor Hybrid trap
  with real consequences: a plain `<a href>` navigates the host WebView and the user is
  stranded inside the app with no browser chrome. Every outbound link, the support link
  above all, goes through `Browser.OpenAsync(url, BrowserLaunchMode.External)`. See
  [ADR-0012](0012-free-forever-voluntary-support.md).
- **Cold start carries a WebView initialisation cost** that XAML would not, which is why
  NFR-1.1 sits at 2.5 s rather than 2.0 s.
- **CSS is the design system.** Tokens are custom properties, the motion module owns every
  spring and easing constant (NFR-2.3a), and animation stays on `transform` and `opacity`
  so it runs on the compositor (NFR-2.2).
- **Razor components are testable with bUnit**, which XAML has no real equivalent of. The
  filter bar and the export button's live count are worth testing, and now can be.
