# VaultKind 1.0 Release Readiness

VaultKind is a Windows-only, permanently English-only product. Development builds are not release candidates and must not be distributed as the only way to access important data.

## Deterministic release layout

Run `scripts/build-native-release.ps1` from a reviewed checkout to stage:

- the self-contained WinUI frontend;
- the existing Java vault-engine classes and resolved runtime dependencies;
- a minimized Java runtime created by `jlink`;
- GPL and third-party notices; and
- a machine-readable release manifest.

The native host first looks for `Engine/runtime/bin/javaw.exe`, `Engine/classes`, and `Engine/lib` beside the application. Source-tree and installed-JDK discovery remain development fallbacks and are not acceptable release dependencies.

Run `powershell -ExecutionPolicy Bypass -File scripts\test-bundled-engine.ps1 -BinaryRoot <staged-layout>` to start the staged Java runtime with an isolated profile. The probe verifies malformed-client isolation, protocol identity, an empty vault list, automatic/current mount-provider discovery, `vault_not_found` handling for all 11 vault-ID commands, unsupported-protocol and unknown-operation errors, and graceful shutdown. It does not create, connect, unlock, mount, or modify a vault or change the selected provider. This avoids reusing a compatible development engine that may already own the normal local socket.

The staging pass removes non-English .NET/WinUI satellite-resource directories. The Java engine carries only the reviewed English catalog and recovery-word list. Translation configuration, inherited localized bundles, language preferences/selectors, locale selection, and right-to-left layout machinery are absent from the product source and release.

The release classpath is resolved with Maven's runtime scope and the build fails if test-only libraries such as JUnit, Mockito, Byte Buddy, Hamcrest, Jimfs, or JavaFX Swing appear. The July 27 cleanup reduced the staged engine from 72 to 55 libraries and removed approximately 11.55 MB of test dependencies. Together with the English-only resource cleanup, the unpacked x64 stage decreased from about 283 MB to 269.69 MB while retaining a successful `backend.hello` and graceful-shutdown smoke test.

## Version 1.0 distribution boundary

VaultKind will not depend on a recurring commercial code-signing subscription. The primary candidate for version 1.0 is a Microsoft Store MSIX: Microsoft accepts an unsigned Store upload, validates it, and signs the package after certification. The isolated WinUI 3 proof app, **VaultKind Store Path Test**, passed certification and entered the Microsoft Store on July 28, 2026. The Store-signed version 1.0.0.0 package installed successfully, moved through Windows' supported app-volume mechanism to `G:\WindowsApps`, and launched its expected WinUI window under the Microsoft-assigned package identity `Webbi.VaultKindStorePathTest_1014d67w6rsqa`. This proves the account, upload, certification, signing, acquisition, installation, app-volume relocation, and first-launch path. It does not yet prove VaultKind's larger package, bundled Java engine, virtual-drive integrations, update behavior, or uninstall behavior.

The fallback is an unsigned portable Windows archive. Windows, SmartScreen, endpoint protection, or organization policy may warn about or block unsigned executables. Any portable download page and release notes must state that plainly and publish a SHA-256 checksum. VaultKind must never ask users to disable security controls globally; users whose policy blocks the app may be unable to run that build.

Create the portable archive and checksum with `scripts/build-native-release.ps1 -CreatePortableArchive`. An unsigned MSIX downloaded directly from the project is not a usable substitute: Windows requires an MSIX package to be signed before normal installation. The Store route is different because the Store signs the accepted package; direct signed distribution remains unavailable without a separately controlled signing identity.

The release script retains optional certificate parameters for a future signed release. `scripts/build-native-msix.ps1` also remains available for local development validation or that future milestone; it verifies that the manifest publisher exactly matches the signing certificate subject.

For local development on a Windows Application Control-protected workstation:

1. Run `powershell -ExecutionPolicy Bypass -File scripts\setup-native-development-signing.ps1` once. This creates a non-exportable current-user code-signing key and adds only its public certificate to the current user's Root and TrustedPublisher stores.
2. Build VaultKind.
3. Run `powershell -ExecutionPolicy Bypass -File scripts\sign-native-development.ps1 -BinaryRoot <build-output-directory>` after every build.

The development certificate must never be used for distributed builds. It exists only to let the current protected workstation run and test local builds.

For local package-pipeline validation only, use a separate identity ending in `.Development`:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-native-msix.ps1 `
  -BinaryRoot artifacts\VaultKind-1.0.0-win-x64 `
  -PackageName WebbiNetworks.VaultKind.Development `
  -Publisher "CN=VaultKind Development" `
  -SigningThumbprint <development-certificate-thumbprint> `
  -DevelopmentPackage
```

The script refuses to use `CN=VaultKind Development` for the production identity. A package produced this way is not the version 1.0 distribution artifact.

## Remaining release gates

- Publish a higher-version **VaultKind Store Path Test** package and prove Store update delivery, then verify uninstall and reinstall behavior.
- Confirm how the Store package will use supported virtual-drive providers. Do not assume an MSIX can install a non-Microsoft driver or NT service from inside VaultKind.
- Reserve and record VaultKind's Microsoft-assigned Store identity, then adapt the real package and repeat certification, install, update, relocation, and uninstall testing with disposable vaults.
- If the Store route fails or is unsuitable, produce the unsigned portable ZIP and publish its SHA-256 checksum.
- Verify first-run extraction, replacement/upgrade behavior, removal behavior, and retained user data.
- Test extraction and first launch on clean supported Windows virtual machines with no JDK, Maven repository, source checkout, or Developer Mode.
- Test x64 first; ship ARM64 only after equivalent engine, virtual-drive, and native-library validation.
- Run native automated tests and the existing Java test suite from the exact release commit.
- Verify unlock, readable-drive reveal, lock, shutdown protection, recovery, and upgrade against disposable test vaults.
- Confirm the app is understandable and operable with signature sounds disabled.
- Complete keyboard-only, Narrator, Windows text-scaling, high-DPI, and minimum-window accessibility passes on the release build.
- Record checksums and retain the exact artifacts used for release.
- Rebuild portable ZIP and Store-upload artifacts from the frozen release commit; superseded local packages must not be retained as release candidates.

## Current accessibility baseline

The native shell uses visible system focus indicators, moves keyboard focus into the workspace after navigation, and gives icon-only password-reveal controls explicit accessible names. The sidebar supports Up/Down movement plus Home/End jumps across Dashboard, Vault Doctor, Add Vault, configured vaults, Activity, Preferences, and Learning Center; Tab and Enter behavior remains available. The complete implemented key map and manual test matrix are maintained in [KEYBOARD_CONTROLS.md](KEYBOARD_CONTROLS.md). Dynamic workflow status text is exposed as a polite UI Automation live region, with a `LiveRegionChanged` event raised after a visible message changes. Busy indicators have task-specific names such as Opening vault, Creating vault, and Password recovery in progress rather than an unlabeled generic progress control.

The static accessible-name and focus audit covers both fixed XAML controls and controls generated at runtime. Stateful Preferences, Learning Center, FAQ, and Assistant controls now expose their selected, viewed, expanded, collapsed, or case context in their accessible names. Navigation and replacement flows deliberately place or restore focus across primary workspaces, vault creation, management, sharing, statistics, file tools, FAQ filtering, Assistant results, and completion states.

These semantic foundations do not replace hands-on assistive-technology testing. Before 1.0, test the exact release build with Narrator and keyboard-only input across creation, connection, unlock, lock, recovery, password change, removal, Vault Doctor, Preferences, Learning Center, and Assistant. Repeat the core flows at increased Windows text scale, supported DPI settings, and the minimum usable window size; record every blocker and retest its fix.

On July 27, 2026, the current signed x64 Release development build passed a hands-on zero-vault accessibility pass with Greg. Confirmed behavior includes immediate `/` shortcut handling at startup; sidebar, Preferences-tab, Learning Center, Add Vault, and new-vault keyboard focus; Narrator control names and Vault Doctor live announcements; VaultKind larger text while Windows Text size was already set to Greg's normal 160%; minimum-window scrolling and reachability; visible focus in Light and Dark; and Windows High Contrast rendering. Moving the window to a television configured at an unusually large effective scale made navigation substantially different but remained functional, and moving between displays triggered immediate reflow. This is development evidence, not the final release gate. Vault context-menu access, connected-vault and sensitive workflows, broader supported-DPI coverage, and the complete repeat on the frozen release candidate remain outstanding.

## Deferred non-Store signed distribution

Directly downloadable signed releases would require an organization-controlled signing identity, timestamping, and separate install, upgrade, uninstall, publisher-identity, and clean-machine testing. The project does not assume that recurring expense will ever be accepted. The current-user development certificate is never an acceptable shortcut for public distribution.

## Current native regression checks

Run:

```powershell
dotnet run --project native\VaultKind.Windows.Tests\VaultKind.Windows.Tests.csproj -c Release
```

The current 62 checks lock down critical Doctor classification, the exact vault-in-use warning boundary, keyboard-navigation boundaries, backend identity/profile validation, the embedded keyboard guide, local convenience-state persistence, atomic window-placement replacement, Learning Center progress filtering, and Dashboard locked/unlocked counts. This is still an early seam; backend protocol and workflow coverage must continue to grow before 1.0.

## Explicitly excluded from 1.0

- Any multilingual, translation, or runtime language-selection feature.
- A VaultKind updater without a dedicated signed release channel.
- Automatic Vault Doctor repairs.
- macOS, Linux, web, or mobile packages.
