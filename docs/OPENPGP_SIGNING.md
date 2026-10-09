# VaultKind OpenPGP signing

Webbi Networks uses a dedicated OpenPGP identity for optional verification of release files, press-kit archives, and manifests. GnuPG and Kleopatra are maintainer tools on Windows 11 Pro. They are not bundled with VaultKind or required to install or run it. VaultKind's existing Windows 10-and-later compatibility, package layout, Microsoft Store signature, and local development Authenticode workflow remain unchanged. An OpenPGP signature does not supply Windows executable trust or remove SmartScreen warnings.

## Identity and current status

The owner approved the following identity and storage arrangement on October 9, 2026:

| Field | Approved value |
| --- | --- |
| Public name | Webbi Networks |
| Public email | security@vaultkind.dev |
| Algorithm and purpose | One Ed25519 primary key with signing and certification capability |
| Created / expires | October 9, 2026 / October 8, 2028; review before expiry |
| Full fingerprint | `0012D24994ABFAD13986A366CDA480EEB7B5EE3B` |
| Private home | `%LOCALAPPDATA%\Webbi Networks\VaultKind Signing\gnupg` |
| Protection | Strong passphrase entered only through secure Pinentry |
| Recovery | Two independent BitLocker-encrypted recovery containers; disconnect and store their drives separately after use |

The private home must remain outside repositories, website trees, build output, and cloud-synchronized folders, with access restricted to the owner's Windows account and necessary system administrators. Never print, log, upload, commit, or transmit private key material. Never put the passphrase in a command argument, script, environment variable, configuration file, or log. Public-key upload to a keyserver requires separate authorization. Security software and Windows protections remain enabled.

Gpg4win 5.1.1 installed successfully with Kleopatra on October 9, 2026. GnuPG reports 2.5.24 with libgcrypt 1.12.4 and Ed25519 support; GnuPG component checks and Kleopatra's version check succeeded. The owner confirmed that all Kleopatra self-tests pass and the intended key is visible. The owner approved and completed local key generation through secure Pinentry. The [public certificate](keys/webbi-networks-vaultkind.asc) and [fingerprint record](keys/webbi-networks-vaultkind.fingerprint.txt) contain the public identity only. The working private key and revocation certificate remain in the dedicated private home; protected recovery copies are held inside the two encrypted backup containers.

The public key, fingerprint, verification page, and authorized detached press-kit signature are live on vaultkind.dev. Independent verification using fresh HTTPS downloads and a public-only keyring passed; a modified copy failed. The press-kit archive and its checksum are unchanged, and the archive was not reuploaded. Both backups passed independent restoration and signing checks and are now locked and detached. Physical disconnection and separate storage of their drives remain owner custody steps. The current application release remains unchanged.

The installer was obtained from the [official Gpg4win download](https://www.gpg4win.org/download.html). Before installation its SHA-256 matched the [official integrity information](https://www.gpg4win.org/package-integrity.html), and Windows Authenticode reported `Valid`, signer `g10 Code GmbH`:

- Installer SHA-256: `ED19C0C89EC42FE32C52A7ABD6E3DA5F491752700546B9EED7DDF82FD303CDDF`
- Authenticode certificate SHA-1 thumbprint: `83CC4E382E5E4AF554C66E429E8F66FFE499910D`
- Certificate serial: `271DF934504F8E383B33BCE5`

Future installations require a fresh authenticity check and owner approval before elevation or system-wide changes. Reconfirm identity and storage before generating a replacement key; never generate a substitute or temporary private key to bypass an approval gate.

## Public identity and offline recovery

The approved key was generated locally in the dedicated private home, using secure Pinentry for the passphrase. Confirm its full fingerprint, UID, Ed25519 algorithm, signing capability, and expiration when inspecting it. The [GnuPG key-management manual](https://www.gnupg.org/documentation/manuals/gnupg/OpenPGP-Key-Management.html) describes generation, expiration changes, and revocation.

Set the following variables in an interactive PowerShell session. Replace the executable path if the verified local GnuPG path differs. The fingerprint is public. This example exports a fresh review copy; use a new public output filename if one already exists.

```powershell
$gpg = 'C:\Program Files\GnuPG\bin\gpg.exe'
$signingHome = Join-Path $env:LOCALAPPDATA 'Webbi Networks\VaultKind Signing\gnupg'
$fingerprint = '0012D24994ABFAD13986A366CDA480EEB7B5EE3B'
$publicKey = Join-Path $PWD 'webbi-networks-vaultkind-public-export.asc'
& $gpg --no-options --homedir $signingHome --fingerprint $fingerprint
if ($LASTEXITCODE -ne 0) { throw 'Fingerprint inspection failed.' }
if (Test-Path -LiteralPath $publicKey) { throw 'Choose a new public export filename.' }
& $gpg --no-options --homedir $signingHome --armor --output $publicKey --export $fingerprint
if ($LASTEXITCODE -ne 0) { throw 'Public-key export failed.' }
```

Record the full fingerprint independently of the downloaded public key. Inspect the export before publication: it must contain only the intended public certificate. The fingerprint and public certificate may be published; private exports and unused revocation certificates may not.

The owner selects the recovery media before any private-key export. Use an encrypted removable volume, or an explicitly approved BitLocker-encrypted VHDX container on the selected drive when encrypting that entire drive is unsuitable. A plain VHDX file provides no encryption by itself: confirm that the mounted volume is fully encrypted, BitLocker protection is on, and automatic unlock is disabled before writing private material. Enter the container password through a local masked prompt; enter the separate GPG passphrase only through secure Pinentry. Neither belongs in commands, saved configuration, or logs.

Export the passphrase-protected secret key **directly to a file inside that unlocked encrypted volume**, using an explicit output destination. Never send export output to the console or stage a private copy in the repository, host drive, Downloads, website, or cloud storage. Include the public certificate, full fingerprint, protected revocation certificate, and restoration instructions. Treat the revocation certificate as sensitive because possession permits revocation without the private-key passphrase. Keep recovery credentials separately from the drives. Do not automate private-key export or backup upload in CI.

The completed backups use two owner-approved `VaultKind-Recovery.vhdx` containers, each with a 512 MB maximum virtual capacity, XTS-AES256 encryption at 100%, protection on, and automatic unlock disabled. The first backup passed restoration before it was locked and detached and its ciphertext was copied to the second drive; the copied container's SHA-256 matched. The second container then passed its own unlock and restoration check while the first remained detached. Both restored keys matched the complete approved fingerprint and signed a disposable file that verified using only the public certificate downloaded from GitHub. A modified test copy failed verification. Temporary restored keyrings were removed, and both containers were locked and detached afterward. The underlying physical drives were not encrypted or disconnected by this process; the owner must disconnect and store them separately to complete offline custody.

For future restoration checks, mount and unlock one container on a trusted machine under owner supervision. Create the temporary restored GnuPG home **inside its encrypted volume**, import the protected backup locally without displaying it, and compare the full fingerprint with the independent published record. Sign a disposable file, verify it through a separate public-only home, and confirm a modified copy fails. Remove only that exact temporary restored home after confirming its path, close all users of the mounted volume, then lock and detach the container. Repeat independently for the other copy. Refresh backups after key changes and repeat these checks; never leave a recovered private key on the unencrypted host drive.

To open Kleopatra against the dedicated signing home in a future session, launch it with a process-scoped `GNUPGHOME`. An ordinary Start Menu launch uses its default home and may appear empty. The following preserves the current PowerShell process's environment, passes the dedicated home to a standalone Kleopatra instance, and restores the prior value afterward. It makes no global environment change.

```powershell
$signingHome = Join-Path $env:LOCALAPPDATA 'Webbi Networks\VaultKind Signing\gnupg'
$kleopatra = 'C:\Program Files\Gpg4win\bin\kleopatra.exe'
$previousGnuPGHome = $env:GNUPGHOME
try {
    $env:GNUPGHOME = $signingHome
    Start-Process -FilePath $kleopatra -ArgumentList '--standalone' -ErrorAction Stop | Out-Null
}
finally {
    $env:GNUPGHOME = $previousGnuPGHome
}
```

## Sign and verify a file

Use native `gpg.exe` interactively, with the explicit executable, private home, and full fingerprint established above. The approved identity is a single Ed25519 primary key with `[SC]` capability. The `!` selector requires that exact primary key; moving to a signing subkey requires a reviewed workflow change. Enter the passphrase only in secure Pinentry. Never supply passphrase arguments or use loopback entry.

Set `$inputFile` to the absolute path of the approved file. This example creates an armored detached signature beside it and refuses an existing output. The signature is not ready for publication until the public-only verification below passes.

```powershell
$inputFile = 'C:\release-staging\example-archive.zip'
$signature = "$inputFile.asc"
if (-not (Test-Path -LiteralPath $inputFile -PathType Leaf)) { throw 'Input file missing.' }
if (Test-Path -LiteralPath $signature) { throw 'Signature already exists; stop and review.' }
& $gpg --no-options --homedir $signingHome --pinentry-mode ask --no-auto-key-retrieve `
    --auto-key-locate clear --local-user "$fingerprint!" --digest-algo SHA256 `
    --armor --output $signature --detach-sign $inputFile
if ($LASTEXITCODE -ne 0) { throw 'Signing failed. Do not publish the output.' }
```

Verification needs only public material. Set `$publicKey` to the absolute path of the downloaded public certificate, `$inputFile` to the file being checked, and `$signature` to its detached signature. Set `$gpg` to a compatible local GnuPG executable. Obtain the expected full fingerprint independently from the official project channels. The following native commands create a fresh public-only home, disable configuration-file and network key discovery, and reject a mismatched fingerprint **before importing**. Only the approved single-primary-key certificate is accepted.

```powershell
$fingerprint = '0012D24994ABFAD13986A366CDA480EEB7B5EE3B'
$verifyHome = Join-Path $env:TEMP ('vaultkind-public-verify-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verifyHome -ErrorAction Stop | Out-Null
$publicOptions = @('--no-options', '--homedir', $verifyHome, '--batch',
    '--no-auto-key-retrieve', '--auto-key-locate', 'clear')
$keyRecords = @(& $gpg @publicOptions --with-colons --fingerprint --show-keys $publicKey)
if ($LASTEXITCODE -ne 0) { throw 'Public-key inspection failed.' }
$primaryRecords = @($keyRecords | Where-Object { $_.StartsWith('pub:') })
$otherKeys = @($keyRecords | Where-Object { $_ -match '^(sec|ssb|sub):' })
$fingerprintRecords = @($keyRecords | Where-Object { $_.StartsWith('fpr:') })
if ($primaryRecords.Count -ne 1 -or $otherKeys.Count -ne 0 -or $fingerprintRecords.Count -ne 1) {
    throw 'Expected exactly one public primary key with no subkeys or secret keys.'
}
$exportedFingerprint = $fingerprintRecords[0].Split(':')[9]
Write-Output "Public certificate fingerprint: $exportedFingerprint"
if ($exportedFingerprint -cne $fingerprint) { throw 'Public-key fingerprint mismatch.' }
& $gpg @publicOptions --import $publicKey
if ($LASTEXITCODE -ne 0) { throw 'Public-key import failed.' }
$verification = @(& $gpg @publicOptions --status-fd 1 --verify $signature $inputFile)
if ($LASTEXITCODE -ne 0) { throw 'Signature verification failed.' }
$validSignatures = @($verification | Where-Object { $_ -match '^\[GNUPG:\] VALIDSIG ' })
$invalidStatus = @($verification | Where-Object {
    $_ -match '^\[GNUPG:\] (BADSIG|ERRSIG|EXPSIG|EXPKEYSIG|REVKEYSIG|FAILURE|ERROR) '
})
if ($invalidStatus.Count -ne 0 -or $validSignatures.Count -ne 1 -or
    ($validSignatures[0] -split ' ')[2] -cne $fingerprint) {
    throw 'Verification did not establish the expected primary signer.'
}
Write-Output "Verified exact file and signer: $fingerprint"
```

The temporary home contains public data only. Close the verification session when finished and remove only that exact temporary directory after confirming its path. A successful GPG exit alone is insufficient: require the full fingerprint and `VALIDSIG` checks above.

Freeze and hash the final file before signing, then verify the signature against those exact bytes. Repeat separately for archives and manifests. A signed checksum manifest authenticates the manifest; users must still compare each downloaded file's hash with its entry. Detached signatures do not modify their input files. Do not rebuild, replace, re-sign for publication, or republish the current release or press kit without explicit authorization.

## Validation before publication

The October 9, 2026 native-GnuPG test imported only the exported public certificate into a fresh public-only home. Its primary fingerprint matched the approved identity. The unchanged test file verified with exit code `0` and `VALIDSIG` for that fingerprint; the modified copy failed with exit code `1` and `BADSIG` against the original signature. The unchanged test file's SHA-256 was `6A23485FBBE3EF7FAFDFBC79F3261D0425AB7E95421D01CCF2D606F1209B63D1`. A separate manual check rejected an incorrect all-`F` fingerprint; the existing-output `Test-Path` guard triggered and the signature's SHA-256 remained unchanged. These are native-command and individual guard checks. Independent restoration and signing checks passed for both encrypted recovery containers as described above; their physical offline custody remains the owner's final storage step.

For subsequent checks, use a fresh disposable directory under ignored `artifacts`. No new test identity is required.

1. Write a harmless test text file. Sign it with the approved identity and verify it using the exported public certificate and independently recorded fingerprint. Expect success.
2. Copy the file and change one byte. Set `$inputFile` to that modified copy while retaining the original `$signature`, then run the verification commands. Expect failure.
3. Verify the original file with an intentionally incorrect full fingerprint. Expect failure even though the file and signature are unchanged.
4. Confirm that attempting to replace an existing signature fails. Preserve the original signature and test file as evidence if useful.
5. Scan the proposed Git diff, tracked filenames, website upload list, and any archive contents for private-key files, GnuPG homes, secret exports, unused revocation certificates, and passphrase assignments. Review only filenames/counts for suspect material; never print the secrets. Ignore rules are a safeguard, not proof of absence.

Record the actual fingerprint, tool versions, pass/fail results, and scan scope. A check that has not run is pending. Documentation changes do not call for rebuilding or republishing an unchanged application.

## Optional verification and website publication

The owner-approved [verification page](https://vaultkind.dev/verification.html), [public certificate](https://vaultkind.dev/webbi-networks-vaultkind.asc), [full fingerprint](https://vaultkind.dev/webbi-networks-vaultkind.fingerprint.txt), and [detached press-kit signature](https://vaultkind.dev/press-kit.zip.asc) are live. The [press-kit page](https://vaultkind.dev/review.html) links the verification instructions and signature. Public HTTPS downloads of the certificate and fingerprint were checked before signing the unchanged archive with the exact approved Ed25519 primary key and SHA-256. The signature verified against that downloaded public certificate; a modified copy returned `BADSIG`. A final independent public-only keyring verified fresh public downloads of the archive, certificate, and signature.

For future authorized publication, publish or confirm the public certificate and fingerprint first, download them through public HTTPS, and check the full fingerprint. Verify each new detached signature using that published certificate before placing it beside the exact file it covers. Retain SHA-256 checksums and the fingerprint published through GitHub. Future artifact signing and publication must remain within the owner's authorization.

Users who choose to verify can use a compatible GnuPG installation and the native verification commands above, supplying the public key and full fingerprint from the official channels. In Kleopatra, inspect the public certificate's full fingerprint before importing it, then verify the downloaded file against its detached signature. An unknown-trust warning does not establish identity; the fingerprint comparison remains necessary. These optional tools are not part of VaultKind's installation or runtime requirements.

Before expiry, review custody and renew the existing key only if it remains trusted, then replace the public certificate and update its expiry information. If the private key is lost or compromised, stop signing, use the protected revocation certificate, and publish the revoked public certificate plus a clear dated notice through the same approved website and GitHub channels. Obtain owner approval for a replacement identity and storage arrangement; do not silently substitute a new fingerprint or publish anything to a keyserver.
