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

## Signing boundary

Windows Application Control can block an unsigned VaultKind executable. A production release therefore requires an organization-controlled code-signing certificate and timestamping. Private keys and certificate passwords must never be stored in this repository.

The staging script accepts an installed certificate thumbprint and signs only the VaultKind-authored executable and DLL. An unsigned staged layout is useful for build inspection but is explicitly not a release candidate.

For local development on a Windows Application Control-protected workstation:

1. Run `powershell -ExecutionPolicy Bypass -File scripts\setup-native-development-signing.ps1` once. This creates a non-exportable current-user code-signing key and adds only its public certificate to the current user's Root and TrustedPublisher stores.
2. Build VaultKind.
3. Run `powershell -ExecutionPolicy Bypass -File scripts\sign-native-development.ps1 -BinaryRoot <build-output-directory>` after every build.

The development certificate must never be used for distributed builds. Production signing remains an organization-controlled release operation.

## Remaining release gates

- Choose and protect the production code-signing certificate.
- Produce the signed installer/MSIX around the staged layout.
- Verify publisher identity, upgrade behavior, uninstall behavior, and retained user data.
- Test installation and first launch on clean supported Windows virtual machines with no JDK, Maven repository, source checkout, or Developer Mode.
- Test x64 first; ship ARM64 only after equivalent engine, virtual-drive, and native-library validation.
- Run native automated tests and the existing Java test suite from the exact release commit.
- Verify unlock, readable-drive reveal, lock, shutdown protection, recovery, and upgrade against disposable test vaults.
- Confirm the app is understandable and operable with signature sounds disabled.
- Record checksums and retain the exact signed artifacts used for release.

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
