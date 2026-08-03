# Microsoft Store Submission Preparation

This document prepares and records the first production VaultKind submission. Webbi Networks authorized the exact frozen artifact upload and then separately authorized certification submission on August 2, 2026. Microsoft approved Submission 1 certification and processed the direct-link-only publication on August 3, 2026. Partner Center remains the source of truth for the fields it presents at submission time.

## Safety boundary

- Upload only an exact artifact Webbi Networks explicitly approves. Certification submission was separately approved and started on August 2, 2026; Microsoft approved it on August 3. Do not replace, resubmit, or publish without new approval.
- Do not submit an artifact built before the final release-candidate commit.
- Keep the production `.msixupload` unsigned. Microsoft signs an accepted Store package.
- Never upload a locally signed test MSIX or a package containing the development profile marker.
- Use a non-discoverable direct-link listing for the first certified production-identity validation unless Webbi Networks explicitly chooses a public launch instead.
- Do not make the product discoverable without a separate Webbi Networks product decision. The Store-signed production and higher-version update checks are complete.

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
| Supported platform | Windows 10 version 1809 (build 17763) or later, and Windows 11 desktop |
| Language | English only |

The checked-in identity source is `packaging/store-identity.json`. The build script must reject any mismatch.

## Artifact boundary

The August 2 frozen-candidate upload built from clean commit `bdf44083a1dee926f94488a3191e1350b8aff91c` is:

- `artifacts/VaultKind-1.0.0.0-win-x64.msixupload`
- Size: `96,467,931` bytes
- SHA-256: `A45FB1E1296391B935EB962DD6ADDA7A74E8304380F65847A6D72875BEAF5BDF`
- Sole inner package: `VaultKind-1.0.0.0-win-x64.msix`
- Inner size: `97,191,517` bytes
- Inner SHA-256: `8D82E6DDD24CC37667E29805BD69C546451DAF0E21067C220143C6E688CF27D6`
- Exact Store identity, version `1.0.0.0`, x64 architecture, `Store`/`developmentOnly: false` marker, and English release manifest
- All 796 staged files present byte-for-byte in the 800-entry package, with no unexpected payload files
- Only Logback 1.6.1; all eight package-artwork hashes match the reviewed sources
- Unsigned as required; no `AppxSignature.p7x`
- WACK `10.0.26100.7705`: complete 24-test run, overall PASS, `PARTIAL_RUN=FALSE`; 22 direct passes and the same two documented optional analyzer findings

Webbi Networks explicitly approved this exact SHA-256 artifact for a non-discoverable draft. It was uploaded to Partner Center Submission 1 (`1152921505701563238`) on August 2, 2026 and Microsoft reported the package as **Validated** and **Complete**. It remains unsigned locally and unmodified. Do not rebuild, replace, or upload another artifact unless a later source, dependency, native-interface, or visible-asset change invalidates it. Never modify this upload for local installation.

The submitted configuration uses all worldwide markets, public audience, direct-link-only non-discoverability, and a CAD base price of zero (**Free**). Pricing and availability, Properties, Age ratings, Packages, Store listings, and Submission options all reported **Complete** before submission. Microsoft approved Submission 1 certification and processed publication on August 3, 2026. The listing remains **Available, but not discoverable in the Microsoft Store — Direct link only**. The five reviewed screenshots and approved 300x300 Store tile remain in the English listing, and the certification instructions contain no credentials.

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
- Private contact: `https://vaultkind.dev/contact.php`
- Public source: `https://github.com/WebbiNetworks/VaultKind`

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

Microsoft requires at least one desktop screenshot and recommends at least four. Desktop screenshots must be PNG, landscape or portrait, at least 1366x768, and no larger than 50 MB. VaultKind's five reviewed images are immutable repository files at 1936x1048 and under 250 KB each:

| Order | File | Suggested caption |
| --- | --- | --- |
| 1 | `docs/store-listing-media/01-dashboard.png` | See vault status, recent activity, Vault Doctor results, and learning progress in one Windows workspace. |
| 2 | `docs/store-listing-media/02-vault-doctor.png` | Run private, read-only Vault Doctor checks and review clear results without uploading vault data. |
| 3 | `docs/store-listing-media/03-learning-center.png` | Learn the everyday encrypted-vault workflow through built-in plain-language guidance. |
| 4 | `docs/store-listing-media/04-virtual-drives.png` | Understand readable virtual drives, encrypted storage, opening, locking, and drive availability. |
| 5 | `docs/store-listing-media/05-faq.png` | Search practical answers about vault access, storage, privacy, and troubleshooting. |

The files contain no browser chrome, mouse pointers, test secrets, stale storage paths, or development-only diagnostics. They intentionally show only the disposable `MooseTaxes` vault. Before upload, compare every image with the frozen runtime binary and record that no visible interface changed. The approved optional 300x300 Store tile is `docs/store-listing-media/vaultkind-store-tile-300.png`; do not substitute the website social card for Store identity artwork.

## Restricted capability justification

The package declares `runFullTrust`.

Suggested Partner Center explanation:

> VaultKind is a Windows desktop encryption application. Full trust is required to start its bundled local Java vault engine as a child process and to present an unlocked vault through Windows Explorer. VaultKind does not install a driver, Windows service, or startup task. The bundled engine communicates with the WinUI shell through a local, user-scoped socket and processes vault data on the user’s device. The supported Store path uses the Microsoft-provided Windows Explorer WebDAV integration; WinFsp integrations are optional only when separately installed by the user.

## Notes for certification

The following certification notes were saved before submission:

> Submission date: August 2, 2026
>
> VaultKind is an English-only x64 Windows desktop application for creating and using Cryptomator-compatible encrypted vaults. No VaultKind account or internet connection is required for normal vault operations, and there are no test credentials.
>
> Basic test path: launch VaultKind; choose Add Vault; create a disposable vault in a new temporary folder; set a disposable password and retain it for the test; open Preferences > Virtual Drive and select WebDAV (Windows Explorer); unlock the disposable vault; use the reveal/open action to access the readable drive in Windows Explorer; create and read a disposable text file; return to VaultKind and lock the vault. The drive should disappear after locking. The encrypted test folder may then be deleted.
>
> The package includes a bundled Java vault engine that VaultKind starts as a local child process. This accounts for the required process-launch behavior reported by static analysis. The application installs no Windows driver or NT service. Its Store-supported readable-drive path uses Microsoft’s Windows Explorer WebDAV integration. WinFsp provider entries are optional enhancements only when WinFsp is already installed; VaultKind does not install it.
>
> A complete local Windows App Certification Kit run passed overall. Optional analyzer output included metadata from Microsoft Windows App SDK/WebView components and process-launch/string matches in required Microsoft, Java, icon, and engine files. The application’s child process is the bundled local encryption engine and is necessary for the product to function.
>
> Vault data, passwords, and recovery keys remain local. Privacy policy: https://vaultkind.dev/privacy.html . Support: https://vaultkind.dev/support.html .

## Metadata consistency audit

The prepared identity matches `packaging/store-identity.json`. The manifest template independently confirms the `VaultKind` display name, x64-substituted package model, US-English resource declaration, full-trust desktop entry point, and `Windows.Desktop` minimum version `10.0.17763.0`. The native project uses the same minimum Windows version and limits authored satellite resources to `en-US`. The listing therefore states Windows 10 version 1809 or later rather than the less precise Windows 10 wording. Certification notes say **internet connection** rather than **network service**, because the local engine uses loopback communication and the Windows Explorer WebDAV path can involve Windows' own WebClient service even though no external VaultKind service is required.

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
- Website, privacy, support, private contact-form, and public-source URLs return HTTPS 200 (reverified August 2, 2026), and a benign end-to-end support request reached the private destination inbox.
- Screenshots match the frozen release build.
- Signature sounds disabled workflow confirmed understandable and operable.
- Keyboard-only, Narrator, Windows text-size, high-DPI, and minimum-window passes complete on the frozen build.
- Exact approved production artifact uploaded; Partner Center package validation is Complete.
- Pricing and availability saved as worldwide, Free, public audience, and non-discoverable/direct-link only.
- Properties, Age ratings, Store listings, and Submission options are Complete; reviewed media and certification instructions are saved.
- The publishing hold remained in effect through certification and was released only after the saved direct-link-only visibility was reconfirmed and Webbi Networks explicitly authorized **Publish now**.
- Webbi Networks reviewed the complete draft and explicitly approved certification submission as a separate external action; Partner Center accepted it on August 2, 2026.
- Microsoft approved Submission 1 certification and processed publication on August 3, 2026.
- Partner Center reconfirmed public audience with direct-link-only non-discoverability; Webbi Networks explicitly approved **Publish now**, and the listing remains direct-link only.
- The Microsoft-signed production package was acquired and launched successfully. Its Store profile was isolated from unpackaged/development profiles, as designed.
- A disposable external vault at `G:\VK-Store-Validation` passed exact-folder creation without unwanted nesting, unlock, Windows Explorer WebDAV mounting, readable-file write/read persistence, lock, and unmount.
- Clean close/relaunch preserved the Store-profile registration. Uninstall removed package-local registration but preserved the external encrypted folder; clean reinstall began with an empty Store profile, then reconnect, unlock, persisted-file read, and relock all passed.

## Post-certification production-identity test

The production `1.0.0.0` package has passed acquisition, launch, exact-folder vault creation, unlock, readable-drive file I/O, lock/unmount, clean close/relaunch, uninstall/reinstall, external encrypted-data preservation, reconnect, persisted-file recovery, and supported app-volume relocation from C: to `G:\WindowsApps`. Windows retained the C: package path as a junction to the active G: package directory. After relocation, VaultKind launched from its registered Store identity, retained the connected disposable vault, unlocked it, exposed both persisted files through Windows Explorer, and locked/unmounted cleanly. The external disposable vault remains intact and must not be deleted without separate approval.

The real-product Store update check is complete. Submission 2 (`1152921505701570639`) delivered version `1.0.1.0` while retaining public-audience, direct-link-only non-discoverability. Microsoft Store offered the installed `1.0.0.0` product an **Update**, installed the certified replacement, and returned the action to **Open**. VaultKind launched from `C:\Program Files\WindowsApps\Webbi.VaultKind_1.0.1.0_x64__1014d67w6rsqa\VaultKind.Windows.exe`, reported a healthy Dashboard and connected engine, retained the locked external `G:\VK-Store-Validation` registration, and closed normally. No Store-specific validation gate remains open.

A post-`1.0.0.0` wording correction distinguishes the recovery warning shown when a new vault has no recovery key. Certified Store version `1.0.1.0` now delivers that correction.

The final `1.0.1.0` update packages that correction separately from the frozen production upload and was rebuilt from clean commit `1a70883a6ad3e1fa2a268e14eab83c0e7820b5b7`. Its `.msixupload` is 96,467,930 bytes with SHA-256 `74709134765F23456785433ACB8F9F910C2D48875CA8A45746CA6A7B3E8DB9B3`; the sole 97,191,689-byte inner MSIX has SHA-256 `1E0B2AD5D4890B0FB1B5EBACB51D28193B855264DD019B70D1B944B653C438D6`. The package declares `Webbi.VaultKind`, the assigned Microsoft publisher, version `1.0.1.0`, x64, and the non-development Store profile. It was intentionally unsigned for Microsoft certification. Payload comparison found all 796 staged files unchanged and no missing or unexpected payload; compiled-string inspection found both recovery-warning branches; the eight approved package assets match their source hashes; and only Logback 1.6.1 is present. All 87 native checks pass. WACK completed a full 24-test run against this exact inner package with overall `PASS`, `PARTIAL_RUN="FALSE"`, 22 direct passes, and only the same two optional analyzer findings already documented for `1.0.0.0`. Webbi Networks separately approved the exact artifact upload, Submission 2 certification, and publication. Microsoft certified, signed, processed, and delivered it through the existing direct-link-only listing.

## Official process references

- [Microsoft Store submission overview](https://learn.microsoft.com/en-us/windows/apps/publish/get-started)
- [MSIX Store listing fields](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info)
- [MSIX privacy and support information](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info)
- [MSIX submission options and certification notes](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/manage-submission-options)
