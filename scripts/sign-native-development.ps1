[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BinaryRoot
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedBinaryRoot = [System.IO.Path]::GetFullPath($BinaryRoot)
$allowedRoots = @(
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "native\VaultKind.Windows\bin")),
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
)

$isAllowed = $false
foreach ($allowedRoot in $allowedRoots) {
    $requiredPrefix = $allowedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($resolvedBinaryRoot.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $isAllowed = $true
        break
    }
}

if (-not $isAllowed) {
    throw "Development signing is limited to VaultKind build outputs under native\VaultKind.Windows\bin or artifacts."
}
if (-not (Test-Path -LiteralPath $resolvedBinaryRoot -PathType Container)) {
    throw "Build output directory does not exist: $resolvedBinaryRoot"
}

$subject = "CN=VaultKind Development"
$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $subject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt [DateTime]::UtcNow
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1
if ($null -eq $certificate) {
    throw "Run scripts\setup-native-development-signing.ps1 first."
}

$targets = @("VaultKind.Windows.exe", "VaultKind.Windows.dll") |
    ForEach-Object { Join-Path $resolvedBinaryRoot $_ }
foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        throw "VaultKind build output is missing: $target"
    }

    $signature = Set-AuthenticodeSignature -FilePath $target -Certificate $certificate -HashAlgorithm SHA256
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Signing did not produce a valid signature for $target. Status: $($signature.Status)"
    }
    Write-Host "Signed $target"
}
