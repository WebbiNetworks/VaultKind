# VaultKind 1.0 Release Readiness

VaultKind 1.0.0 is a Windows-only, English-only release. Development builds are not release candidates and must not be distributed as the only way to access important data.

## Deterministic release layout

Run `scripts/build-native-release.ps1` from a reviewed checkout to stage:

- the self-contained WinUI frontend;
- the existing Java vault-engine classes and resolved runtime dependencies;
- a minimized Java runtime created by `jlink`;
- GPL and third-party notices; and
- a machine-readable release manifest.

The native host first looks for `Engine/runtime/bin/javaw.exe`, `Engine/classes`, and `Engine/lib` beside the application. Source-tree and installed-JDK discovery remain development fallbacks and are not acceptable release dependencies.

Run `powershell -ExecutionPolicy Bypass -File scripts\test-bundled-engine.ps1 -BinaryRoot <staged-layout>` to start the staged Java runtime with an isolated profile, verify the native protocol identity, and request graceful shutdown. This avoids reusing a compatible development engine that may already own the normal local socket.

The staging pass removes non-English .NET/WinUI satellite-resource directories. Inherited Java-engine resource bundles remain internal compatibility dependencies of the retained upstream engine; the native 1.0 interface does not load or expose them as selectable languages.

## Version 1.0 distribution boundary

VaultKind will not depend on a recurring commercial code-signing subscription. The primary candidate for version 1.0 is a Microsoft Store MSIX: Microsoft accepts an unsigned Store upload, validates it, and signs the package after certification. An isolated WinUI 3 proof app, **VaultKind Store Path Test**, was submitted for certification on July 27, 2026. Do not adapt VaultKind to this path until that certification result proves the complete route.

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

- Wait for the **VaultKind Store Path Test** certification result and record the outcome.
- If it passes, adapt VaultKind to its Microsoft-assigned Store identity and repeat package, certification, install, update, and uninstall testing with disposable vaults.
- If the Store route fails or is unsuitable, produce the unsigned portable ZIP and publish its SHA-256 checksum.
- Verify first-run extraction, replacement/upgrade behavior, removal behavior, and retained user data.
- Test extraction and first launch on clean supported Windows virtual machines with no JDK, Maven repository, source checkout, or Developer Mode.
- Test x64 first; ship ARM64 only after equivalent engine, virtual-drive, and native-library validation.
- Run native automated tests and the existing Java test suite from the exact release commit.
- Verify unlock, readable-drive reveal, lock, shutdown protection, recovery, and upgrade against disposable test vaults.
- Confirm the app is understandable and operable with signature sounds disabled.
- Record checksums and retain the exact artifacts used for release.

## Deferred non-Store signed distribution

Directly downloadable signed releases would require an organization-controlled signing identity, timestamping, and separate install, upgrade, uninstall, publisher-identity, and clean-machine testing. The project does not assume that recurring expense will ever be accepted. The current-user development certificate is never an acceptable shortcut for public distribution.

## Current native regression checks

Run:

```powershell
dotnet run --project native\VaultKind.Windows.Tests\VaultKind.Windows.Tests.csproj -c Release
```

The initial checks lock down critical Doctor classification, the exact vault-in-use warning boundary, and safe migration of preferences that still contain the retired `LanguageCode` property. This is only the first seam; backend protocol, persistence, and workflow coverage must continue to grow before 1.0.

## Explicitly excluded from 1.0

- Runtime language selection or partial localization.
- A VaultKind updater without a dedicated signed release channel.
- Automatic Vault Doctor repairs.
- macOS, Linux, web, or mobile packages.
