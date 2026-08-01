# Microsoft Store Submission Preparation

This document prepares the first production VaultKind submission without authorizing an upload, certification submission, or publication. Partner Center remains the source of truth for the fields it presents at submission time.

## Safety boundary

- Do not upload or submit until Greg gives explicit approval for that external action.
- Do not submit an artifact built before the final release-candidate commit.
- Keep the production `.msixupload` unsigned. Microsoft signs an accepted Store package.
- Never upload a locally signed test MSIX or a package containing the development profile marker.
- Use a non-discoverable direct-link listing for the first certified production-identity validation unless Greg explicitly chooses a public launch instead.
- Do not make the product discoverable until the Store-signed acquisition, update, uninstall/reinstall, and external-vault retention matrix passes.

## Product identity

| Field | Required value |
| --- | --- |
| Product name | VaultKind |
| Store ID | `9P31PF0927Z4` |
| Package identity | `Webbi.VaultKind` |
| Publisher | `CN=B46E8F20-201E-4AEB-AF2B-B6AB3D44E5FC` |
| Publisher display name | Webbi |
| Package family | `Webbi.VaultKind_1014d67w6rsqa` |
| Architecture | x64 |
| Initial package version | `1.0.0.0` |
| Supported platform | Windows 10 and Windows 11 desktop |
| Language | English only |

The checked-in identity source is `packaging/store-identity.json`. The build script must reject any mismatch.

## Artifact boundary

The July 31 locally validated reference upload is:

- `artifacts/VaultKind-1.0.0.0-win-x64.msixupload`
- Size: `96,469,603` bytes
- SHA-256: `0E4F25149DA6B04B5A9CF42045EB926E5B8834920D2AB20EE25071DE8CF983F7`
- One unsigned x64 inner MSIX; no `AppxSignature.p7x`
- WACK `10.0.26100.7705`: complete 24-test run, overall PASS, not partial
- Real-identity local install, packaged engine launch, Windows Explorer WebDAV file I/O, lock, cleanup, and exact-name uninstall: passed

This is evidence, not the final upload. Dependency or source changes after that build—including the Jackson 2.21.5 maintenance update—require a fresh artifact from the frozen release commit, a new recorded hash, package inspection, WACK, and the applicable runtime checks.

## Recommended Partner Center choices

These are the conservative first-submission defaults. Confirm every displayed field before saving.

- Pricing: **Free**.
- Availability: all intended Windows markets, with an English-only listing and support statement.
- Visibility: **Available, but not discoverable in the Store; direct link only** for the first production-identity certification and retention test.
- Publishing: publish after certification only when the selected non-discoverable visibility is confirmed. Do not combine public discoverability with the initial proof.
- Category: **Security** if offered; otherwise **Utilities & tools**. Do not invent a category not shown by Partner Center.
- Secondary category: none unless a clearly accurate choice is useful.
- Trial, add-ons, in-app purchases, subscriptions, ads, and commerce: none.
- Product declarations: no generative AI, user-generated content, gambling, controlled substances, or other regulated content.
- Age rating: answer the IARC questionnaire factually. VaultKind contains no violence, sexual content, gambling, drugs, social interaction, or user-generated online content.
- Personal information question: answer **Yes** conservatively because VaultKind can access user-selected files locally and declares full trust; provide the privacy URL even though VaultKind does not transmit readable vault data.

## Public URLs

These URLs must return HTTPS 200 publicly before the submission is saved:

- Website: `https://vaultkind.dev/`
- Privacy policy: `https://vaultkind.dev/privacy.html`
- Support: `https://vaultkind.dev/support.html`
- Public source: `https://github.com/WebbiNetworks/VaultKind`
- Support email: `webbi@webbi.ca`

## English Store listing

### Product name

VaultKind

### Short description

An approachable Windows workspace for creating, opening, learning about, and checking Cryptomator-compatible encrypted vaults—locally and without an account.

### Description

VaultKind makes encrypted vaults feel at home on Windows.

Create, connect, unlock, and manage Cryptomator-compatible vaults from one focused Windows workspace. VaultKind keeps vault operations local, presents important security decisions in plain English, and includes built-in guidance for people who do not want to become encryption experts.

Vault Doctor provides offline, read-only health checks with clear explanations and guided next steps. The Learning Center explains passwords, recovery keys, cloud synchronization, virtual drives, backups, and keyboard controls inside the application.

Your readable vault files, vault password, and recovery key are not sent to VaultKind. You choose where encrypted vault data is stored, including local folders, external storage, and folders synchronized by a cloud provider.

VaultKind 1.0 is an English-only, x64 Windows desktop application. It does not require a VaultKind account, advertising, analytics, or a recurring subscription.

Important: encryption does not replace backups. Keep verified backups and retain your password and recovery information safely. VaultKind cannot reconstruct a forgotten password or recovery key.

### Product features

Enter each as a separate feature without a bullet character:

1. Native Windows workspace
2. Cryptomator-compatible vault format
3. Create and connect encrypted vaults
4. Windows Explorer virtual-drive access
5. Offline Vault Doctor health checks
6. Plain-English guidance and warnings
7. Built-in searchable Learning Center
8. Keyboard and accessibility support
9. Local operation with no VaultKind account
10. Optional signature sounds for open, lock, and warning events

### What’s new

Leave blank for the first submission. Use this field only for later updates.

### Search terms

Use only fields actually offered by Partner Center. Suitable terms are `encrypted vault`, `Cryptomator`, `file encryption`, `Windows security`, and `vault manager`. Do not use competitor names other than the factual compatibility reference.

## Listing media

Provide at least four current desktop screenshots without browser chrome, mouse pointers, test secrets, stale paths, or development-only diagnostics. The current source candidates are:

1. `G:\Projects\CODEX\sites\vaultkind.dev\assets\vaultkind-dashboard-v4.png`
2. `G:\Projects\CODEX\sites\vaultkind.dev\assets\vaultkind-doctor-v4.png`
3. `G:\Projects\CODEX\sites\vaultkind.dev\assets\vaultkind-workflow-v4.png`
4. `G:\Projects\CODEX\sites\vaultkind.dev\assets\vaultkind-faq-v4.png`
5. `G:\Projects\CODEX\sites\vaultkind.dev\assets\vaultkind-virtual-drives-v4.png`

Before upload, visually confirm that every image matches the frozen release candidate and contains only disposable vault names and paths. Use the approved VK icon artwork for Store identity assets; do not substitute the website social card for a required Store logo.

## Restricted capability justification

The package declares `runFullTrust`.

Suggested Partner Center explanation:

> VaultKind is a Windows desktop encryption application. Full trust is required to start its bundled local Java vault engine as a child process and to present an unlocked vault through Windows Explorer. VaultKind does not install a driver, Windows service, or startup task. The bundled engine communicates with the WinUI shell through a local, user-scoped socket and processes vault data on the user’s device. The supported Store path uses the Microsoft-provided Windows Explorer WebDAV integration; WinFsp integrations are optional only when separately installed by the user.

## Notes for certification

Use concise notes and update the date before submission:

> Submission date: [DATE]
>
> VaultKind is an English-only x64 Windows desktop application for creating and using Cryptomator-compatible encrypted vaults. No account or network service is required, and there are no test credentials.
>
> Basic test path: launch VaultKind; choose Add Vault; create a disposable vault in a new temporary folder; set a disposable password and retain it for the test; open Preferences > Virtual Drive and select WebDAV (Windows Explorer); unlock the disposable vault; use the reveal/open action to access the readable drive in Windows Explorer; create and read a disposable text file; return to VaultKind and lock the vault. The drive should disappear after locking. The encrypted test folder may then be deleted.
>
> The package includes a bundled Java vault engine that VaultKind starts as a local child process. This accounts for the required process-launch behavior reported by static analysis. The application installs no Windows driver or NT service. Its Store-supported readable-drive path uses Microsoft’s Windows Explorer WebDAV integration. WinFsp provider entries are optional enhancements only when WinFsp is already installed; VaultKind does not install it.
>
> A complete local Windows App Certification Kit run passed overall. Optional analyzer output included metadata from Microsoft Windows App SDK/WebView components and process-launch/string matches in required Microsoft, Java, icon, and engine files. The application’s child process is the bundled local encryption engine and is necessary for the product to function.
>
> Vault data, passwords, and recovery keys remain local. Privacy policy: https://vaultkind.dev/privacy.html . Support: https://vaultkind.dev/support.html .

## Final pre-upload evidence

- Frozen release commit hash recorded.
- Clean tracked worktree and expected ignored-artifact inventory recorded.
- Java tests pass from the frozen commit.
- Native tests pass from the frozen commit.
- Exact Release shortcut build succeeds and authored binaries carry only the local development signature used for workstation testing.
- Final unsigned Store upload rebuilt from the frozen commit.
- Upload and inner-MSIX SHA-256 hashes recorded.
- Identity, publisher, version, architecture, marker, payload, notices, and icon dimensions re-inspected.
- `AppxSignature.p7x` absent from the upload payload.
- WACK complete and not partial; all required tests pass.
- Separately signed local-test copy passes launch, disposable Windows Explorer WebDAV I/O, lock, cleanup, and uninstall.
- Temporary certificate, package, package data, sockets, mounts, and test vault are absent afterward.
- Production upload hash is unchanged after local testing.
- Privacy and support URLs return HTTPS 200.
- Screenshots match the frozen release build.
- Signature sounds disabled workflow confirmed understandable and operable.
- Keyboard-only, Narrator, Windows text-size, high-DPI, and minimum-window passes complete on the frozen build.
- Greg reviews the complete Partner Center preview and explicitly approves upload/submission.

## Post-certification production-identity test

Keep the first listing non-discoverable while validating the Store-signed package. Use only a new disposable external vault. Confirm acquisition, launch, create, unlock, readable-drive file I/O, lock, update to a higher package version, app-volume relocation where supported, uninstall, reinstall, and preservation of the encrypted vault outside package data. Only after this matrix passes should a later submission make VaultKind publicly discoverable.

## Official process references

- [Microsoft Store submission overview](https://learn.microsoft.com/en-us/windows/apps/publish/get-started)
- [MSIX Store listing fields](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info)
- [MSIX privacy and support information](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info)
- [MSIX submission options and certification notes](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/manage-submission-options)
