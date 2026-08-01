# VaultKind Product Philosophy

## Desktop First. Windows Focused. Privacy Always.

> VaultKind is the best Windows desktop vault experience.

VaultKind is intentionally a Windows desktop product. It does not pursue cross-platform parity, a browser-based experience, or mobile clients. This focus gives the project permission to use Windows-native behavior, terminology, accessibility features, packaging, and system integration whenever they produce a better experience.

The accepted long-term interface direction is documented in [ADR 0001: Move Toward a Native Windows Interface](decisions/0001-native-windows-ui.md). VaultKind will move incrementally toward a WinUI 3 frontend while retaining the proven Java vault engine until a secure, compatible replacement is independently justified.

The cross-surface public interface is governed by the [VaultKind Shell Specification](VAULTKIND_SHELL_SPECIFICATION.md): **One shell. One identity.** The Windows application is the reference implementation, and VaultKind Web, branded documentation, future update experiences, diagnostics, and any future assistant reuse its language and design system without simulating capabilities they do not provide.

## Desktop First

- Design complete desktop workflows instead of shrinking web patterns into a desktop window.
- Keep primary actions, settings, diagnostics, and guidance inside the main application workspace.
- Prefer clear navigation, keyboard access, responsive layouts, and useful use of available screen space.
- Treat offline operation as the normal state, not a degraded fallback.

### VaultKind UI Law #1: The main window is the workspace

All routine tasks remain inside the VaultKind shell. The user should never lose context because of an unnecessary popup window.

Popup dialogs are reserved for operating-system functions that cannot reasonably be embedded, including:

- Windows file and folder pickers.
- Windows authentication or permission prompts.
- Critical confirmations that must interrupt the current action to prevent harm or data loss.

Application pages, vault management, settings, guidance, diagnostics, progress, results, and recoverable errors belong inside the main window. Before introducing a popup, the design must establish why an embedded workspace view cannot serve the user safely and clearly.

### VaultKind UI Law #2: Useful depth, progressively disclosed

VaultKind should provide useful, task-focused detail without becoming a manual that users must read from beginning to end. Pages lead with concise choices and plain-language summaries, then reveal deeper guidance only when the user asks for it.

- Search includes detailed guidance, not only headings or landing-page summaries.
- Contextual links open the exact relevant subsection, not merely the correct chapter.
- Guidance pages present compact questions or tasks and expand one detailed answer at a time.
- Safe actions, risks, and important “do not” guidance appear where they are relevant.
- Deeper guidance remains inside the main workspace and preserves the user's context.

### VaultKind UI Law #3: The shell is the brand

Every public-facing VaultKind experience uses the same recognizable shell language: persistent navigation, a contextual workspace, shared cards and actions, approved typography and spacing, honest status, and calm plain-language guidance. Each surface names itself clearly and exposes only real capabilities. Consistency teaches the product; simulation undermines trust.

## Windows Focused

- Windows is the only supported operating-system target.
- Optimize for Windows conventions, system themes, title bars, virtual drives, notifications, installers, and accessibility settings.
- Do not weaken the Windows experience merely to preserve visual or behavioral parity with another platform.
- Test releases against supported Windows versions and document Windows-specific requirements plainly.

### English-only product

VaultKind uses one reviewed English interface. It does not include a display-language selector, runtime translation machinery, right-to-left layout preference, or partially translated safety guidance.

Security, recovery, deletion, integrity, and error messages must preserve exact meaning. An incomplete or ambiguous translation can change the user's understanding of risk, so VaultKind does not plan multilingual support. English-only is a permanent product boundary unless this philosophy is deliberately replaced.

New interface text is authored and reviewed in English. Centralized English string catalogs may remain for wording consistency, but translation bundles, language preferences, selectors, and localization services must not be added.

VaultKind inherits cross-platform source abstractions from its upstream foundation. Some inherited source remains where removing it could destabilize security-sensitive behavior or make upstream security updates harder to review. Its presence does not imply product support. Removal remains deliberate, tested, and separated from cryptographic changes.

### Windows-only consolidation

Legacy macOS and Linux distribution files, build profiles, and release automation have been removed. Source-level consolidation continues incrementally:

- Inventory operating-system abstractions, native libraries, packaging plugins, release workflows, and tests by platform.
- Remove platform-specific runtime dependencies where they are not required by shared security code.
- Simplify platform-selection branches only after Windows behavior has equivalent test coverage.
- Compare application size, dependency count, startup time, memory use, and build duration before and after each removal.
- Keep cleanup commits separate from cryptographic or vault-format changes so upstream security updates remain reviewable.
- Retain a component when its removal would create more maintenance or merge risk than measurable product benefit.

The goal is a smaller, clearer Windows codebase—not deletion for its own sake.

## Privacy Always

- Encryption, passwords, recovery keys, decrypted names, and vault contents remain local.
- Troubleshooting guidance is deterministic and offline by default.
- No diagnostic information leaves the device without a specific, informed action by the user.
- Online accounts, telemetry, cloud services, and remote assistance must never become requirements for core vault use.
- Privacy claims must describe verifiable product behavior, not marketing language.

## Experience Principles

- Accessibility is a core feature, including low-glare themes, strong contrast, scalable layouts, and visible interaction states.
- Explain security concepts in plain language without hiding important consequences.
- Keep users oriented with contextual headers, persistent navigation, progress, and clear recovery paths.
- Prefer calm confidence over fear-based security messaging.
- Preserve compatibility with supported Cryptomator vault formats while keeping security-sensitive changes reviewable against upstream.

### Three signature sounds, used sparingly

VaultKind has exactly three sound identities:

- **Vault Open** acknowledges a successful transition from locked encrypted storage to the readable Windows drive.
- **Vault Locked** acknowledges a successful transition back to the protected locked state.
- **Warning** calls attention to a confirmed caution or danger that could affect vault access, integrity, or a consequential user decision. A caution may use a brief, quieter presentation of the same warning identity; critical danger uses the unmistakable full warning.

Sound reinforces state and safety; it does not decorate the interface. Navigation, hover, ordinary button presses, routine validation, informational messages, and general success feedback remain silent. A red or amber visual treatment alone does not justify audio. Every new warning trigger must identify the concrete risk and show why visual feedback is insufficient.

All sounds are brief, local, user-controllable, and optional. Audio failure must never delay, interrupt, or change a vault operation. VaultKind must remain fully understandable and operable with signature sounds disabled.

## Decision Filter

When evaluating a feature or technical change, ask:

1. Does it improve the Windows desktop experience?
2. Can it work locally and preserve user control?
3. Is it accessible to someone unfamiliar with encryption software?
4. Does it avoid unnecessary change to security-sensitive upstream code?
5. Can its behavior and limitations be explained honestly?
6. If it adds sound, does it fit one of the three signature identities and communicate a meaningful state or safety event?

If a proposal primarily serves cross-platform parity, online engagement, telemetry, or a web/mobile expansion, it is outside VaultKind's product direction unless this philosophy is explicitly revised.
