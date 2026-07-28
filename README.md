# VaultKind

**Desktop First. Windows Focused. Privacy Always.**

VaultKind is a Windows desktop encryption application focused on accessibility, modern usability, and transparent security. Its product promise is simple: **VaultKind is the best Windows desktop vault experience.** It is currently in private development and is not ready for production use or public distribution.

VaultKind is derived from [Cryptomator](https://github.com/cryptomator/cryptomator) under the GNU General Public License v3. It is not affiliated with or endorsed by Skymatic GmbH or the Cryptomator project.

## Project direction

- Windows is the only supported target; cross-platform parity is not a product goal
- Desktop-native workflows take priority over web, mobile, and platform-neutral compromises
- Dark, Light, and System themes included for everyone
- Clear first-run onboarding and approachable security language
- Accessible contrast, scalable layouts, and keyboard-friendly interaction
- Permanently English-only interface with no translation or language-selection roadmap
- Compatibility with Cryptomator vaults
- Security-sensitive code kept close to upstream to make review and updates safer
- No VaultKind updater until a dedicated, signed release channel exists

The product philosophy and its engineering decision filter are documented in [PRODUCT_PHILOSOPHY.md](docs/PRODUCT_PHILOSOPHY.md). The implemented keyboard model and its release-test checklist are documented in [KEYBOARD_CONTROLS.md](docs/KEYBOARD_CONTROLS.md). Package names and core cryptographic components retain their upstream identifiers for now; changing them would add risk without improving the user experience.

## Development status

This repository is private while the product identity, security update process, packaging, and release model are established. Do not use development builds as the only way to access important data.

## Building

### Requirements

- JDK 26 (for example Eclipse Temurin or Azul Zulu)
- .NET 10 SDK with the Windows App SDK workload used by the native project

### Build and test

```shell
./mvnw clean install
```

The assembled modules and platform-specific dependencies are written beneath `target`.

Build the native Windows frontend and run its package-free policy checks with:

```powershell
dotnet build native\VaultKind.Windows\VaultKind.Windows.csproj -c Debug --no-restore
dotnet run --project native\VaultKind.Windows.Tests\VaultKind.Windows.Tests.csproj -c Release
```

Release staging and distribution requirements are documented in [RELEASE_READINESS.md](docs/RELEASE_READINESS.md). An isolated WinUI 3 package has now passed Microsoft Store certification and completed Store-signed installation and first launch, proving the distribution path before VaultKind is adapted to it. An unsigned portable Windows archive remains the fallback, with the associated Windows warning and policy limitations documented plainly.

## Upstream and licensing

See [FORK_NOTICE.md](FORK_NOTICE.md) for the fork relationship and compatibility policy. The complete GPLv3 terms are in [LICENSE.txt](LICENSE.txt).

Cryptomator copyright © 2016–2026 Skymatic GmbH and contributors. Subsequent VaultKind modifications are copyright their respective contributors.
