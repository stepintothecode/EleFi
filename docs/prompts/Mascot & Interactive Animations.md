ROLE

Act as an Expert .NET MAUI Blazor Hybrid Architect and Web Animation Specialist. You write modular, decoupled code adhering strictly to SOLID principles, dependency inversion, and clean front-end architecture.

GOAL

Scaffold and implement a complete, end-to-end first iteration of an interactive elephant mascot animation system for "EleFi" using an abstracted service pattern. The implementation must work out of the box with placeholder animation logic so that I can easily swap in final art assets and tweak state transitions later.

CONTEXT

App: EleFi (personal finance tracker).

Tech Stack: C# .NET MAUI Blazor Hybrid targeting Android.

Mascot Concept: An expressive elephant mascot that reacts dynamically to user events (e.g., celebrating when a transaction is logged, showing concern if spending is high, idle breathing on the home dashboard).

Architecture Requirement: The UI must never talk directly to JavaScript or tightly couple to a specific animation runtime. All animation triggers must flow through an interface (IMascotService). The underlying engine should target Rive (with fallback/scaffolding for standard web canvas or Lottie), keeping all JS interop isolated inside wwwroot.

ACTION

Provide the implementation in the following order:

Contract Definition (Core/Domain Layer):

Create IMascotService defining states and triggers (e.g., InitializeAsync(), SetMoodAsync(MascotMood mood), TriggerCheerAsync(), TriggerConcernAsync()). Define any relevant enums or DTOs.

JavaScript Interop Bridge (wwwroot):

Write a clean, self-contained JavaScript module (elefi-mascot.js) to handle canvas initialization, state machine inputs, and animation playback. Include fallback visual feedback (such as an animated SVG/Canvas elephant placeholder) so it runs immediately before custom .riv or .json assets are added.

Service Implementation (Infrastructure Layer):

Write the concrete C# service implementing IMascotService using IJSRuntime. Implement IAsyncDisposable to prevent memory leaks in the web view.

Reusable Blazor Component (UI Layer):

Build a <MascotViewer/> Blazor component that handles canvas mounting, lifecycle hooks (OnAfterRenderAsync), and sizing.

Usage Example:

Demonstrate how a page (such as AddTransaction.razor) injects IMascotService and triggers visual reactions based on user input.