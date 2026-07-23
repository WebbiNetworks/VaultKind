# VaultKind UX Roadmap

This file records agreed future UX ideas that are intentionally outside the current implementation milestone.

All roadmap work follows the product direction in [PRODUCT_PHILOSOPHY.md](PRODUCT_PHILOSOPHY.md): **Desktop First. Windows Focused. Privacy Always.** Windows is the sole supported platform, and cross-platform parity is not a roadmap objective.

## Learning Center

Evolve the current Help area into a **Learning Center** that supports guided, self-paced onboarding.

- Rename the Help sidebar heading from **Learn** to **Learning Center**.
- Add a search field inside the Learning Center so users can quickly find guidance by topic or terminology.
- Show a completion checkmark beside each topic after the user opens it, treating that topic as read or learned.
- Keep progress lightweight and local to the device; it should guide users without blocking access to any topic.
- Provide a way to revisit completed topics and, if useful, reset learning progress.
- Preserve accessible contrast and ensure completion is communicated by text or state as well as color.

### VaultKind Assistant — Troubleshooter

Add an optional assistant inside the Learning Center that helps users understand errors and safely troubleshoot common VaultKind problems.

- Explain errors in plain language and recommend clear next steps.
- Use the current screen, vault state, and locally available diagnostic information only with explicit user consent.
- Never read or transmit vault contents, passwords, recovery keys, master keys, or decrypted file names.
- Keep troubleshooting entirely local and deterministic.
- Present only reviewed product guidance and link to the relevant Learning Center topic.
- Require confirmation before performing any action that changes vault settings, files, or system configuration.
- Remain optional and never depend on an online service.

Current topic set:

- How VaultKind Works
- Creating Your First Vault
- Recovery Keys
- Cloud Storage
- Virtual Drives
- Security Tips
- Frequently Asked Questions
