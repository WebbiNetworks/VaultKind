# OpenPGP setup report — October 9, 2026

VaultKind's optional signing identity, public verification page, and detached press-kit signature are operational. Both owner-approved encrypted recovery copies passed independent restoration and signing tests and are locked and detached. The only remaining owner step is to disconnect their physical drives and store them separately.

The owner subsequently requested `Webbi Networks <security@webbi.ca>` as an additional identity on the same key. The update and publication passed: fresh HTTPS downloads confirmed both identities have valid self-certifications, and `Webbi Networks <security@vaultkind.dev>` remains primary. The fingerprint, Ed25519 key, expiry, private-key material, passphrase, and existing press-kit signature are unchanged. An isolated public-only test successfully merged the updated certificate into the original one. The unchanged press-kit signature also verified with the updated published certificate.

| Check | Result |
| --- | --- |
| Installation | Gpg4win 5.1.1 installed after owner approval. Official installer SHA-256 matched; Windows Authenticode was valid and matched the published g10 Code GmbH certificate. |
| Tool operation | GnuPG 2.5.24, Ed25519 support, and component checks passed. The owner confirmed Kleopatra's self-tests passed and the key is visible. |
| Approved identities | Primary: Webbi Networks `<security@vaultkind.dev>`. Additional: Webbi Networks `<security@webbi.ca>`. Same Ed25519 signing/certification key; expires October 8, 2028. Both self-certifications verified. |
| Fingerprint | `0012D24994ABFAD13986A366CDA480EEB7B5EE3B` |
| Private storage | Dedicated access-restricted local profile folder outside the repository and website. The passphrase-protected private export exists only inside the approved encrypted recovery containers. GPG passphrases used Pinentry; container passwords used a local masked prompt. No GPG passphrase, container password, or private-key material was printed or logged. |
| Signing test | A disposable file received an armored detached signature. Verification in a fresh public-only keyring returned exit 0 and the expected `VALIDSIG`. The same test passed with the public key downloaded from the pushed GitHub branch. |
| Tamper test | A modified copy returned exit 1 and `BADSIG` using the original signature. |
| Manual guards | Wrong expected fingerprint rejected; existing-output guard triggered and the original signature remained unchanged. |
| Recovery | Two owner-approved BitLocker VHDX containers hold the protected private export, revocation certificate, public files, and instructions. Each is fully encrypted with XTS-AES256, protection on, and automatic unlock off. Each copy independently restored the expected fingerprint and signed a test that passed public-only verification; the first copy's modified-file test returned `BADSIG`. Temporary restored homes were removed; both containers are locked and detached. Physical disconnection and separate storage remain owner steps. |
| Additional-identity recovery | The original protected private exports contain one user ID. The updated public certificate and `ADDITIONAL-IDENTITY-RECOVERY.txt` were saved and hash-checked beside each locked encrypted container, with references in each adjacent README. After restoring the original private backup, import that public certificate into the same home to recover both user IDs and the primary designation. Public-only merge verification passed. Private-key material and revocation certificates are unchanged. The original private recovery tests remain valid; no new private restoration test is claimed for this public-only identity update. |
| Website publication | The public certificate, full fingerprint, and verification page are live. Public HTTPS byte and fingerprint checks passed before press-kit signing. The press-kit page links the verification instructions and detached signature. |
| Published press-kit signature | The exact approved Ed25519 primary key signed the unchanged archive with SHA-256. Verification using the HTTPS-downloaded website key passed; a modified copy returned exit 1 and `BADSIG`. A final fresh public-only keyring verified independent HTTPS downloads of the archive, public key, and signature. |

The [public key](keys/webbi-networks-vaultkind.asc), [fingerprint](keys/webbi-networks-vaultkind.fingerprint.txt), and [signing, verification, backup, and publication instructions](OPENPGP_SIGNING.md) are ready. These are development-machine tools: no application code, dependencies, packaging, Windows minimum version, or CI signing secrets changed. GPG verification remains optional for users.

GitHub update: [pull request #13](https://github.com/WebbiNetworks/VaultKind/pull/13), branch `codex/openpgp-signing`. The public materials are available on that branch; the pull request remains open. The approved website deployment and detached press-kit signing are complete. The current application release has not changed.

Live resources: [verification instructions](https://vaultkind.dev/verification.html), [public key](https://vaultkind.dev/webbi-networks-vaultkind.asc), [fingerprint](https://vaultkind.dev/webbi-networks-vaultkind.fingerprint.txt), and [press-kit signature](https://vaultkind.dev/press-kit.zip.asc). The 273-byte signature has SHA-256 `9D887D9EDD24788826A0DDA8A03D71BC8AEF1F1FCA40169EDA12CA7EF6293B0A`. The archive remains 5,318,711 bytes with SHA-256 `4120EA4EE88E37228901AC40E23A8D507230C733C6CE7F3EC16C7D1EFD963E93`; its checksum file is byte-identical. The archive was never reuploaded.

The published public certificate containing both identities is 773 bytes, with SHA-256 `976713C1D0EDA9E95FEA1E2D70F296155E211D4DFCC48C8D23A23310A40420DA`. Its two exported self-certifications passed verification. Fresh HTTPS downloads of the updated certificate and verification page matched the staged files, and the local website source is synchronized. Downloads of the archive, existing detached signature, and checksum remain unchanged; the archive verified with the updated certificate without re-signing.

ESET FILEGUARD prevented validation of proposed helper scripts. They were not executed and were removed; the documented workflow uses the installed GnuPG commands directly. No antivirus or Windows protection was disabled.

## Repository and website checks

- Final scoped audit checked 712 tracked/nonignored repository files, 43 website source files, all 11 press-kit archive entries, and the seven publication allowlist files. No private-key, secret-export, revocation, or literal passphrase-assignment indicators were found. Website `remote-backups` and unrelated logs/artifacts were excluded.
- Structural GPG inspection of the public key and detached signature found zero secret-key packets and zero revocation signatures. All five local published source files matched their final staged versions byte-for-byte.
- The setup history scan of VaultKind `HEAD` and `origin/main` covered 768 blob versions and found no indicators.
- Live website checks confirmed the unchanged archive matched its published SHA-256, `4120EA4EE88E37228901AC40E23A8D507230C733C6CE7F3EC16C7D1EFD963E93`.
- Broader local fetched upstream/archive history contains an encrypted historical Cryptomator RSA private-key file, `34C80F11.gpg`, added June 17, 2018 and deleted July 23, 2018. It is not in current VaultKind `main` or `origin/main` ancestry. It was not imported, decrypted, printed, or changed. Therefore the audit does not claim every local Git reference is free of private-key material.

These are bounded, pattern-based checks, not a guarantee about uninspected server paths or text encoded inside PDF/image content. The newly generated Webbi Networks private key was never exported into the repository or website.

Final public HTTPS readback matched all seven published or preserved files, including the key, fingerprint, archive, checksum, signature, verification page, and press-kit page. Static HTML, link, and documented PowerShell syntax checks passed. Browser-based visual QA was unavailable; no visual-review result is claimed.

## Remaining owner step

Disconnect the two selected physical backup drives and keep them in separate secure locations, with recovery credentials stored separately. Locking and detaching the containers does not make connected physical drives offline.

No public-key upload to an external keyserver occurred.
