# VaultKind Store Path Test

This isolated WinUI 3 app proves VaultKind's manual MSIX packaging and Microsoft Store submission path before VaultKind release engineering continues.

The Partner Center product name was reserved on July 26, 2026. Its public Store identity is recorded in `store-identity.json`.

Build the unsigned Store upload package from the VaultKind repository root:

```powershell
powershell -ExecutionPolicy Bypass -File experiments\store-path-proof\build-package.ps1 `
  -PackageName Webbi.VaultKindStorePathTest `
  -Publisher "CN=B46E8F20-201E-4AEB-AF2B-B6AB3D44E5FC" `
  -PublisherDisplayName Webbi
```

Microsoft Store replaces any existing signature and signs the package after certification. Do not use the VaultKind development certificate for the Store upload.

## Submission status

Submission 1 (`1152921505701495721`) was sent to Microsoft Store certification on July 27, 2026 and entered the Store on July 28, 2026.

- Product: **VaultKind Store Path Test**
- Store ID: `9NKQ43JXQMHN`
- Package identity: `Webbi.VaultKindStorePathTest`
- Package family: `Webbi.VaultKindStorePathTest_1014d67w6rsqa`
- Package: `StorePathProof_1.0.0.0_x64.msixupload`
- Package validation: complete
- IARC rating: Everyone / 3+
- Availability: free, worldwide, direct-link-only, and non-discoverable
- Certification result: passed; Partner Center reports **In Microsoft Store**
- Store acquisition and installation: passed on July 28, 2026
- App-volume relocation: passed; package content runs from the protected `G:\WindowsApps` volume
- First launch: passed; the registered `App` identity opened the expected responsive WinUI window
- Update test: pending a higher-version submission
- Uninstall and reinstall test: pending

The submission includes the required `runFullTrust` explanation for a conventional packaged WinUI 3 desktop executable. The test app does not request elevation, launch external processes, install services or drivers, access the network, or collect data.

The proof now covers submission, certification, Store signing, acquisition, installation, supported app-volume relocation, and first launch. Publish a higher-version proof package and record Store update behavior, then test uninstall and reinstall before treating the distribution experiment as complete. The proof app does not establish that VaultKind's bundled Java engine or virtual-drive providers satisfy Store packaging and policy constraints; validate those seams separately before submitting VaultKind.
