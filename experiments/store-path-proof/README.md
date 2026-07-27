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

Submission 1 (`1152921505701495721`) was sent to Microsoft Store certification on July 27, 2026.

- Product: **VaultKind Store Path Test**
- Store ID: `9NKQ43JXQMHN`
- Package identity: `Webbi.VaultKindStorePathTest`
- Package: `StorePathProof_1.0.0.0_x64.msixupload`
- Package validation: complete
- IARC rating: Everyone / 3+
- Availability: free, worldwide, direct-link-only, and non-discoverable
- Certification state at handoff: pre-processing, step 2 of 4

The submission includes the required `runFullTrust` explanation for a conventional packaged WinUI 3 desktop executable. The test app does not request elevation, launch external processes, install services or drivers, access the network, or collect data.

Do not treat submission as final proof. Record the certification result, Store-signed installation result, launch result, update behavior, and uninstall behavior before applying this packaging model to VaultKind.
