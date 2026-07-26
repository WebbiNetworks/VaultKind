# VaultKind UX Roadmap

This file records the current native-shell UX state and the agreed ideas that remain outside the current implementation milestone.

All roadmap work follows the product direction in [PRODUCT_PHILOSOPHY.md](PRODUCT_PHILOSOPHY.md): **Desktop First. Windows Focused. Privacy Always.** Windows is the sole supported platform, and cross-platform parity is not a roadmap objective.

VaultKind 1.0.0 is intentionally English-only. Localization is outside the 1.0 roadmap and must not be reintroduced piecemeal; a later localization effort requires complete translation and security review.

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
- FAQ

### Next Learning Center milestones

- Grow the reviewed diagnostic catalogue when real, reproducible Windows failures are found; do not add speculative cases simply to increase the count.
- Consider optional Windows printing for selected guidance if it adds value beyond the implemented copy and plain-text export actions.
- Continue accessibility review at minimum supported window sizes and increased Windows text scaling.
