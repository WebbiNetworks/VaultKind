# External link policy

VaultKind must not send users to Cryptomator-owned websites, support channels, release feeds, or issue trackers as though they were VaultKind services.

## Approved product destinations

- Product, help, and documentation: <https://vaultkind.dev>
- Source and issue tracking: <https://github.com/WebbiNetworks/VaultKind>

Until VaultKind operates a signed release feed, automatic update checks are intentionally disabled. Releases must never be sourced from an upstream Cryptomator update channel.

## Intentional exceptions

- GPL, copyright, and upstream attribution links.
- Source-code comments that preserve the provenance of an implementation or workaround.
- Third-party dependency and standards links used by the build.
- The server URL embedded in an existing Cryptomator Hub vault. This is user data and remains supported for vault compatibility; VaultKind does not advertise or redirect new users to Cryptomator Hub.

## Regression protection

`ExternalLinkAuditTest` scans runtime Java, FXML, property, and CSS files for upstream product links. Add a destination to `VaultKindUrls` rather than introducing literal product URLs in controllers.

Repository and packaging metadata should use `vaultkind.dev`, `WebbiNetworks/VaultKind`, or `${{ github.repository }}`. Upstream release-publishing integrations remain disabled until VaultKind owns the corresponding infrastructure and signing credentials.
