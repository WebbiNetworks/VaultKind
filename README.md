# VaultKind

**Encryption designed for people.**

VaultKind is a community-driven desktop encryption application focused on accessibility, modern usability, and transparent security. It is currently in private development and is not ready for production use or public distribution.

VaultKind is derived from [Cryptomator](https://github.com/cryptomator/cryptomator) under the GNU General Public License v3. It is not affiliated with or endorsed by Skymatic GmbH or the Cryptomator project.

## Project direction

- Dark, Light, and System themes included for everyone
- Clear first-run onboarding and approachable security language
- Accessible contrast, scalable layouts, and keyboard-friendly interaction
- Compatibility with Cryptomator vaults
- Security-sensitive code kept close to upstream to make review and updates safer
- No VaultKind updater until a dedicated, signed release channel exists

The first development phase deliberately concentrates on the desktop interface. Package names and core cryptographic components retain their upstream identifiers for now; changing them would add risk without improving the user experience.

## Development status

This repository is private while the product identity, security update process, packaging, and release model are established. Do not use development builds as the only way to access important data.

## Building

### One-click Windows development launcher

Double-click `Launch-VaultKind-Dev.cmd` in the repository root. It compiles changed files, starts VaultKind with an isolated development profile under `target/ui-dev-profile`, and leaves an installed Cryptomator profile untouched.

### Requirements

- JDK 26 (for example Eclipse Temurin or Azul Zulu)

### Build and test

```shell
./mvnw clean install
```

The assembled modules and platform-specific dependencies are written beneath `target`.

## Upstream and licensing

See [FORK_NOTICE.md](FORK_NOTICE.md) for the fork relationship and compatibility policy. The complete GPLv3 terms are in [LICENSE.txt](LICENSE.txt).

Cryptomator copyright © 2016–2026 Skymatic GmbH and contributors. Subsequent VaultKind modifications are copyright their respective contributors.
