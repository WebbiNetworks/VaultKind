[CmdletBinding()]
param(
    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string]$Version = "1.0.0",

    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeIdentifier = "win-x64",

    [switch]$SkipEngineBuild
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$identityPath = Join-Path $repositoryRoot "packaging\store-identity.json"
if (-not (Test-Path -LiteralPath $identityPath -PathType Leaf)) {
    throw "The reviewed Microsoft Store identity file is missing: $identityPath"
}

$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
$requiredIdentityValues = [ordered]@{
    productName = $identity.productName
    storeId = $identity.storeId
    packageIdentityName = $identity.packageIdentityName
    packageIdentityPublisher = $identity.packageIdentityPublisher
    publisherDisplayName = $identity.publisherDisplayName
    packageFamilyName = $identity.packageFamilyName
}
foreach ($identityValue in $requiredIdentityValues.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace([string]$identityValue.Value)) {
        throw "The reviewed Microsoft Store identity is missing $($identityValue.Key)."
    }
}
$identityMatches = $identity.productName -ceq "VaultKind" `
    -and $identity.storeId -ceq "9P31PF0927Z4" `
    -and $identity.packageIdentityName -ceq "Webbi.VaultKind" `
    -and $identity.packageIdentityPublisher -ceq "CN=B46E8F20-201E-4AEB-AF2B-B6AB3D44E5FC" `
    -and $identity.publisherDisplayName -ceq "Webbi" `
    -and $identity.packageFamilyName -ceq "Webbi.VaultKind_1014d67w6rsqa"
if (-not $identityMatches) {
    throw "The Microsoft Store identity changed. Review Partner Center before updating the fail-closed build constants."
}

$releaseScript = Join-Path $repositoryRoot "scripts\build-native-release.ps1"
$releaseArguments = @{
    Version = $Version
    RuntimeIdentifier = $RuntimeIdentifier
    PackageName = $identity.packageIdentityName
    PackagePublisher = $identity.packageIdentityPublisher
    PackagePublisherDisplayName = $identity.publisherDisplayName
    PackageProfileId = "Store"
    CreateStoreUpload = $true
}
if ($SkipEngineBuild) {
    $releaseArguments.SkipEngineBuild = $true
}

& $releaseScript @releaseArguments
if ($LASTEXITCODE -ne 0) {
    throw "The VaultKind Microsoft Store upload build failed."
}

$uploadPath = Join-Path $repositoryRoot "artifacts\VaultKind-$Version.0-$RuntimeIdentifier.msixupload"
if (-not (Test-Path -LiteralPath $uploadPath -PathType Leaf)) {
    throw "The Store-upload build did not create the expected artifact: $uploadPath"
}
Write-Host "VaultKind Microsoft Store upload is ready: $uploadPath"
Write-Host "Store ID: $($identity.storeId)"
Write-Host "Microsoft signs this package after certification."
