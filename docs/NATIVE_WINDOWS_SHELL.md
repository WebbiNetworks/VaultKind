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

Launching the native executable now starts the Java vault engine automatically when no compatible local engine with the expected settings profile already exists. The `backend.hello` handshake reports the engine profile, preventing a development build from accidentally reusing a portable, installed, or isolated-test engine and displaying that profile's vault list. The lifecycle host validates this identity before startup reuse, and the native client revalidates the exact protocol, request identifier, capabilities, backend name, and settings profile on every command connection. A mismatched engine is asked to lock its vaults and shut down safely before the correct engine starts. Closing the native app requests the same graceful shutdown for the engine process it owns; the engine refuses to exit if Windows still has a vault in use. A staged build uses fixed `Engine/runtime`, `Engine/classes`, and `Engine/lib` locations beside the app. Repository build output and an installed Java runtime remain development fallbacks only.

## How it runs today

The development build is an unpackaged, self-contained Windows executable. This avoids enabling Windows Developer Mode or registering a test package merely to review the prototype.

Generated build output is placed under the project's `bin` and `obj` directories and is not intended for source control.

## Migration rule

Confirmed JavaFX screens are design references, not disposable work. Native screens should preserve the workflows and accessibility decisions already approved while taking advantage of genuine Windows behavior.

The Java vault engine remains the source of truth until the native frontend has proven a complete and secure list, unlock, mount, and lock lifecycle.
