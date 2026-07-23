# VaultKind Product Philosophy

## Desktop First. Windows Focused. Privacy Always.

> VaultKind is the best Windows desktop vault experience.

VaultKind is intentionally a Windows desktop product. It does not pursue cross-platform parity, a browser-based experience, or mobile clients. This focus gives the project permission to use Windows-native behavior, terminology, accessibility features, packaging, and system integration whenever they produce a better experience.

The accepted long-term interface direction is documented in [ADR 0001: Move Toward a Native Windows Interface](decisions/0001-native-windows-ui.md). VaultKind will move incrementally toward a WinUI 3 frontend while retaining the proven Java vault engine until a secure, compatible replacement is independently justified.

## Desktop First

- Design complete desktop workflows instead of shrinking web patterns into a desktop window.
- Keep primary actions, settings, diagnostics, and guidance inside the main application workspace.
- Prefer clear navigation, keyboard access, responsive layouts, and useful use of available screen space.
- Treat offline operation as the normal state, not a degraded fallback.

## Windows Focused

- Windows is the only supported operating-system target.
- Optimize for Windows conventions, system themes, title bars, virtual drives, notifications, installers, and accessibility settings.
- Do not weaken the Windows experience merely to preserve visual or behavioral parity with another platform.
- Test releases against supported Windows versions and document Windows-specific requirements plainly.

VaultKind inherits cross-platform code and automation from its upstream foundation. That inherited material may remain while the fork is young, especially where removing it could destabilize security-sensitive code. Its presence does not imply product support. Removal should be deliberate, tested, and separated from cryptographic changes.

### Future Windows-only consolidation

Once the Windows product workflows and security-update process are stable, perform a measured consolidation pass:

- Inventory operating-system abstractions, native libraries, packaging plugins, release workflows, and tests by platform.
- Remove macOS- and Linux-only packaging and runtime dependencies where they are not required by shared security code.
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

## Decision Filter

When evaluating a feature or technical change, ask:

1. Does it improve the Windows desktop experience?
2. Can it work locally and preserve user control?
3. Is it accessible to someone unfamiliar with encryption software?
4. Does it avoid unnecessary change to security-sensitive upstream code?
5. Can its behavior and limitations be explained honestly?

If a proposal primarily serves cross-platform parity, online engagement, telemetry, or a web/mobile expansion, it is outside VaultKind's product direction unless this philosophy is explicitly revised.
