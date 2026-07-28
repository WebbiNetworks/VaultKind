# ADR 0002: Establish a Local Native-to-Java Backend Contract

- **Status:** Accepted for prototype development
- **Date:** 2026-07-23

## Context

The WinUI frontend must eventually operate the proven Java vault engine without importing cryptographic behavior into the interface layer. A narrow, explicit boundary is safer and easier to test than allowing UI code to reach directly into engine internals.

The contract began as read-only. Its first reviewed state-changing milestone now supports the minimum native vault lifecycle: unlock, reveal the readable Windows view, lock, and graceful backend shutdown.

## Decision

VaultKind will expose backend capabilities to the native Windows frontend through a versioned local contract.

The initial frontend code depends on an `IVaultBackend` interface rather than a specific transport or Java process. A disconnected implementation supplies an honest empty snapshot while the transport is being designed. This prevents prototype UI code from pretending that it has inspected real vaults.

## Protocol version 1 capabilities

Protocol version 1 begins with only:

- Backend identity and protocol-version negotiation
- Connection and readiness state
- List configured vault summaries
- Report each vault's stable identifier, display name, coarse state, and user-facing local path
- Report aggregate health information that the backend can prove locally

After the read-only transport was proven, protocol version 1 added:

- `vault.unlock`, with the password held in memory only for the duration of the request
- `vault.reveal`, using the engine's authoritative mounted path
- `vault.lock`, using the engine's established safe unmount operation
- `backend.shutdown`, which first locks every open vault and refuses to exit if a vault cannot be locked safely

The protocol still does **not** include:

- Recovery-key transmission
- Creating, importing, moving, sharing, or deleting vaults
- Repairs or settings changes
- Vault contents or decrypted file names

## Proposed message shape

Messages will be length-bounded UTF-8 JSON with a request identifier and explicit protocol version. Exact transport framing remains subject to a focused proof of concept.

```json
{
  "protocol": 1,
  "requestId": "local-request-id",
  "operation": "vault.list"
}
```

```json
{
  "protocol": 1,
  "requestId": "local-request-id",
  "ok": true,
  "vaults": [
    {
      "id": "stable-local-id",
      "name": "Personal",
      "state": "locked",
      "path": "F:\\Vaults\\Personal"
    }
  ]
}
```

Unknown operations or protocol versions must fail closed with a structured error. Neither side may silently guess at incompatible semantics.

### Vault summary mapping

The Java engine is authoritative for each summary. Protocol version 1 maps only:

- `id` from the vault's stable local identifier
- `name` from its user-facing display name
- `state` from its lifecycle state, serialized as a lowercase wire value
- `path` from its user-facing local path, used to identify the configured vault in the native sidebar

Valid initial state values are `missing`, `vault_config_missing`, `all_missing`, `needs_migration`, `locked`, `processing`, `unlocked`, and `error`. The native UI must tolerate a future unknown value by presenting the vault as unavailable rather than guessing its state.

The local path is deliberately included as non-secret display metadata because it is already configured by and visible to the current Windows user. Complete vault settings, vault contents, decrypted file names, and secret-bearing data remain outside this contract.

## Transport requirements

The selected local transport must:

- Be accessible only to the interactive Windows user running VaultKind.
- Reject remote connections.
- Authenticate or verify the expected peer process where practical.
- Use bounded message sizes, timeouts, cancellation, and explicit error responses.
- Avoid placing secrets in command-line arguments, environment variables, URLs, logs, crash reports, or persistent temporary files.
- Stop accepting requests when the owning VaultKind session ends.

The proof of concept originally attempted a Windows named pipe created by the native frontend with the .NET `CurrentUserOnly` restriction. Live interoperability testing demonstrated that Java 26 cannot open the Win32 pipe through its standard file APIs. Supporting it would require adding a native-access dependency to the Java engine.

The prototype therefore uses the local Unix-domain socket mechanism already supported by the Java engine and by modern Windows. The Java engine owns the socket beneath the current user's local VaultKind application-data directory, and the native frontend connects as its client. The containing bridge directory is created with an owner-only Windows ACL before the socket is opened. The frontend performs identity negotiation on every command connection before sending a request. Passwords are never placed in command-line arguments, environment variables, URLs, logs, or files, and both Java and C# discard their request-local password references immediately after use.

The first operation is `backend.hello`. It proves the expected backend identity, correlates the request identifier, negotiates protocol version 1, and reports the engine's exact settings-profile path before any vault information is requested. The native lifecycle host accepts an existing engine only when that normalized profile path matches the profile the current build is supposed to use. The native command client repeats the complete identity and profile check on every new connection, preventing a replaced or stale socket from returning another profile's vault information after startup. A compatible engine attached to a different development, portable, installed, or test profile is shut down safely and replaced rather than silently showing the wrong vault list. Once negotiation succeeds on the same connection, `vault.list` returns only the privacy-limited summaries defined above. Both request and response frames use a four-byte big-endian length followed by bounded UTF-8 JSON, with a 64 KiB maximum.

## Logging rule

The frontend and backend may log operation names, timing, protocol failures, and non-sensitive state transitions. They must not log passwords, recovery keys, keys, decrypted names, file contents, or complete sensitive paths.

## Backend construction boundary

The `--native-backend` launcher path uses a dedicated Dagger root component that exposes only `NativeBackendApplication`. It does not construct or expose the inherited JavaFX application component. The legacy root graph is initialized only when the legacy GUI path is selected.

This boundary makes native-backend reachability explicit and provides a safe starting point for Windows-only cleanup. Native commands depend on a neutral `VaultRegistry`; the native component supplies a thread-safe plain Java list, direct mutation dispatch, and explicit settings persistence. The inherited GUI component alone supplies the JavaFX observable-list and application-thread adapters. `EngineSettings` exposes configured-vault and mount-provider values without property types, while `LegacySettingsAdapter` contains the current JavaFX-backed implementation. Native rename, auto-lock, and lifecycle transitions also use value-oriented methods. Recovery-key generation, validation, encoding, and masterkey restoration are neutral engine services under `org.cryptomator.common.recovery`; only their interactive workflows remain in the legacy UI package. `VaultState` is now a JavaFX-free atomic state/concurrency primitive. Its legacy observable adapter routes notifications through the frontend-specific dispatcher, keeping JavaFX application-thread delivery out of the state machine. `VaultSettingsData` is the synchronized JavaFX-free source of truth for persisted per-vault values. `LegacyVaultSettingsProperties` preserves the existing inherited-GUI bindings and autosave invalidations, while serialization reads the neutral data and retains the existing JSON schema. Native-reachable engine services consume the corresponding plain accessors. This does not yet make the engine JavaFX-free because state-derived bindings plus exception and statistics observability still remain in `Vault`, and mounting defaults retain JavaFX-backed selection. Those implementations must be separated before deleting JavaFX libraries, GUI source trees, resources, or cross-platform workflows.

## Migration sequence

1. Native frontend used a disconnected `IVaultBackend` implementation.
2. Proved a user-restricted local transport with identity/version negotiation.
3. Connected the read-only `vault.list` operation.
4. Validated process lifecycle, cancellation, malformed input, size limits, and failure recovery.
5. Added unlock, reveal, lock, and graceful shutdown without moving cryptographic or mounting behavior into the frontend.
6. Continue migrating workflows one bounded engine command at a time.

## Consequences

This adds an architectural layer and delays visible vault integration slightly. In return, the native UI remains testable without the Java engine, the Java engine remains authoritative, and security-sensitive capabilities cannot leak into the frontend accidentally.
