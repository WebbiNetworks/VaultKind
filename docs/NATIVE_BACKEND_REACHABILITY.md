# Native Backend Reachability Audit

This audit records why a file or dependency is retained or removed from the VaultKind native Windows release. Static dependency warnings alone are not removal evidence because Dagger generation, Java service loading, reflection, vault frontends, and platform integrations can create runtime edges that do not appear as ordinary source references.

## Audited entry point

`NativeBackendMain` is the dedicated release and development entry point used by the WinUI host. It initializes only the shared property/logging bootstrap and `DaggerNativeBackendComponent.create().application()`. The component exposes only `NativeBackendApplication` and includes `NativeBackendModule` plus `CommonsModule`. It does not reference or construct `Cryptomator`, `CryptomatorComponent`, `FxApplicationComponent`, or inherited JavaFX screen subcomponents.

The generated `DaggerNativeBackendComponent` confirms that the native root reaches the protocol bridge, vault registry and list manager, vault creation and operations, recovery-key services, auto-lock, mount selection and mounting, notifications, settings, executors, and shutdown handling. Per-vault legacy JavaFX adapters remain lazy compatibility edges inside `Vault`; their presence means JavaFX libraries and the compiled adapter classes are not yet safe to remove.

The former `Cryptomator --native-backend` branch was unsuitable as a class-trimming root because `Cryptomator` bytecode also references JavaFX `Application`, the legacy Dagger graph, IPC GUI startup, and `FxApplicationComponent`. The dedicated entry point removes that false reachability without deleting the inherited launcher.

## Resource evidence

The pre-audit x64 stage copied `target/classes` wholesale. That included 92 FXML files, eight CSS/font files, and 40 inherited GUI images. Their compiled-source sizes were approximately 262 KB, 1.27 MB, and 1.51 MB respectively.

The native backend has explicit runtime resource requirements:

- `logback-native.xml` configures backend logging.
- `i18n/strings.properties` supplies the retained English engine messages.
- `i18n/4096words_en.txt` supplies recovery-key word encoding.
- `THIRD-PARTY.txt` is copied into release notices.
- `module-info.class` is retained while the Java build remains a single modular source tree.
- Every compiled `org/cryptomator` class is retained at this boundary.

No native-backend-reachable code loads an FXML document, legacy CSS/font, or GUI image. Those presentation resources are consumed by the inherited JavaFX application graph, which the native entry point does not construct. The native WinUI frontend has its own compiled XAML/PRI resources and Assets directory outside `Engine/classes`.

## First verified removal slice

`scripts/build-native-release.ps1` now stages all compiled Java classes and only the explicit backend resources above. It rejects a build if any required resource is missing and rejects a stage if `fxml`, `css`, or `img` appears under `Engine/classes`.

This is packaging-only removal. The inherited JavaFX source/resources remain available to Maven tests and the legacy launcher, and no Java service-loaded provider or platform integration is removed.

## Retained pending further proof

- JavaFX libraries and legacy adapter classes remain because common settings and per-vault compatibility facades still compile against them.
- Mount frontend and integrations JARs remain because providers are discovered dynamically and must be tested on supported Windows provider configurations before trimming.
- macOS/Linux frontend JARs, Maven profiles, distribution files, and GitHub workflows are candidates for later Windows-only cleanup, but are not part of this resource boundary.
- Compiled inherited GUI classes remain until a class-level reachability method accounts for Dagger factories, service descriptors, reflection, and all native protocol commands.

## Class inventory

`scripts/audit-native-backend-classes.ps1` now builds a repeatable class-level inventory from the staged `NativeBackendMain`. It asks the release JDK's `jdeps` for every direct authored-class edge with same-package filtering disabled, then walks that graph from the dedicated entry point. It writes the reachable list, the static-candidate list, and a machine-readable summary under `target/native-backend-reachability`.

The first inventory found 1,095 authored `org.cryptomator` classes: 180 are statically reachable from the dedicated native entry point and 915 are outside that static graph. The 915 candidates occupy 3,415,768 bytes (3.26 MiB) before compression. Most are inherited UI classes (666), followed by legacy launcher classes (131).

This is evidence for review, not permission to remove 915 classes. The report deliberately marks `removalAuthorized` false. Dagger-generated/nested implementations, service-loaded platform providers, reflection or serialization-created types, and full native-protocol/provider coverage must be accounted for before any packaging filter consumes the candidate list. Thirty currently reachable authored classes remain under `org.cryptomator.ui`; some are neutral cryptographic/key-loading implementations in a legacy package, while others arrive through shared Dagger component types. Those edges must be classified before a narrow removal slice is selected.

The first classification found that the entire 30-class UI branch was rooted by a single direction violation: neutral `Constants` and `VaultListManager` imported `MasterkeyFileLoadingStrategy` only to reuse its compile-time `masterkeyfile` scheme string. `MASTERKEY_SCHEME` now lives beside `MASTERKEY_FILENAME` in the neutral constants layer. Native password unlock, legacy key-loading bindings, and the inherited strategy all share that value without an engine-to-GUI import.

After that change, the staged graph contains 145 statically reachable authored classes and 950 static candidates. No authored `org.cryptomator.ui` class or `JavaFXUtil` remains reachable from `NativeBackendMain`. The boundary removed 35 classes and 100,621 bytes from the reachable set because the 30 UI classes had five additional authored dependencies. Source and staged classes are still retained at this audit boundary; this result establishes eligibility for a later packaging slice rather than claiming immediate package savings.

## Runtime retention inventory

The audit also inventories dynamic edges that `jdeps` cannot prove on its own. The current 55 release JARs contain 19 `META-INF/services` descriptors naming 30 provider implementations. JAR contents remain whole at this boundary, so all of those filesystem, cryptography, mount, Windows integration, Jackson, Jetty, and logging providers remain present. This inventory is recorded in generated `target/native-backend-reachability/runtime-retention.json` rather than being used to trim inside third-party JARs.

The authored module descriptor declares three `uses` services and six `provides` groups. The native engine launches with `-cp`, not `--module-path`, so the JVM does not activate those authored module service declarations for `NativeBackendMain`; they remain documented for the inherited modular launcher. This distinction prevents legacy JavaFX service providers from being retained under a false native-runtime assumption.

Only two dynamic-construction sites are reachable in authored native code: settings deserialization and native protocol request deserialization. Their three reviewed authored targets (`SettingsJson`, `VaultSettingsJson`, and `NativeUiProtocol.NativeUiRequest`) are asserted to remain inside the 145-class static closure. The audit also applies a nested-class family check; no nested class belonging to a reachable outer class currently falls into the candidate set. Dagger-generated classes used by the native graph are explicit bytecode dependencies and are already present in the static closure.

The zero-vault behavioral blocker is now closed. The staged-engine probe rejects malformed JSON without losing the listener, reconnects, verifies `backend.hello`, confirms an empty `vault.list`, inventories the current mount providers without changing the selection, verifies `vault_not_found` for all 11 vault-ID commands, checks unsupported-protocol and unknown-operation errors, and requests graceful shutdown. Destructive creation, provider-selection changes, live-vault commands, password/recovery behavior against a real vault, and actual mounting remain deferred to intentionally disposable fixtures.

The next safe packaging step is a narrow authored-class filter driven by the reviewed static candidate list. Apply one small slice, rebuild the stage, rerun this zero-vault probe, and compare the resulting class inventory and package size. Do not filter third-party JAR contents or remove mount-provider/platform integrations until provider-specific runtime coverage exists.

## Required validation after this boundary

1. Complete Java test suite.
2. Native policy/protocol test suite.
3. Fresh x64 staged release build.
4. Assertion that required resources exist and inherited presentation directories do not.
5. Bundled-engine zero-vault protocol probe, including malformed-client recovery and graceful shutdown.

Live vault or provider testing remains a separate milestone and must use intentionally disposable vaults.

## Validation result

The boundary passed on July 28, 2026:

- 315 Java tests passed with zero failures and zero errors; two platform-specific tests were skipped.
- 66 native policy, protocol, persistence, keyboard, documentation, and workflow checks passed.
- The fresh x64 stage contains 1,101 files under `Engine/classes`, including all compiled classes and required resources.
- `fxml`, `css`, and `img` are absent from staged engine classes; English messages, the recovery word list, and native logging configuration are present.
- The bundled engine passed `backend.hello` and graceful shutdown.
- The stage is 279,876,142 bytes (266.91 MiB). The removed presentation resources total 3,042,203 bytes (2.90 MiB), compared with the same stage contents before this filter.

The later permanent English-only cleanup removed two additional compiled language-policy classes plus the language/orientation machinery. The refreshed stage contains 1,099 files under `Engine/classes` and is 279,861,126 bytes (266.90 MiB). Only the `en-us` WinUI satellite directory, the English string catalog, and the English recovery-word list remain as authored language assets.

The dedicated `NativeBackendMain` validation also passed all 315 Java tests (two platform skips), all 66 native checks, a fresh x64 stage, and the bundled-engine smoke test. Its extra entry class brings the current stage to 279,862,701 bytes and 1,100 files under `Engine/classes`; this is a 1,575-byte increase over the prior English-only stage and establishes a clean root for the class inventory above.

The neutral masterkey-scheme boundary passed all 315 Java tests (two platform skips), a fresh x64 stage, and the bundled-engine smoke test. The refreshed stage is 279,862,559 bytes; the 142-byte physical reduction is incidental because compiled candidates remain packaged until runtime retention evidence is complete.

The expanded zero-vault probe passed on July 28, 2026. It exercises malformed-client isolation, backend identity, empty vault listing, current mount-provider discovery, all 11 missing-vault command paths, unsupported protocol, unknown operation, and graceful shutdown against the staged runtime. The bridge now contains malformed input to the offending connection so a fresh client can reconnect. All 315 Java tests pass with zero failures/errors (two platform skips), all 66 native checks pass, and the refreshed unfiltered x64 stage is 279,862,721 bytes. No real vault, password, recovery key, mount, provider selection, or user profile was touched.
