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

This is evidence for review, not blanket permission to remove every candidate. The report deliberately marks `removalAuthorized` false. Dagger-generated/nested implementations, service-loaded platform providers, reflection or serialization-created types, and native-protocol/provider coverage must be accounted for before a candidate is consumed by a packaging filter. A separately reviewed, exact class slice may be authorized only with a fail-closed build guard and its applicable staged-engine coverage.

The first classification found that the entire 30-class UI branch was rooted by a single direction violation: neutral `Constants` and `VaultListManager` imported `MasterkeyFileLoadingStrategy` only to reuse its compile-time `masterkeyfile` scheme string. `MASTERKEY_SCHEME` now lives beside `MASTERKEY_FILENAME` in the neutral constants layer. Native password unlock, legacy key-loading bindings, and the inherited strategy all share that value without an engine-to-GUI import.

After that change, the staged graph contains 145 statically reachable authored classes and 950 static candidates. No authored `org.cryptomator.ui` class or `JavaFXUtil` remains reachable from `NativeBackendMain`. The boundary removed 35 classes and 100,621 bytes from the reachable set because the 30 UI classes had five additional authored dependencies. Source and staged classes are still retained at this audit boundary; this result establishes eligibility for a later packaging slice rather than claiming immediate package savings.

## Runtime retention inventory

The audit also inventories dynamic edges that `jdeps` cannot prove on its own. The current 55 release JARs contain 19 `META-INF/services` descriptors naming 30 provider implementations. JAR contents remain whole at this boundary, so all of those filesystem, cryptography, mount, Windows integration, Jackson, Jetty, and logging providers remain present. This inventory is recorded in generated `target/native-backend-reachability/runtime-retention.json` rather than being used to trim inside third-party JARs.

The authored module descriptor declares three `uses` services and six `provides` groups. The native engine launches with `-cp`, not `--module-path`, so the JVM does not activate those authored module service declarations for `NativeBackendMain`; they remain documented for the inherited modular launcher. This distinction prevents legacy JavaFX service providers from being retained under a false native-runtime assumption.

Only two dynamic-construction sites are reachable in authored native code: settings deserialization and native protocol request deserialization. Their three reviewed authored targets (`SettingsJson`, `VaultSettingsJson`, and `NativeUiProtocol.NativeUiRequest`) are asserted to remain inside the 145-class static closure. The audit also applies a nested-class family check; no nested class belonging to a reachable outer class currently falls into the candidate set. Dagger-generated classes used by the native graph are explicit bytecode dependencies and are already present in the static closure.

The staged-engine behavioral probe now owns an isolated disposable fixture under its temporary profile. It rejects malformed JSON without losing the listener, reconnects, verifies `backend.hello`, confirms an initially empty `vault.list`, and inventories the current mount providers without changing the selection. It then creates and registers a disposable encrypted vault, verifies recovery-key display and wrong-password rejection, changes and recovers the password, renames and removes the vault, reconnects the existing vault, removes it again, and confirms the isolated list is empty. It also verifies `vault_not_found` for all 11 vault-ID commands, checks unsupported-protocol and unknown-operation errors, and requests graceful shutdown. The wrapper removes the entire temporary engine profile and vault fixture afterward. Actual mounting, provider-selection changes, readable-drive operations, and live file activity remain deferred.

The first physical filter removes only the six compiled classes in `org.cryptomator.ui.dialogs`. Every class in that package is an unreachable static candidate and directly belongs to the inherited JavaFX/FXML dialog layer. The release script names every file explicitly and compares the reviewed list with the compiled package before removal; any future class-set change fails the build pending a new audit. This is deliberately not a wildcard or a general license to consume the candidate report.

The second physical filter applies the same guard to the ten compiled classes in `org.cryptomator.ui.wrongfilealert`. That package is also wholly within the unreachable inherited JavaFX/FXML window graph; its main-window references are unreachable from the native entry point. The native vault-health and warning experiences do not use these legacy classes.

The third physical filter applies the guard to the ten compiled classes in `org.cryptomator.ui.updatereminder`. Every reference to this package is inside the unreachable inherited JavaFX application/window graph. This omits only the legacy reminder window and does not remove the update-checking engine or affect the Microsoft Store update path.

The fourth physical filter applies the guard to the ten compiled classes in `org.cryptomator.ui.sharevault`. The package is wholly within the unreachable inherited JavaFX sharing-window graph. VaultKind's native sharing/navigation experience is separate; vault data and engine operations remain packaged.

The fifth physical filter applies the guard to the twelve compiled classes in `org.cryptomator.ui.stats`. They implement the unreachable inherited JavaFX statistics chart window. The underlying vault-statistics engine remains packaged for native use.

The sixth physical filter applies the guard to the eleven compiled classes in `org.cryptomator.ui.decryptname`. They implement the unreachable inherited JavaFX filename-inspection window and its view model. The cryptographic filesystem and vault engine remain packaged.

The seventh physical filter applies the guard to the eleven compiled classes in `org.cryptomator.ui.error`. They implement the unreachable inherited JavaFX error window, its Dagger wiring, and its window-specific discussion model. Native error reporting and the shared `org.cryptomator.common.ErrorCode` engine class remain packaged.

The eighth physical filter applies the guard to the fifteen compiled classes in `org.cryptomator.ui.controls`. They are inherited JavaFX-only labels, icons, text fields, password widgets, and view helpers referenced exclusively by the unreachable legacy JavaFX graph. The native WinUI controls, native password handling, and neutral `org.cryptomator.common.Passphrase` engine class remain packaged.

The ninth physical filter applies the guard to the fifteen compiled classes in `org.cryptomator.ui.quit`. They implement the unreachable inherited JavaFX quit and forced-quit dialogs. VaultKind's native graceful-shutdown protocol, vault lifecycle engine, and native window-close handling remain packaged; the bundled-engine probe exercises the native shutdown path after every release rebuild.

The tenth physical filter applies the guard to the seventeen compiled classes in `org.cryptomator.ui.lock`. They implement the unreachable inherited JavaFX lock, force-retry, and failure-dialog workflow. The package only catches the mount API's unmount-failure type; it neither provides nor dynamically loads a mount service. Neutral `Vault.lock`, every mount-provider integration, and VaultKind's native lock handling remain packaged.

Static candidacy alone remains insufficient. The ten `org.cryptomator.ui.traymenu` classes were reviewed and deliberately retained because the runtime inventory identifies `AwtTrayMenuController` as a `TrayMenuController` service provider. The package is not eligible for this static-only filtering process without stronger runtime evidence.

The next safe packaging step is another independently reviewed, exact inherited GUI-only package, now using the disposable-vault probe for password, recovery, registration, rename, removal, and reconnection evidence. Rebuild the stage, rerun the complete isolated probe, and compare the resulting class inventory and package size after every slice. Do not filter third-party JAR contents or remove mount-provider/platform integrations until provider-specific runtime coverage exists.

## Required validation after this boundary

1. Complete Java test suite.
2. Native policy/protocol test suite.
3. Fresh x64 staged release build.
4. Assertion that required resources exist and inherited presentation directories do not.
5. Bundled-engine isolated disposable-vault protocol probe, including malformed-client recovery, password/recovery rotation, fixture cleanup, and graceful shutdown.

Mounted-vault and provider testing remains a separate milestone and must use intentionally disposable vaults.

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

The first physical class slice passed on July 29, 2026. The filtered stage contains 1,089 authored classes: the same 145 remain statically reachable and 944 remain candidates after the six reviewed dialog classes were omitted. `Engine/classes` contains 1,094 class/resource files, no dialog class survived, and the complete stage is 279,838,327 bytes. This is an exact 24,394-byte reduction from the immediately preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The second physical class slice passed on July 29, 2026. The filtered stage contains 1,079 authored classes: the same 145 remain statically reachable and 934 remain candidates after the ten reviewed wrong-file-alert classes were omitted. `Engine/classes` contains 1,084 class/resource files, neither reviewed package survives, and the complete stage is 279,817,672 bytes. The second slice saves exactly 20,655 bytes; both slices save 45,049 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The third physical class slice passed on July 29, 2026. The filtered stage contains 1,069 authored classes: the same 145 remain statically reachable and 924 remain candidates after the ten reviewed update-reminder classes were omitted. `Engine/classes` contains 1,074 class/resource files, none of the three reviewed packages survives, and the complete stage is 279,797,490 bytes. The third slice saves exactly 20,182 bytes; all three slices save 65,231 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The fourth physical class slice passed on July 29, 2026. The filtered stage contains 1,059 authored classes: the same 145 remain statically reachable and 914 remain candidates after the ten reviewed share-vault classes were omitted. `Engine/classes` contains 1,064 class/resource files, none of the four reviewed packages survives, and the complete stage is 279,774,392 bytes. The fourth slice saves exactly 23,098 bytes; all four slices save 88,329 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The fifth physical class slice passed on July 29, 2026. The filtered stage contains 1,047 authored classes: the same 145 remain statically reachable and 902 remain candidates after the twelve reviewed statistics-window classes were omitted. `Engine/classes` contains 1,052 class/resource files, none of the five reviewed packages survives, and all ten tray-menu classes remain. The complete stage is 279,738,622 bytes. The fifth slice saves exactly 35,770 bytes; all five slices save 124,099 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The sixth physical class slice passed on July 29, 2026. The filtered stage contains 1,036 authored classes: the same 145 remain statically reachable and 891 remain candidates after the eleven reviewed filename-inspection classes were omitted. `Engine/classes` contains 1,041 class/resource files, none of the six reviewed packages survives, and all ten tray-menu classes remain. The complete stage is 279,697,455 bytes. The sixth slice saves exactly 41,167 bytes; all six slices save 165,266 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The seventh physical class slice passed on July 29, 2026. The filtered stage contains 1,025 authored classes: the same 145 remain statically reachable and 880 remain candidates after the eleven reviewed JavaFX error-window classes were omitted. `Engine/classes` contains 1,030 class/resource files, none of the seven reviewed packages survives, and all ten tray-menu classes remain. The complete stage is 279,664,538 bytes. The seventh slice saves exactly 32,917 bytes; all seven slices save 198,183 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The eighth physical class slice passed on July 29, 2026. The filtered stage contains 1,010 authored classes: the same 145 remain statically reachable and 865 remain candidates after the fifteen reviewed JavaFX control classes were omitted. `Engine/classes` contains 1,015 class/resource files, none of the eight reviewed packages survives, and all ten tray-menu classes remain. The complete stage is 279,602,233 bytes. The eighth slice saves exactly 62,305 bytes; all eight slices save 260,488 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged.

The ninth physical class slice passed on July 29, 2026. The filtered stage contains 995 authored classes: the same 145 remain statically reachable and 850 remain candidates after the fifteen reviewed JavaFX quit-dialog classes were omitted. `Engine/classes` contains 1,000 class/resource files, none of the nine reviewed packages survives, and all ten tray-menu classes remain. The complete stage is 279,558,478 bytes. The ninth slice saves exactly 43,755 bytes; all nine slices save 304,243 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe, including graceful shutdown, passed unchanged.

The tenth physical class slice passed on July 29, 2026. The filtered stage contains 978 authored classes: the same 145 remain statically reachable and 833 remain candidates after the seventeen reviewed JavaFX lock-workflow classes were omitted. `Engine/classes` contains 983 class/resource files, none of the ten reviewed packages survives, and all ten tray-menu classes remain. The complete stage is 279,515,959 bytes. The tenth slice saves exactly 42,519 bytes; all ten slices save 346,762 bytes cumulatively from the preceding unfiltered stage. The complete zero-vault bundled-engine probe passed unchanged, including mount-provider inventory and graceful shutdown.

The isolated disposable-vault probe passed on July 29, 2026. It creates its vault only beneath the temporary engine profile, verifies registration, recovery-key stability, wrong-password rejection, password change, recovery-key password reset, rename, removal, reconnection, and final deregistration, then relies on the guarded wrapper to delete the complete temporary profile. The unchanged mount-provider inventory and graceful shutdown checks also pass. The 66 native policy checks pass independently. No configured user vault, user profile, provider selection, mount point, or readable drive is touched.
