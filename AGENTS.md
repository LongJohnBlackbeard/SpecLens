# AGENTS.md

## Purpose
This file gives coding agents a short, reliable set of repo-specific rules and workflows.
Prefer incremental, scoped changes over refactors. Match existing patterns and naming.

---

## Non-negotiables (read first)
- **Keep changes small and targeted.** No “cleanup-only” edits unless requested.
- **Don’t change SDK pinning** (`global.json`) unless explicitly requested.
- **Don’t upgrade NuGet packages opportunistically.** Upgrade only when required by the task.
- **Respect architecture boundaries.** No new cross-layer references without strong justification.
- **No secrets/PII in code, logs, or commits.** Treat logs as sensitive.
- **Run verification steps** (build + tests) before considering changes complete.

---

## Repo layout
- `SpecLens.Avalonia/` — UI app (Avalonia).
- `JdeClient.Core/` — client library.
- `ViewportGrid.Core/`, `ViewportGrid.Data/` — shared grid components.
- `.github/workflows/release.yml` — release pipeline.
- `docs/LOGGING.md` — logging guide.
- `TESTING_BEST_PRACTICES.md` — authoritative testing rules.
- `TESTING_EXAMPLES.md` — examples for test setup patterns.
- `JdeClient.Core.UnitTests/` — unit tests for client library.
- `JdeClient.Core.IntegrationTests/` — integration tests for client library.
- `JdeClient.TestConsole/` — test console app for client library.
- `JdeClient.XmlEngineTestConsole/` — test console app for XML engine.
---

## Tooling & SDK
- Use the repo-pinned .NET SDK (see `global.json` if present).
- If a tool manifest exists (`.config/dotnet-tools.json`), restore tools first:
  - `dotnet tool restore`
- Follow repo formatting/analyzers:
  - Prefer existing `.editorconfig`/analyzers. **Do not introduce new formatting regimes** unless requested.
  - Avoid adding suppressions. Fix issues at the source when possible.
- NuGet:
  - Do **not** upgrade packages unless required by the change or explicitly requested.

---

## Architecture & dependencies (multi-project rules)
**Dependency direction must remain stable:**
- UI (`SpecLens.Avalonia`) depends on shared libs (`JdeClient.Core`, `ViewportGrid.*`).
- Core/Data libraries must **not** reference the UI project.

**General rules:**
- Avoid circular project references.
- Prefer interfaces/abstractions in the correct layer over adding cross-layer references.
- Keep shared components (`ViewportGrid.*`) framework-agnostic when possible (avoid UI-specific dependencies unless explicitly UI-layer).
- Public APIs in library projects should be **additive by default**. Avoid breaking changes unless requested; if unavoidable:
  - document in `CHANGELOG.md`
  - update any versioning expectations and dependent code

---

## Coding principles (beyond DRY + SRP)
Use these as defaults unless the task requires otherwise:
- **KISS:** simplest readable solution that meets requirements.
- **YAGNI:** don’t add abstractions/extensibility “just in case.”
- **DIP/ISP (SOLID):** depend on abstractions at boundaries; keep interfaces small and specific.
- **Composition over inheritance** (especially for reusable behaviors).
- **Separate pure logic from side effects** (UI/IO/time/system calls) to keep code testable.
- **Explicit > clever:** prefer clarity over terse tricks.

---

## C# & .NET implementation standards
- **Follow existing style** (naming, file layout, patterns).
- **Nullable reference types / warnings:**
  - Respect the project’s current nullable context.
  - Do not introduce new warnings; if analyzers treat warnings as errors, keep builds clean.
  - Avoid `#nullable disable` unless there is a strong, documented reason.
- **Public surface area:**
  - `internal` by default in libraries; make types/members `public` only when needed.
  - For public APIs, prefer additive changes and document non-trivial behavior.
- **Async & cancellation (general .NET rule):**
  - No sync-over-async (`.Result`, `.Wait()`).
  - Use `async/await` end-to-end for async workflows.
  - Accept and pass `CancellationToken` where appropriate (usually last parameter).
- **Resource management:**
  - Dispose `IDisposable`/`IAsyncDisposable` correctly.
  - Avoid unnecessary buffering/large allocations; stream when appropriate.
- **Time/culture determinism:**
  - Prefer `DateTimeOffset` for timestamps and boundaries.
  - Use culture-safe parsing/formatting (`InvariantCulture`) unless UI/localization requires otherwise.
  - Avoid direct `DateTime.Now` in core logic; prefer an injectable time source when it affects behavior/tests.

---

## UI threading (Avalonia)
- Do not block the UI thread.
- Marshal to the UI thread only when updating UI state; keep IO/CPU work off the UI thread.
- Prefer existing dispatching patterns used in the repo.

---

## Error handling & exceptions
- Do not swallow exceptions.
- Catch exceptions only when you can:
  - add useful context, or
  - translate to a meaningful domain result at a boundary
- Avoid “log and rethrow” duplication. **Log once** at the appropriate boundary.

---

## Logging rules
- App logging (Serilog) and client logging are separate; both default to **disabled** in settings.
- App logs default: `%LOCALAPPDATA%\SpecLens\Logs\App\spec-lens-.log`
- Client logs default: `%LOCALAPPDATA%\SpecLens\Logs\JdeClient\jde-client-.log`
- Never log query data payloads; keep redaction/sanitization in place.
- Treat logs as potentially sensitive: do not log secrets, credentials, tokens, connection strings, or PII.

---

## Config & secrets
- Never commit secrets or real credentials.
  - Note: this application does not need secrets/credentials; connection is already set by a running FAT Client.
- Prefer environment variables / user secrets / CI secrets for sensitive configuration.
- Any new config keys must have:
  - a safe default
  - documentation update where appropriate (README or relevant `docs/*`)

---

## Admin elevation
- The Avalonia app auto-relaunches with UAC prompt when not elevated (see `SpecLens.Avalonia/Program.cs`).

---

## Build & run (PowerShell)
Restore/build solution (Release):
- `dotnet build SpecLens.sln -c Release`

Build solution (Debug):
- `dotnet build SpecLens.sln -c Debug`

Run UI app:
- `dotnet run --project SpecLens.Avalonia/SpecLens.Avalonia.csproj -c Debug`

### Publish (Avalonia, single-file, win-x64)
- `dotnet publish SpecLens.Avalonia/SpecLens.Avalonia.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/SpecLens-win-x64`

---

## Testing guidelines
- `TESTING_BEST_PRACTICES.md` is authoritative and must be followed for any testing, verification, or changes that impact tests.
- `TESTING_EXAMPLES.md` provides examples of various test setups and should be referenced when adding/updating tests.
- Use **TUnit** as the test framework.
- Use **NSubstitute** for mocking; do not use Moq.
- Coverage must be **greater than 60%** (do not lower thresholds).

**Test intent rules:**
- Unit tests for pure logic and edge cases.
- Integration tests only where behavior depends on IO/process boundaries; keep them deterministic and isolated.
- Integration tests can only be run locally with a running FAT Client.

---

## Verification checklist (required for code changes)
Run these locally before considering a change complete:
1) Build:
  - `dotnet build SpecLens.sln -c Release`
2) Tests:
  - `dotnet test SpecLens.sln -c Release`
3) If tooling is configured:
  - `dotnet tool restore` (if tool manifest exists)
  - `dotnet format` / analyzers (only if already used by the repo; do not introduce new formatting regimes without request)
4) If touching release packaging or startup:
  - Run the UI once (`dotnet run ...`) and/or do a local `dotnet publish` sanity check.

---

## Release workflow
- Releases are tag-driven (`v*`).
- `CHANGELOG.md` **must** include a `## [x.y.z]` section matching the tag; the release job fails if missing.
- Release artifacts:
  - `SpecLens-win-x64.zip`
  - `SpecLens-win-x64-symbols.zip`
  - `SpecLens-win-x64.sha256`

---

## Editing guidance (how to change code here)
- Follow existing patterns and naming.
- Prefer targeted edits over refactors unless requested.
- Update docs when behavior changes (README or `docs/LOGGING.md`).
- Avoid introducing new libraries, architectural patterns, or cross-cutting abstractions unless explicitly requested.

**Pragmatic heuristics:**
- If you can solve it by extending an existing type, do that before adding a new abstraction.
- Keep PRs easy to review: small diffs, clear intent, minimal churn.

---
# Avalonia Specific
## 1) Architecture: keep UI thin, keep logic pure
- **MVVM with a real “core”**: put business rules in services/domain classes that don’t know Avalonia exists.
- **ViewModels are orchestration**: call services, manage state, expose commands/observables—avoid “business logic” inside them when possible.
- **One-way data flow by default**: UI events → VM → state → UI. Use two-way binding only when it genuinely reduces complexity.

## 2) Avalonia best practices (XAML, bindings, composition)
- **Use compiled bindings** (`x:DataType`) wherever possible for performance + compile-time safety.
- Prefer **DataTemplates** to map VM → View (less manual view instantiation).
- Use **styles/themes/resources** instead of hard-coded values:
    - keep spacing, typography, colors as resources (“design tokens”)
    - prefer `DynamicResource` for theme switching.
- Keep code-behind minimal:
    - view activation / subscription wiring is OK
    - avoid putting logic in events; route events into the VM.

**Compiled binding example**
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:MyApp.ViewModels"
             x:Class="MyApp.Views.CustomerView"
             x:DataType="vm:CustomerViewModel">

  <TextBox Text="{Binding Name}" />
  <Button Command="{Binding Save}" Content="Save" />
</UserControl>
```

## 3) ReactiveUI patterns that prevent “Rx spaghetti”
### Use `ReactiveCommand` for *all* user actions
- encapsulates async work + disabled states + error streams
- keeps event handlers out of views

```csharp
public sealed class CustomerViewModel : ReactiveObject
{
    private string? _name;
    public string? Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public ReactiveCommand<Unit, Unit> Save { get; }

    public CustomerViewModel(ICustomerService service)
    {
        var canSave = this.WhenAnyValue(x => x.Name)
            .Select(name => !string.IsNullOrWhiteSpace(name));

        Save = ReactiveCommand.CreateFromTask(
            execute: () => service.SaveAsync(Name!),
            canExecute: canSave
        );

        // Centralized error handling for the command
        Save.ThrownExceptions.Subscribe(ex => /* log + show dialog via Interaction */);
    }
}
```

### Use activation to avoid memory leaks
For views/viewmodels that subscribe to observables, use **`WhenActivated` + disposables**.

```csharp
this.WhenActivated(disposables =>
{
    ViewModel!.Save
        .IsExecuting
        .ObserveOn(RxApp.MainThreadScheduler)
        .Subscribe(isBusy => BusyIndicator.IsVisible = isBusy)
        .DisposeWith(disposables);
});
```

### Prefer a few “state streams” over many subscriptions
- Model your UI state as derived observables (e.g., `IsBusy`, `StatusText`, `HasErrors`)
- Use `ToProperty` / `ObservableAsPropertyHelper` for computed state.

## 4) Threading rules (desktop apps live or die here)
- Never block the UI thread (`.Result`, `.Wait()`, long loops).
- Always **`ObserveOn(RxApp.MainThreadScheduler)`** before touching UI-bound properties if work happens off-thread.
- For “typeahead/search” scenarios:
    - `Throttle` input
    - cancel stale requests via `Switch`
    - handle errors without killing the stream

```csharp
this.WhenAnyValue(x => x.Query)
    .Throttle(TimeSpan.FromMilliseconds(250))
    .DistinctUntilChanged()
    .Select(q => Observable.FromAsync(ct => service.SearchAsync(q, ct)))
    .Switch()
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(results => Results = results);
```

## 5) Dialogs, navigation, and platform services without coupling
- Use **ReactiveUI `Interaction<TInput, TOutput>`** for dialogs/notifications:
    - VM requests a dialog
    - View decides how to show it
- Keep platform-specific concerns behind interfaces:
    - file pickers, clipboard, windowing, notifications, OS paths

This avoids `ViewModel -> Window` references and keeps VMs testable.

## 6) Styling & design system practices (Avalonia-specific)
- Treat your theme as a **small design system**:
    - spacing scale (`4, 8, 12, 16…`)
    - typography styles
    - semantic colors (`Primary`, `Danger`, `Surface`, etc.)
- Prefer **ControlThemes** / reusable styles for consistent UI.
- Create custom controls only when composition cannot express the behavior cleanly.

## 7) Performance & responsiveness
- Use virtualization for large lists (Avalonia supports virtualizing panels).
- Avoid heavy value converters in hot paths; prefer computed VM properties.
- Keep images/icons optimized; prefer vector when possible.
- Defer expensive work until needed (lazy loading, incremental rendering).

## 8) Testing strategy that actually works
- **Unit test ViewModels** with `TestScheduler` (ReactiveUI/Rx).
- Mock services; verify:
    - command `CanExecute`
    - derived state changes
    - error flows (`ThrownExceptions`)
- Integration test only a thin slice of UI; most logic should already be covered.

## 9) Logging, errors, and “don’t crash the UX”
- Handle exceptions at 3 levels:
    1) inside services (wrap + enrich)
    2) `ReactiveCommand.ThrownExceptions`
    3) global exception handler (last resort)
- Make failures visible but non-fatal: status bar, toast, dialog, retry.

## 10) Composition root & DI (keep it boring)
- Configure DI once (startup), inject services into VMs.
- Avoid service locator inside application code (fine at the edges/bootstrapping, not inside core logic).
- Register view ↔ VM mappings cleanly (ReactiveUI view locator patterns).

---

## Change requirements (what to include with any code change)
Every change must include:
- **Impact:** what behavior changes and which project(s) are affected.
- **Security:** how data is handled; confirm no secrets/PII are logged or persisted unintentionally.
- **Performance:** any perf implications (allocations, async/blocking, IO frequency).
- **Cross-project analysis:** confirm whether other projects need updates (references, shared types, tests, docs).
- **Tests:** add/update unit and integration tests as needed, and run `dotnet test`.
