# Public Repository Readiness

This document defines the source-publication boundary for VaultKind. It complements, but does not replace, product release validation. A public repository is not a production release and does not authorize submitting an artifact to Microsoft.

## Published source boundary

The public repository contains the Windows-native shell, the retained Java vault engine source, tests, build and audit scripts, English resources, licensing, security reporting guidance, and current engineering documentation.

The following material is deliberately excluded:

- IDE workspace metadata and user-specific settings.
- Generated build, package, signing, and diagnostic artifacts.
- Private keys, certificates, environment files, credentials, and local handoff notes.
- The completed Store-path proof experiment and its one-off submission screenshot.
- Inherited macOS and Linux distribution files and build profiles.
- Inherited release automation that referenced upstream bots, external signing systems, unsupported platforms, or unrelated publication channels.

Inherited cross-platform source may remain when it supports shared security behavior, compatibility, tests, or reviewable upstream merges. Its presence does not make that platform a supported VaultKind target. Native release staging uses fail-closed, reviewed filters documented in [NATIVE_BACKEND_REACHABILITY.md](NATIVE_BACKEND_REACHABILITY.md).

## Public automation

Public CI must use least privilege, require no repository secrets, and run only the supported Windows test boundary. Third-party actions must be official and pinned to complete commit hashes. Production Store packages are built and submitted through the separately documented release process; CI must not invent or reuse a public signing identity.

## Publication checks

Before publishing or replacing public history:

1. Confirm the worktree contains no credential material, private key, certificate, machine-specific user path, generated package, or local handoff file.
2. Run `git diff --check`, parse the Maven model and PowerShell scripts, and run both the Maven and native Windows regression suites.
3. Rebuild and sign the exact local Release shortcut target, then confirm the normal profile, configured vault registrations, provider inventory, launch, and close behavior.
4. Create and verify an offline Git bundle containing the complete pre-public history before rewriting any remote reference.
5. Publish one clean root history on `main`; remove obsolete development and automated-update branches so deleted private-era material is not reachable through another public reference.
6. Read back the remote heads and tags and repeat the current-tree secret and generated-artifact checks.

The offline history bundle is an owner-controlled recovery artifact. It must never be committed, uploaded as a release asset, or placed in the public repository.

The August 1, 2026 cleanup pass removed 132 obsolete tracked files and replaced inherited multi-platform/release automation with one secret-free Windows test workflow. Current-tree scans found no private key, certificate, generated package, executable, DLL, user-specific Windows path, or credential assignment. Credential-like test vectors remain only where they are deliberate public test fixtures. The complete automated and signed-shortcut checkpoint passed before history publication.

## Source and release obligations

VaultKind is distributed under GPLv3. Each released binary must include the applicable license and notices, and its corresponding public source must be identifiable by an immutable VaultKind version tag. A tag is created only for the exact tested release candidate; cleanup or pre-release snapshots are not presented as stable releases.

## Remaining product gates

Repository publication does not close the release gates in [RELEASE_READINESS.md](RELEASE_READINESS.md). The live privacy-policy and support destinations, Store listing media, frozen-release accessibility pass, final package validation, and explicit production-submission authorization remain separate requirements.
