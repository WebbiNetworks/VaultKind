[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$subject = "CN=VaultKind Development"
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $subject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt [DateTime]::UtcNow.AddDays(30)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter ([DateTime]::UtcNow.AddYears(2))
}

$certificateDirectory = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) "VaultKind\DevelopmentSigning"
New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
$publicCertificatePath = Join-Path $certificateDirectory "VaultKind-Development.cer"
Export-Certificate -Cert $certificate -FilePath $publicCertificatePath -Force | Out-Null

foreach ($trustStore in @("Root", "TrustedPublisher")) {
    $trustedCopy = Get-ChildItem "Cert:\CurrentUser\$trustStore" |
        Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } |
        Select-Object -First 1
    if ($null -eq $trustedCopy) {
        Import-Certificate -FilePath $publicCertificatePath -CertStoreLocation "Cert:\CurrentUser\$trustStore" | Out-Null
    }
}

Write-Host "VaultKind development signing certificate is ready."
Write-Host "Thumbprint: $($certificate.Thumbprint)"
Write-Host "Expires: $($certificate.NotAfter.ToString('u'))"
Write-Host "The non-exportable private key remains in the current user's certificate store."
Write-Host "Its public certificate is trusted for the current user only."
