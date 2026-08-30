# JavaScript interop

Blazor interop modules live here, and nowhere else.

Rules, from [ADR-0011](../../../../docs/adr/0011-blazor-hybrid-over-native-xaml.md):

- No Razor component calls `IJSRuntime` directly. Every module is reached through an
  interface declared in `EleFi.Application/Abstractions` and implemented in C#.
- Every interop implementation is `IAsyncDisposable`. A `BlazorWebView` leaks
  `DotNetObjectReference` and module handles readily.
- Nothing here handles money, and nothing here makes a domain decision. This layer draws
  and animates.

Tests for these modules go in `tests/EleFi.App.Tests/js/`, following the same `src` and
`tests` split as the C#.

First expected module: `elefi-mascot.js`, driving the Rive state machine behind
`IMascotService`.
