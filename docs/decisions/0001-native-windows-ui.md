# ADR 0001: Move Toward a Native Windows Interface

- **Status:** Accepted
- **Date:** 2026-07-23

## Context

VaultKind is intentionally a Windows desktop product. Its current JavaFX interface was inherited from Cryptomator and remains useful as a working application and UX prototype, but it carries cross-platform windowing assumptions that conflict with VaultKind's Windows-focused direction.

Recent work on title bars, multi-monitor movement, embedded navigation, responsive layouts, and native window behavior has required fragile JavaFX-specific workarounds. Continuing to invest heavily in those workarounds would preserve a cross-platform UI architecture that VaultKind no longer intends to support.

At the same time, the inherited Java vault and encryption implementation is mature, security-sensitive, and compatible with Cryptomator vaults. Rewriting that core merely to change UI technology would create unacceptable compatibility, security, and maintenance risk.

## Decision

VaultKind's long-term UI destination is a native Windows frontend built with **C# and WinUI 3/XAML**.

The existing Java implementation will initially remain responsible for vault-format handling, cryptography, mounting, and other proven security-sensitive operations. The native frontend will communicate with that backend through a narrowly defined, authenticated local interface.

The intended architecture is:

```text
WinUI 3 / C# frontend
          |
          | private local IPC
          v
Existing Java vault engine
          |
          v
Cryptomator-compatible vaults
```

This is an incremental migration, not an immediate rewrite.

## Non-negotiable boundaries

- Do not reimplement cryptographic algorithms or the vault format simply to remove Java.
- Preserve compatibility with supported Cryptomator vault formats unless a separately reviewed decision explicitly changes that goal.
- Keep passwords, recovery keys, decrypted names, vault contents, and diagnostic evidence local.
- Design the frontend/backend boundary so secrets are exposed only to the component that genuinely needs them and only for the shortest practical time.
- Keep security-sensitive upstream changes reviewable and separable from UI migration work.
- Maintain GPLv3 obligations and attribution for Cryptomator-derived components.

## Migration approach

1. Continue using the JavaFX build as the working product and UX specification while the principal workflows are established.
2. Create a small native Windows proof of concept containing the real VaultKind window, Dashboard, navigation, theme, and accessibility foundations.
3. Define a versioned local backend contract and prove one complete vault lifecycle: list, unlock, mount, lock.
4. Validate failure handling, process recovery, IPC permissions, secret handling, and Cryptomator compatibility before expanding the native frontend.
5. Migrate confirmed screens and workflows incrementally, keeping the JavaFX application usable during the transition.
6. Replace the JavaFX frontend only after the native application reaches functional parity for supported Windows workflows.
7. Remove obsolete cross-platform UI and packaging dependencies deliberately, with measurements and regression coverage.

## Why WinUI 3

- Native Windows windowing, snapping, DPI scaling, input, and multi-monitor behavior.
- Modern XAML controls and Fluent styling suitable for VaultKind's visual direction.
- Stronger integration with Windows accessibility, high-contrast settings, notifications, taskbar behavior, and system dialogs.
- A better architectural match for the product philosophy: **Desktop First. Windows Focused. Privacy Always.**

## Consequences

The migration requires a second frontend codebase and a carefully designed process boundary. Distribution must account for the Windows App SDK and the retained Java backend runtime. Development effort will temporarily increase while both interfaces coexist.

In return, VaultKind can stop spending disproportionate effort imitating Windows behavior through JavaFX and can build its long-term interface directly on the platform it exclusively supports.

## Revisit criteria

Revisit this decision only if the native proof of concept cannot securely operate the existing vault engine, introduces unacceptable deployment complexity, or fails to produce a material improvement in Windows usability and maintainability.
