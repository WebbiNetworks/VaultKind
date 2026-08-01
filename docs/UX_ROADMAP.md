# VaultKind UX Roadmap

This file records the current native-shell UX state and the agreed ideas that remain outside the current implementation milestone.

All roadmap work follows the product direction in [PRODUCT_PHILOSOPHY.md](PRODUCT_PHILOSOPHY.md): **Desktop First. Windows Focused. Privacy Always.** Windows is the sole supported platform, and cross-platform parity is not a roadmap objective.

VaultKind is permanently English-only. Multilingual support is not on the roadmap. Translation bundles, language selectors/preferences, localization services, and right-to-left layout machinery must not be reintroduced.

## Windows-only release hygiene

- Production classpath generation now excludes test-scoped Java dependencies and rejects known test libraries if they reappear.
- The repository no longer tracks legacy macOS/Linux distribution files, platform build profiles, IDE metadata, the completed Store-path experiment, or inherited release automation. Public CI is a least-privilege Windows-only test workflow with commit-pinned official actions.
- The Java engine source and staged release retain only the reviewed default English catalog and recovery-word list. Inherited translation files, Crowdin configuration, language/orientation settings, the selector, and locale-selection machinery have been removed.
- Superseded Debug, validation, portable ZIP, and development-MSIX outputs are disposable generated artifacts. Preserve the active signed development build, the current validated stage, and the identity and validation records for the certified Store-path proof.
- The isolated Store-path proof is complete: Microsoft-certified versions `1.0.0.0` and `1.0.1.0` passed acquisition, supported app-volume relocation, Store-managed update, uninstall, clean reinstall, and launch after both update and reinstall. Real VaultKind packaging still requires disposable-vault validation of the bundled engine, supported virtual-drive provider, and external encrypted-data retention boundaries.
- Native startup now records bounded, privacy-safe phase timings, enforces cancellation deadlines on engine identity frames, and excludes known test libraries from the development engine classpath. A measured zero-vault cold start changed from 3.48 seconds with the old classpath to 2.26–3.31 seconds across two cleaned runs, with the window active in 1.00–1.51 seconds. Engine cleanup now runs off the UI thread and defers final window closure until cleanup completes; the test operator confirmed two consecutive closes and an instant warm reopen. Retain the timing log through release-candidate testing to catch intermittent security-scanning regressions.
- Native backend startup uses a dedicated Dagger root component and no longer exposes the inherited JavaFX application graph. Its configured-vault registry uses a thread-safe plain Java list, neutral snapshots, direct headless mutation dispatch, and explicit settings persistence; the observable-list and JavaFX-thread adapters belong only to the inherited GUI component. Recovery-key services live in the neutral engine package. Native configured-vault persistence, mount-provider selection, rename, auto-lock, and lifecycle transitions now use value-oriented settings and vault methods rather than JavaFX properties. `VaultState` is a JavaFX-free atomic state machine; a legacy observable adapter owns frontend-thread event delivery. All persisted per-vault settings now live in synchronized, JavaFX-free `VaultSettingsData`; a dedicated legacy facade preserves the inherited property bindings and autosave behavior without changing the JSON schema. State-derived bindings and exception/statistics observability now live behind lazily constructed legacy facades. The exception value, statistics scheduler, activity timestamp, and transport snapshots are JavaFX-free. `MountServiceSelector` now resolves automatic and per-vault providers using plain settings values, so `Mounter` has no JavaFX dependency; only the inherited options screen requests a live observable adapter. The native-backend reachability audit now records runtime evidence, and the first verified slice removes inherited FXML, CSS/fonts, and GUI images from the staged native release only. Class-level, JavaFX-library, provider, and non-Windows cleanup remains incremental and evidence-gated.

## Keyboard accessibility — implemented foundation

- Standard Tab, Shift+Tab, Enter, Space, and native Windows control behavior remain available.
- The main sidebar supports Up/Down movement and Home/End jumps across fixed destinations and configured vaults without activating a destination merely by focusing it. Add Vault is a persistent primary action. Vault Manager is a permanent destination that first presents a neutral all-vault list without selecting the first vault; deliberate selection opens the existing tools and an explicit configured-vault selector.
- Preferences tabs and Learning Center topics provide explicit arrow-key navigation.
- `/` opens Learning Center search from outside text-entry controls.
- Valid forms provide task-specific Enter behavior, and sensitive embedded workflows deliberately move and restore focus.
- The authoritative control reference and release-test checklist are in [KEYBOARD_CONTROLS.md](KEYBOARD_CONTROLS.md).

## Screen-reader accessibility — implemented foundation

- Dynamic workflow status text uses polite UI Automation live regions so important state changes can be announced without taking focus away from the current task.
- Progress indicators have task-specific accessible names rather than relying on visual context alone.
- Live-region announcements cover engine state, vault creation and opening, password and recovery workflows, Vault Doctor, clipboard results, statistics, and file tools.
- Focus is deliberately placed and restored around embedded workflows so keyboard and screen-reader users remain oriented inside the main workspace.
- A static accessible-name and focus audit now covers fixed and generated controls. Stateful Preferences, Learning Center, FAQ, and Assistant controls announce selection, viewed, expanded, collapsed, and diagnostic-case context where applicable.
- Primary workspaces, creation steps, vault tools, management subflows, FAQ category rebuilding, Assistant result replacement, and completion screens now receive or restore a deliberate keyboard focus target.
- Automated coverage now includes 87 native policy, protocol, persistence, keyboard-navigation, embedded-documentation, backend-identity, profile, preference, and workflow checks. Activity history, application preferences, cached Vault Doctor summaries, Learning Center progress, and window placement have direct file-level coverage. Dashboard locked/unlocked counts cover empty, mixed-case, and unknown vault states.
- A July 27, 2026 hands-on pass confirmed the zero-vault shell with keyboard-only navigation, Narrator names and live Doctor status, larger text, minimum-window layout, High Contrast, and visible focus in Light and Dark.

### Next accessibility milestones

- Repeat the keyboard and Narrator matrix on the frozen release candidate. Development passes have covered zero-, one-, and multi-vault navigation; sensitive connected-vault workflows remain below.
- Complete connected-vault, recovery, password, sharing, file-tool, and removal checks with Narrator.
- Validate additional supported-DPI configurations. The current development build is confirmed at the test operator's normal 160% Windows Text size; cross-monitor reflow remained functional on a television at an unusually large effective scale; and app larger text, minimum-window layout, High Contrast, and both themes also passed.
- Retest the standard Menu-key and `Shift+F10` gestures on release-candidate hardware, but do not depend on them: every vault's visible **More actions** button must remain reachable with `Tab` and operable with `Enter` or `Space`.
- Repeat the static semantic audit against the exact release candidate after all remaining UI changes are frozen.

## Learning Center — implemented in the native shell

VaultKind now includes a **Learning Center** that supports guided, self-paced onboarding.

- The main sidebar opens the Learning Center directly, and `/` provides a keyboard shortcut from anywhere outside an input field.
- Search matches both headings and the detailed text inside each chapter.
- Topic progress is lightweight, local to the device, and can be reset.
- Detailed guidance is progressively disclosed one answer at a time instead of becoming a long manual page.
- Contextual Assistant links open and strongly highlight the exact relevant answer.
- An open answer can be copied or saved as a focused plain-text guide without including vault state or private vault data.
- Completion and selection use text, icons, borders, and contrast rather than color alone.

### VaultKind Assistant — implemented foundation

The optional Assistant inside the Learning Center helps users understand errors and safely troubleshoot common VaultKind problems.

- It explains reviewed diagnostic cases in plain language and recommends clear next steps.
- Search and case filtering are instant, local, and deterministic.
- It never reads or transmits vault contents, passwords, recovery keys, master keys, or decrypted file names.
- Each case links to the strongest matching Learning Center answer.
- Actionable Vault Doctor findings can open their exact Assistant case with the finding and report scope carried forward as local evidence.
- It performs no repair automatically and never depends on an online service.

Current topic set:

- How VaultKind Works
- Your First Vault
- Recovery Keys
- Cloud Storage
- Virtual Drives
- Security Tips
- Keyboard Shortcuts, embedded directly from the authoritative `KEYBOARD_CONTROLS.md` source
- FAQ

### Next Learning Center milestones

- Grow the reviewed diagnostic catalogue when real, reproducible Windows failures are found; do not add speculative cases simply to increase the count.
- Consider optional Windows printing for selected guidance if it adds value beyond the implemented copy and plain-text export actions.
- Keep Learning Center-specific accessibility findings within the shared accessibility verification pass above.
