# OpenPGP setup report — October 9, 2026

VaultKind's optional signing identity is operational. Offline recovery copies still need to be made and tested on owner-selected encrypted media.

| Check | Result |
| --- | --- |
| Installation | Gpg4win 5.1.1 installed after owner approval. Official installer SHA-256 matched; Windows Authenticode was valid and matched the published g10 Code GmbH certificate. |
| Tool operation | GnuPG 2.5.24, Ed25519 support, and component checks passed. The owner confirmed Kleopatra's self-tests passed and the key is visible. |
| Approved identity | Webbi Networks `<security@vaultkind.dev>`; Ed25519 signing/certification; expires October 8, 2028. |
| Fingerprint | `0012D24994ABFAD13986A366CDA480EEB7B5EE3B` |
| Private storage | Dedicated access-restricted local profile folder outside the repository and website. Passphrase entered through Pinentry; no private export or passphrase captured. |
| Signing test | A disposable file received an armored detached signature. Verification in a fresh public-only keyring returned exit 0 and the expected `VALIDSIG`. The same test passed with the public key downloaded from the pushed GitHub branch. |
| Tamper test | A modified copy returned exit 1 and `BADSIG` using the original signature. |
| Manual guards | Wrong expected fingerprint rejected; existing-output guard triggered and the original signature remained unchanged. |
| Recovery | Revocation certificate generated in the private home. Two encrypted offline copies and a supervised restore test remain pending. |

The [public key](keys/webbi-networks-vaultkind.asc), [fingerprint](keys/webbi-networks-vaultkind.fingerprint.txt), and [signing, verification, backup, and publication instructions](OPENPGP_SIGNING.md) are ready. These are development-machine tools: no application code, dependencies, packaging, Windows minimum version, or CI signing secrets changed. GPG verification remains optional for users.

GitHub update: [pull request #13](https://github.com/WebbiNetworks/VaultKind/pull/13), branch `codex/openpgp-signing`. The public materials are available on that branch; merging and website deployment have not occurred.

ESET FILEGUARD prevented validation of proposed helper scripts. They were not executed and were removed; the documented workflow uses the installed GnuPG commands directly. No antivirus or Windows protection was disabled.

## Repository and website checks

- Current tracked/nonignored content checked: 711 files, excluding the three subsequently removed blocked scripts; no private-key or literal passphrase indicators found.
- Current VaultKind `HEAD` and `origin/main` history: 768 blob versions; no indicators found.
- Live website checks covered the home and review pages plus all 11 entries in the linked press-kit archive. No indicators found. The archive matched its published SHA-256, `4120EA4EE88E37228901AC40E23A8D507230C733C6CE7F3EC16C7D1EFD963E93`.
- Broader local fetched upstream/archive history contains an encrypted historical Cryptomator RSA private-key file, `34C80F11.gpg`, added June 17, 2018 and deleted July 23, 2018. It is not in current VaultKind `main` or `origin/main` ancestry. It was not imported, decrypted, printed, or changed. Therefore the audit does not claim every local Git reference is free of private-key material.

These are bounded, pattern-based checks, not a guarantee about uninspected server paths or text encoded inside PDF/image content. The newly generated Webbi Networks private key was never exported into the repository or website.

## Remaining owner actions

1. Select two encrypted offline backup volumes and their separate physical storage locations; perform the documented backup and recovery test.
2. Approve website publication of the public key, full fingerprint, and optional verification instructions when ready.
3. Explicitly authorize any signing or republication of the current release or press kit. Neither was changed during setup.

No public-key upload to an external keyserver occurred.
