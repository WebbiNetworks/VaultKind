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
| `Assets/AppIcon.ico` and Windows logo assets | The approved metallic-and-blue VK mark exported for the executable, desktop shortcut, title bar, taskbar, Store logo, package tiles, lock-screen logo, wide tile, and splash screen. |
| `MainPage.xaml` | The current VaultKind Dashboard and sidebar appearance. XAML describes what appears and how it is arranged. |
| `MainPage.xaml.cs` | Dashboard behavior, navigation, engine connection status, vault rendering, and native unlock/open/lock interactions. |
| `Services/JavaVaultEngineHost.cs` | Development lifecycle host that starts and stops the existing Java vault engine with the native app. |
| `Services/DoctorFindingPolicy.cs` | Defines the narrow evidence threshold for critical Vault Doctor warnings. |
| `Services/KeyboardNavigationPolicy.cs` | Defines testable sidebar Up/Down/Home/End movement and wrap boundaries. |
| `Services/SignatureSoundPolicy.cs` | Keeps warning audio limited to explicitly approved safety events. |
| `Services/WindowPlacementStore.cs` | Saves the last window placement locally and safely brings it back on-screen if the monitor layout later changes. |
| `../VaultKind.Windows.Tests` | Package-free native policy and preference regression checks. |
| `../../scripts/build-native-release.ps1` | Stages the self-contained frontend, bundled Java engine/runtime, notices, and optional signatures. |
| `NATIVE_BACKEND_REACHABILITY.md` | Records evidence for native release trimming and the boundaries that remain unsafe to remove. |
| `../../src/main/java/org/cryptomator/launcher/NativeBackendMain.java` | Dedicated headless Java entry point used by the WinUI host and staged-engine tests. |
| `../../scripts/audit-native-backend-classes.ps1` | Generates a conservative staged-class reachability inventory from `NativeBackendMain`; its candidate list is not an automatic deletion list. |
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

The native interface is permanently English-only. No runtime translation service, language preference, selector, right-to-left layout option, or translation bundle is shipped or planned. The centralized English catalog remains a wording source, not localization infrastructure.

## Engine lifecycle in the source-tree preview

Launching the native executable now starts the Java vault engine automatically when no compatible local engine with the expected settings profile already exists. The `backend.hello` handshake reports the engine profile, preventing a development build from accidentally reusing a portable, installed, or isolated-test engine and displaying that profile's vault list. The lifecycle host validates this identity before startup reuse, and the native client revalidates the exact protocol, request identifier, capabilities, backend name, and settings profile on every command connection. A mismatched engine is asked to lock its vaults and shut down safely before the correct engine starts. Closing the native app requests the same graceful shutdown for the engine process it owns; the engine refuses to exit if Windows still has a vault in use. Shutdown work runs off the WinUI thread, with the initial close deferred until cleanup finishes, so the window cannot freeze inside a synchronous engine request. A staged build uses fixed `Engine/runtime`, `Engine/classes`, and `Engine/lib` locations beside the app. Repository build output and an installed Java runtime remain development fallbacks only.

Packaged VaultKind builds are required to contain a signed-content `VaultKind.PackageProfile.json` marker generated by `build-native-msix.ps1`. Its validated identifier moves that package's engine settings, logs, plugins, mount-point base, preferences, activity, learning progress, Doctor summary, window placement, startup timing, diagnostics, and generated sound assets beneath `%LOCALAPPDATA%\VKP\<id>`; Windows then isolates those writes inside the package's virtualized application-data boundary. The Java child receives the same data root through its process-local `LOCALAPPDATA`. Because package virtualization makes that physical path too long and can resolve differently across the Java and .NET processes, the ephemeral local bridge alone uses the owner-only `%USERPROFILE%\.vaultkind-runtime\<id>\native-bridge-v1.sock` path. Identifiers are limited to sixteen characters, and the complete bridge path must remain within Windows' 108-character Unix-domain-socket limit. The native client and lifecycle host share one resolver, so no package can attach to the permanent unpackaged engine or display its vault list. A development marker must explicitly declare `developmentOnly: true` and use a package name ending in `.Development`; a Microsoft Store marker must explicitly declare `false` and use the reviewed non-development identity. Missing markers preserve the ordinary permanent profile for unpackaged builds. A missing package-kind flag, malformed name, kind/name mismatch, path traversal, overlong identifier, or stale pre-staged marker fails closed.

The July 31 development package inspection confirmed that this marker is present only inside the signed `.Development` MSIX and names `LocalMsix.WebDav`; the ordinary staged release remains marker-free. Versions `1.0.0.0` and `1.0.0.1` exposed, respectively, the 108-character socket limit and the effect of package file-system virtualization. Version `1.0.0.2` uses the external ephemeral bridge boundary above. Its installed window reached a healthy local engine in 3.75 seconds, showed zero vaults and fresh package-local UI state, and direct protocol inspection confirmed the exact isolated profile plus all five provider choices. A guarded package-aware probe then selected Windows Explorer WebDAV, created and unlocked one temporary external vault, mounted a new drive letter, wrote/read/deleted a marker file, locked and unmounted, deregistered the vault, restored Automatic, removed its guarded temporary directory, and deliberately left the package engine running. The permanent VK1/VK2 registry and unpackaged profile were not read or changed.

The subsequent real-UI package matrix used a disposable external vault named `Mooselock`. Its encrypted folder contained eight files totaling 2,403 bytes, with manifest SHA-256 `EBB8C4F49AD19F4B2CA1AFDAD2884922F0E650F63079E522DDC0F37CA09BCAC0`. An in-place development-package update preserved its registration and exact encrypted fingerprint. Uninstalling only `WebbiNetworks.VaultKind.Development` removed the package-local registry but left the external encrypted folder byte-for-byte unchanged. After a clean reinstall, reconnecting that folder restored access: the test operator unlocked it, read the disposable text file through the mounted view, and locked it again. This proves the local development MSIX boundary for external-vault retention; it is not evidence that the real VaultKind Store identity has passed Microsoft certification.

Development package `1.0.0.6` adds two confirmed creation/connection safeguards. When a selected empty folder already has the requested vault name, VaultKind uses it directly rather than appending the name a second time. A matching non-empty folder is rejected at the exact displayed path, preventing accidental `Name\\Name` nesting; selecting its parent still produces the expected child path. Connect-vault validation errors now wrap within the available content width. The test operator confirmed both visible behaviors. The package builder also signs and verifies the authored EXE and DLL before packing a development MSIX, then signs and verifies the package envelope. This was added after Windows Application Control blocked version `1.0.0.3` when a refreshed stage contained unsigned internal binaries even though its MSIX envelope was valid.

Startup identity reads have cancellation-enforced deadlines, so a stale process that accepts the local socket but never returns a complete frame cannot hold the native window indefinitely. The repository development fallback prefers the reviewed 55-library production classpath and filters known test-only libraries if it must use an older generated classpath. Privacy-safe phase timings are retained in a bounded `%LOCALAPPDATA%\VaultKind\diagnostics\startup-timing.log`; they contain elapsed phase names only, never vault details or secrets. On the July 28, 2026 zero-vault cold-start comparison, the previous 72-library fallback reached an engine-ready dashboard in 3.48 seconds. Two cleaned-fallback runs reached it in 2.26 and 3.31 seconds, with window activation at 1.00 and 1.51 seconds; retain a range because endpoint scanning and cache state vary.

The dedicated `NativeBackendMain` entry point constructs `NativeBackendComponent` directly. The legacy `CryptomatorComponent` and its JavaFX subcomponent are created only for the inherited GUI launcher path. Native vault commands read stable snapshots through `VaultRegistry`; they do not receive a JavaFX collection or call the JavaFX application thread. The native component owns a thread-safe plain Java vault list, applies mutations directly, and persists additions/removals explicitly. Configured-vault persistence and mount-provider selection use the value-oriented `EngineSettings` contract; the current JavaFX-backed settings model is hidden behind `LegacySettingsAdapter`. Native rename, auto-lock, and lifecycle code likewise use plain methods instead of property access. Recovery-key generation, validation, encoding, and masterkey restoration live in the neutral `org.cryptomator.common.recovery` engine package, while interactive recovery screens remain in the legacy UI package. `VaultState` is now a JavaFX-free atomic state machine with neutral listeners and condition-based waits. `LegacyVaultStateObservable` alone adapts it to JavaFX observability and routes notifications through the frontend dispatcher. `VaultSettingsData` is the synchronized JavaFX-free source of truth for every persisted per-vault value; `LegacyVaultSettingsProperties` mirrors those values into the unchanged property API used by the inherited GUI, and serialization reads the neutral data without changing the JSON schema. Native-reachable vault creation, mounting, recovery preparation, and configuration loading consume plain per-vault settings accessors. State-derived bindings now live in a lazily created legacy facade; the most recent exception is stored in JavaFX-free `VaultExceptionState`; and `VaultStats` uses a neutral scheduled sampler and immutable snapshots while the inherited statistics screen receives dispatched JavaFX properties through `LegacyVaultStatsObservable`. `MountServiceSelector` resolves global and per-vault providers through plain settings values; `Mounter` no longer receives a JavaFX observable, while the inherited options screen retains its live adapter. The native-backend reachability audit is recorded in `NATIVE_BACKEND_REACHABILITY.md`. Its first verified packaging slice omits inherited FXML, CSS/fonts, and GUI images from staged native releases while retaining all compiled classes, required English/recovery resources, and service-loaded providers.

## How it runs today

The WinUI host now starts `NativeBackendMain` directly. This supersedes the earlier shared `Cryptomator --native-backend` branch and prevents the inherited JavaFX launcher graph from appearing reachable merely because both launch modes shared one class.

The `masterkeyfile` scheme identifier is owned by neutral engine constants. Common vault discovery and native password unlock no longer import the inherited JavaFX password-loading strategy merely to reuse that string. The staged reachability inventory consequently contains no authored `org.cryptomator.ui` class reachable from `NativeBackendMain`.

The reachability audit additionally records every release-JAR service descriptor, authored module service declaration, reachable deserialization site, reviewed dynamic target, and nested-class family. Third-party JARs remain whole. Because the native process launches on the classpath, authored `module-info` providers are not treated as active native services.

The development build is an unpackaged, self-contained Windows executable. This avoids enabling Windows Developer Mode or registering a test package merely to review the prototype.

Generated build output is placed under the project's `bin` and `obj` directories and is not intended for source control.

The Windows identity artwork is derived deterministically from the approved repository sources `vaultkind_mark_256.png` and `vaultkind_full_lockup.png`; it is not a separate reinterpretation of the brand. The standalone transparent VK mark is used for app-icon surfaces, including an eight-frame 16-through-256-pixel ICO embedded into the executable through `ApplicationIcon`. The full VaultKind lockup is reserved for the wide tile and splash screen. All PNG package assets use the exact Microsoft-required dimensions and 8-bit output. The test operator confirmed the rebuilt desktop shortcut, title-bar, and taskbar icons on July 31, 2026.

## Migration rule

Confirmed JavaFX screens are design references, not disposable work. Native screens should preserve the workflows and accessibility decisions already approved while taking advantage of genuine Windows behavior.

The Java vault engine remains the source of truth until the native frontend has proven a complete and secure list, unlock, mount, and lock lifecycle.
