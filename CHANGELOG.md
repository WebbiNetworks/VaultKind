# Changelog

All notable VaultKind changes will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and VaultKind uses semantic versioning for its public releases.

## [Unreleased]

No changes yet.

## [1.0.2] - 2026-08-05

### Changed

- Updated the public publisher presentation to **Webbi Networks** throughout the app, package metadata, and Microsoft Store listing.
- Restored the approved `— © Webbi Networks —` footer to Preferences > About.
- Made untrimmed .NET publication a project-level invariant so reflection-based local protocol and persistence models remain available in every Release build.

### Distribution

- Microsoft certified, signed, and published version `1.0.2.0` as the current publicly discoverable x64 release.
- Microsoft Store remains the supported source for the signed production package and Store-managed updates.

## [1.0.1] - 2026-08-03

### Fixed

- Corrected the vault-creation completion message when a vault is created without a recovery key.

### Distribution

- Proved the real Microsoft Store update path from `1.0.0.0` to `1.0.1.0` while retaining external encrypted data and configured vault state.

## [1.0.0] - 2026-08-03

### Added

- Native WinUI 3 Windows shell with Dashboard, Vault Manager, Vault Doctor, Activity, Preferences, Learning Center, recovery workflows, and keyboard navigation.
- Microsoft Store MSIX packaging with an isolated Store profile and Microsoft-assigned package identity.
- Windows Explorer WebDAV support for opening vaults as readable drive letters without requiring a third-party driver.
- Optional WinFsp virtual-drive providers when WinFsp is already installed by the user.
- Three optional signature sounds for vault open, vault locked, and consequential warnings.

### Changed

- Product scope is permanently Windows-only and English-only.
- Automatic upstream update checks are disabled; Store distribution is the primary signed update channel.
- Release packaging filters reviewed legacy presentation classes and unused runtime files without modifying vault-format or cryptographic behavior.

### Security

- Vault passwords, recovery keys, decrypted names, vault contents, and local diagnostics remain on the user's computer during core operation.
- Store, development-package, and unpackaged profiles are isolated from one another.
- Package, engine, disposable-vault, WebDAV, uninstall, external-data-retention, and accessibility checkpoints are documented in `docs/RELEASE_READINESS.md`.

### Distribution

- Microsoft certified, signed, and processed VaultKind's first production release.
- Completed acquisition, launch, vault creation, Explorer file I/O, lock, uninstall, reinstall, external-data retention, and supported app-volume relocation testing with the real Store identity.

## Fork baseline

VaultKind is derived from Cryptomator under GPLv3. The current engine baseline was taken from upstream Cryptomator `develop` at commit `193aa1887d6dba090e74a7245a19dc00b9ea0e84` after the 1.19.3 release line. Upstream release history remains available in the [Cryptomator changelog](https://github.com/cryptomator/cryptomator/blob/develop/CHANGELOG.md).
