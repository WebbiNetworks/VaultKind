# VaultKind Native Windows Shell

This document explains the native Windows prototype in plain language. No knowledge of C#, XAML, .NET, or WinUI is assumed.

## Purpose

The native shell proves that VaultKind can behave like a proper Windows desktop application while keeping the existing Java vault engine authoritative for security-sensitive work.

It currently starts the local Java vault engine, establishes a versioned private connection, displays configured vault summaries, and supports the complete everyday open/close lifecycle: unlock, open the readable Windows drive, and lock.

## Where it lives

The prototype is isolated under:

```text
native/VaultKind.Windows
```

The existing Java application remains in its original Maven project structure. Work on the native shell does not replace or modify the Java vault engine.

## The important files

| File | Plain-language purpose |
| --- | --- |
| `VaultKind.Windows.csproj` | The project recipe. It records the .NET version, Windows requirements, dependencies, build modes, and included assets. |
| `App.xaml` | Application-wide visual settings and resources. The prototype currently requests the dark theme here. |
| `App.xaml.cs` | Application startup code. It starts the local vault engine and creates the main window. |
| `MainWindow.xaml` | The genuine Windows window and title-bar layout. |
| `MainWindow.xaml.cs` | Native window behavior including its icon and restoring the previous size, position, and maximized state. |
| `MainPage.xaml` | The current VaultKind Dashboard and sidebar appearance. XAML describes what appears and how it is arranged. |
| `MainPage.xaml.cs` | Dashboard behavior, navigation, engine connection status, vault rendering, and native unlock/open/lock interactions. |
| `Services/JavaVaultEngineHost.cs` | Development lifecycle host that starts and stops the existing Java vault engine with the native app. |
| `Services/DoctorFindingPolicy.cs` | Defines the narrow evidence threshold for critical Vault Doctor warnings. |
| `Services/KeyboardNavigationPolicy.cs` | Defines testable sidebar Up/Down/Home/End movement and wrap boundaries. |
| `Services/SignatureSoundPolicy.cs` | Keeps warning audio limited to explicitly approved safety events. |
| `Services/WindowPlacementStore.cs` | Saves the last window placement locally and safely brings it back on-screen if the monitor layout later changes. |
| `../VaultKind.Windows.Tests` | Package-free native policy and preference regression checks. |
| `../../scripts/build-native-release.ps1` | Stages the self-contained frontend, bundled Java engine/runtime, notices, and optional signatures. |
| `../../src/main/java/org/cryptomator/launcher/NativeBackendComponent.java` | Constructs only the Java services reachable from the native backend entry point. It does not expose the inherited JavaFX application component. |
| `../../src/main/java/org/cryptomator/launcher/NativeBackendModule.java` | Supplies the small backend-specific root bindings that are still required while common vault services are separated from legacy GUI infrastructure. |

The complete implemented keyboard behavior, deliberate focus targets, and remaining manual release checks are documented in [KEYBOARD_CONTROLS.md](KEYBOARD_CONTROLS.md). That file is the source of truth for keyboard claims; do not infer a global Back or Escape command that the native shell does not implement.

## Two languages, two jobs

### XAML

XAML describes visible interface structure: grids, buttons, text, spacing, colors, and alignment. It serves a role similar to the FXML and CSS used by the JavaFX application, but it is native to Microsoft's Windows UI stack.

### C#

C# controls behavior: what happens when a button is clicked, how navigation works, and eventually how the frontend communicates with the local Java vault engine.

The interface should keep visual definitions in XAML and application behavior in C# rather than mixing them unnecessarily.

## Current safety boundary

The native frontend remains deliberately disconnected from:

- Cryptographic implementation details
- Direct virtual-drive mounting code
- Cloud-provider APIs
- The internet

The versioned local contract permits backend identity negotiation, privacy-limited vault summaries, and narrowly scoped vault commands, including password and recovery-key operations whose cryptographic work remains in the Java engine. Secrets cross the owner-only local socket only for the requested command, are never persisted or logged by the native frontend, and are cleared by the Java command handler after use. The Java engine remains solely responsible for password validation, recovery-key processing, encryption, mounting, revealing the readable view, and safe locking.

The version 1.0.0 native interface is intentionally English-only. No runtime translation service or partial language bundle is shipped; localization requires a later complete translation and security-review milestone.

## Engine lifecycle in the source-tree preview

Launching the native executable now starts the Java vault engine automatically when no compatible local engine with the expected settings profile already exists. The `backend.hello` handshake reports the engine profile, preventing a development build from accidentally reusing a portable, installed, or isolated-test engine and displaying that profile's vault list. The lifecycle host validates this identity before startup reuse, and the native client revalidates the exact protocol, request identifier, capabilities, backend name, and settings profile on every command connection. A mismatched engine is asked to lock its vaults and shut down safely before the correct engine starts. Closing the native app requests the same graceful shutdown for the engine process it owns; the engine refuses to exit if Windows still has a vault in use. Shutdown work runs off the WinUI thread, with the initial close deferred until cleanup finishes, so the window cannot freeze inside a synchronous engine request. A staged build uses fixed `Engine/runtime`, `Engine/classes`, and `Engine/lib` locations beside the app. Repository build output and an installed Java runtime remain development fallbacks only.

Startup identity reads have cancellation-enforced deadlines, so a stale process that accepts the local socket but never returns a complete frame cannot hold the native window indefinitely. The repository development fallback prefers the reviewed 55-library production classpath and filters known test-only libraries if it must use an older generated classpath. Privacy-safe phase timings are retained in a bounded `%LOCALAPPDATA%\VaultKind\diagnostics\startup-timing.log`; they contain elapsed phase names only, never vault details or secrets. On the July 28, 2026 zero-vault cold-start comparison, the previous 72-library fallback reached an engine-ready dashboard in 3.48 seconds. Two cleaned-fallback runs reached it in 2.26 and 3.31 seconds, with window activation at 1.00 and 1.51 seconds; retain a range because endpoint scanning and cache state vary.

The `--native-backend` launcher path now constructs a dedicated `NativeBackendComponent`. The legacy `CryptomatorComponent` and its JavaFX subcomponent are created only for the inherited GUI launcher path. Native vault commands read stable snapshots through `VaultRegistry`; they do not receive a JavaFX collection or call the JavaFX application thread. The native component owns a thread-safe plain Java vault list, applies mutations directly, and persists additions/removals explicitly. Configured-vault persistence and mount-provider selection use the value-oriented `EngineSettings` contract; the current JavaFX-backed settings model is hidden behind `LegacySettingsAdapter`. Native rename, auto-lock, and lifecycle code likewise uses plain methods instead of property access. Recovery-key generation, validation, encoding, and masterkey restoration live in the neutral `org.cryptomator.common.recovery` engine package, while interactive recovery screens remain in the legacy UI package. `VaultState` is now a JavaFX-free atomic state machine with neutral listeners and condition-based waits. `LegacyVaultStateObservable` alone adapts it to JavaFX observability and routes notifications through the frontend dispatcher. `VaultSettingsData` is the synchronized JavaFX-free source of truth for every persisted per-vault value; `LegacyVaultSettingsProperties` mirrors those values into the unchanged property API used by the inherited GUI, and serialization reads the neutral data without changing the JSON schema. Native-reachable vault creation, mounting, recovery preparation, and configuration loading consume plain per-vault settings accessors. The remaining state-derived bindings plus exception and statistics observability in `Vault` must be separated before JavaFX libraries or inherited GUI sources can be removed.

## How it runs today

The development build is an unpackaged, self-contained Windows executable. This avoids enabling Windows Developer Mode or registering a test package merely to review the prototype.

Generated build output is placed under the project's `bin` and `obj` directories and is not intended for source control.

## Migration rule

Confirmed JavaFX screens are design references, not disposable work. Native screens should preserve the workflows and accessibility decisions already approved while taking advantage of genuine Windows behavior.

The Java vault engine remains the source of truth until the native frontend has proven a complete and secure list, unlock, mount, and lock lifecycle.
