# VaultKind UX Roadmap

This file records the current native-shell UX state and the agreed ideas that remain outside the current implementation milestone.

All roadmap work follows the product direction in [PRODUCT_PHILOSOPHY.md](PRODUCT_PHILOSOPHY.md): **Desktop First. Windows Focused. Privacy Always.** Windows is the sole supported platform, and cross-platform parity is not a roadmap objective.

VaultKind 1.0.0 is intentionally English-only. Localization is outside the 1.0 roadmap and must not be reintroduced piecemeal; a later localization effort requires complete translation and security review.

## Keyboard accessibility — implemented foundation

- Standard Tab, Shift+Tab, Enter, Space, and native Windows control behavior remain available.
- The main sidebar supports Up/Down movement and Home/End jumps across fixed destinations and configured vaults without activating a destination merely by focusing it.
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
- Automated coverage now includes 32 native policy, keyboard-navigation, embedded-documentation, backend-identity, profile, and preference checks.
- A July 27, 2026 hands-on pass confirmed the zero-vault shell with keyboard-only navigation, Narrator names and live Doctor status, larger text, minimum-window layout, High Contrast, and visible focus in Light and Dark.

### Next accessibility milestones

- Repeat the keyboard and Narrator matrix with one and many configured disposable vaults; the zero-vault shell is confirmed.
- Complete connected-vault, recovery, password, sharing, file-tool, and removal checks with Narrator.
- Validate additional supported-DPI configurations. The current development build is confirmed at Greg's normal 160% Windows Text size; cross-monitor reflow remained functional on a television at an unusually large effective scale; and app larger text, minimum-window layout, High Contrast, and both themes also passed.
- Verify context-menu access with the Menu key and Shift+F10 before declaring the keyboard release gate complete.
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
