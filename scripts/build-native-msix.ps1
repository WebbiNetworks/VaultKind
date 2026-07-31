[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BinaryRoot,

    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeIdentifier = "win-x64",

    [ValidatePattern("^\d+\.\d+\.\d+\.\d+$")]
    [string]$Version = "1.0.0.0",

    [ValidatePattern("^[A-Za-z0-9.-]+$")]
    [string]$PackageName = "Webbi.VaultKind",

    [Parameter(Mandatory)]
    [string]$Publisher,

    [string]$PublisherDisplayName = "Webbi",

    [ValidatePattern("^[A-Fa-f0-9]{40}$")]
    [string]$SigningThumbprint,

    [string]$OutputPath,

    [string]$TimestampUrl = "http://timestamp.digicert.com",

    [switch]$DevelopmentPackage,

    [switch]$StoreUpload,

    [ValidatePattern("^[A-Za-z0-9][A-Za-z0-9.-]{0,15}$")]
    [string]$IsolatedProfileId
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$resolvedBinaryRoot = [System.IO.Path]::GetFullPath($BinaryRoot)
$artifactsPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedBinaryRoot.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "MSIX input must be a staged layout beneath $artifactsRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $resolvedBinaryRoot "VaultKind.Windows.exe") -PathType Leaf)) {
    throw "The staged layout is missing VaultKind.Windows.exe: $resolvedBinaryRoot"
}

if ($DevelopmentPackage -and $StoreUpload) {
    throw "DevelopmentPackage and StoreUpload are mutually exclusive."
}

if ($DevelopmentPackage) {
    if ($Publisher -cne "CN=VaultKind Development") {
        throw "Development packages must use Publisher=CN=VaultKind Development."
    }
    if (-not $PackageName.EndsWith(".Development", [System.StringComparison]::Ordinal)) {
        throw "Development package identity names must end with .Development to avoid claiming the production identity."
    }
    if ([string]::IsNullOrWhiteSpace($IsolatedProfileId)) {
        throw "Development packages require IsolatedProfileId so they cannot reuse the permanent VaultKind profile."
    }

    if ([string]::IsNullOrWhiteSpace($SigningThumbprint)) {
        throw "Development packages require SigningThumbprint for local installation."
    }
}
elseif ($StoreUpload) {
    if ($Publisher -ceq "CN=VaultKind Development") {
        throw "Store uploads must use the exact Microsoft-assigned publisher, not the local development publisher."
    }
    if ($PackageName.EndsWith(".Development", [System.StringComparison]::Ordinal)) {
        throw "Store uploads must use the exact Microsoft-assigned production package name."
    }
    if ([string]::IsNullOrWhiteSpace($IsolatedProfileId)) {
        throw "Store uploads require IsolatedProfileId so they cannot reuse the unpackaged VaultKind profile."
    }
    if (-not [string]::IsNullOrWhiteSpace($SigningThumbprint)) {
        throw "Store uploads must remain unsigned; Microsoft signs them after certification."
    }
}
elseif ($Publisher -ceq "CN=VaultKind Development") {
    throw "The locally trusted VaultKind Development certificate cannot create a production package. Use -DevelopmentPackage and a .Development identity for local validation."
}
elseif (-not [string]::IsNullOrWhiteSpace($IsolatedProfileId)) {
    throw "IsolatedProfileId is reserved for explicitly marked development packages."
}
elseif ([string]::IsNullOrWhiteSpace($SigningThumbprint)) {
    throw "Directly installable MSIX packages require SigningThumbprint. Use -StoreUpload for Microsoft Store certification and signing."
}

$normalizedThumbprint = $null
if (-not [string]::IsNullOrWhiteSpace($SigningThumbprint)) {
    $normalizedThumbprint = $SigningThumbprint.Replace(" ", "").ToUpperInvariant()
    $certificate = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Thumbprint -eq $normalizedThumbprint -and $_.HasPrivateKey } |
        Select-Object -First 1
    if ($null -eq $certificate) {
        throw "The requested signing certificate is not available with a private key in Cert:\CurrentUser\My."
    }
    if ($certificate.Subject -cne $Publisher) {
        throw "The manifest publisher must exactly match the signing certificate subject. Certificate: $($certificate.Subject); Publisher: $Publisher"
    }
    if ($certificate.NotBefore -gt [DateTime]::Now -or $certificate.NotAfter -le [DateTime]::Now) {
        throw "The signing certificate is not currently valid."
    }
    $enhancedKeyUsageIds = @($certificate.EnhancedKeyUsageList | ForEach-Object {
        if ($_.ObjectId -is [string]) { $_.ObjectId } else { $_.ObjectId.Value }
    })
    if (-not ($enhancedKeyUsageIds -contains "1.3.6.1.5.5.7.3.3")) {
        throw "The signing certificate is not valid for code signing."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $suffix = if ($DevelopmentPackage) { "-development" } else { "" }
    $extension = if ($StoreUpload) { ".msixupload" } else { ".msix" }
    $OutputPath = Join-Path $artifactsRoot "VaultKind-$Version-$RuntimeIdentifier$suffix$extension"
}
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
if (-not $resolvedOutputPath.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "MSIX output must remain beneath $artifactsRoot"
}
$expectedOutputExtension = if ($StoreUpload) { ".msixupload" } else { ".msix" }
if ([System.IO.Path]::GetExtension($resolvedOutputPath) -cne $expectedOutputExtension) {
    throw "The requested output must use the $expectedOutputExtension extension."
}

$projectPath = Join-Path $repositoryRoot "native\VaultKind.Windows\VaultKind.Windows.csproj"
[xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw
$buildToolsReference = @($projectXml.Project.ItemGroup.PackageReference) |
    Where-Object { $_.Include -eq "Microsoft.Windows.SDK.BuildTools" } |
    Select-Object -First 1
if ($null -eq $buildToolsReference) {
    throw "The native project does not reference Microsoft.Windows.SDK.BuildTools."
}
$packageVersion = [string]$buildToolsReference.Version
$userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$buildToolsRoot = Join-Path $userProfile ".nuget\packages\microsoft.windows.sdk.buildtools\$packageVersion"
$buildToolsProps = Join-Path $buildToolsRoot "build\Microsoft.Windows.SDK.BuildTools.props"
if (-not (Test-Path -LiteralPath $buildToolsProps -PathType Leaf)) {
    throw "Restore the native project so the Windows SDK Build Tools package is available."
}
[xml]$propsXml = Get-Content -LiteralPath $buildToolsProps -Raw
$sdkToolsVersion = [string]$propsXml.Project.PropertyGroup.WindowsSDKBuildToolsVersion
$toolRoot = Join-Path $buildToolsRoot "bin\$sdkToolsVersion\x64"
$makeAppx = Join-Path $toolRoot "MakeAppx.exe"
$signTool = Join-Path $toolRoot "signtool.exe"
foreach ($tool in @($makeAppx)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        throw "Required Windows SDK tool is missing: $tool"
    }
}
if (-not $StoreUpload -and -not (Test-Path -LiteralPath $signTool -PathType Leaf)) {
    throw "Required Windows SDK signing tool is missing: $signTool"
}

$architecture = if ($RuntimeIdentifier -eq "win-arm64") { "arm64" } else { "x64" }
$packageWorkRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot ("msix-staging-" + [Guid]::NewGuid().ToString("N"))))
if (-not $packageWorkRoot.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "MSIX staging must remain beneath $artifactsRoot"
}
$packageContentRoot = Join-Path $packageWorkRoot "content"

try {
    New-Item -ItemType Directory -Path $packageContentRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $resolvedBinaryRoot "*") -Destination $packageContentRoot -Recurse -Force
    Get-ChildItem -LiteralPath $packageContentRoot -Filter "*.pdb" -File -Recurse | Remove-Item -Force

    $packageProfileMarkerPath = Join-Path $packageContentRoot "VaultKind.PackageProfile.json"
    if (Test-Path -LiteralPath $packageProfileMarkerPath) {
        throw "The staged release input unexpectedly contains a package-profile marker. Rebuild the clean stage before packaging."
    }
    if ($DevelopmentPackage -or $StoreUpload) {
        $packageProfileMarker = [ordered]@{
            profileId = $IsolatedProfileId
            packageName = $PackageName
            developmentOnly = [bool]$DevelopmentPackage
        }
        $packageProfileJson = $packageProfileMarker | ConvertTo-Json -Compress
        [System.IO.File]::WriteAllText($packageProfileMarkerPath, $packageProfileJson, [System.Text.UTF8Encoding]::new($false))
    }

    if ($DevelopmentPackage) {
        foreach ($authoredBinaryName in @("VaultKind.Windows.exe", "VaultKind.Windows.dll")) {
            $authoredBinaryPath = Join-Path $packageContentRoot $authoredBinaryName
            if (-not (Test-Path -LiteralPath $authoredBinaryPath -PathType Leaf)) {
                throw "The development package is missing authored binary: $authoredBinaryName"
            }
            & $signTool sign /sha1 $normalizedThumbprint /fd SHA256 $authoredBinaryPath
            if ($LASTEXITCODE -ne 0) { throw "SignTool could not sign development-package binary $authoredBinaryName." }
            & $signTool verify /pa /v $authoredBinaryPath
            if ($LASTEXITCODE -ne 0) { throw "Development-package binary $authoredBinaryName did not pass signature verification." }
        }
    }

    $assetAliases = [ordered]@{
        "SplashScreen.scale-200.png" = "SplashScreen.png"
        "Square150x150Logo.scale-200.png" = "Square150x150Logo.png"
        "Square44x44Logo.scale-200.png" = "Square44x44Logo.png"
        "Wide310x150Logo.scale-200.png" = "Wide310x150Logo.png"
    }
    foreach ($assetAlias in $assetAliases.GetEnumerator()) {
        $assetSource = Join-Path $packageContentRoot "Assets\$($assetAlias.Key)"
        if (-not (Test-Path -LiteralPath $assetSource -PathType Leaf)) {
            throw "The staged layout is missing required package artwork: $($assetAlias.Key)"
        }
        Copy-Item -LiteralPath $assetSource -Destination (Join-Path $packageContentRoot "Assets\$($assetAlias.Value)") -Force
    }

    $manifestTemplate = Get-Content -LiteralPath (Join-Path $repositoryRoot "packaging\AppxManifest.template.xml") -Raw
    $manifest = $manifestTemplate.Replace("__PACKAGE_NAME__", [System.Security.SecurityElement]::Escape($PackageName))
    $manifest = $manifest.Replace("__PUBLISHER__", [System.Security.SecurityElement]::Escape($Publisher))
    $manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", [System.Security.SecurityElement]::Escape($PublisherDisplayName))
    $manifest = $manifest.Replace("__VERSION__", $Version)
    $manifest = $manifest.Replace("__ARCHITECTURE__", $architecture)
    Set-Content -LiteralPath (Join-Path $packageContentRoot "AppxManifest.xml") -Value $manifest -Encoding utf8

    $outputDirectory = Split-Path $resolvedOutputPath -Parent
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $resolvedOutputPath) {
        Remove-Item -LiteralPath $resolvedOutputPath -Force
    }

    $msixPath = if ($StoreUpload) {
        Join-Path $packageWorkRoot "VaultKind-$Version-$RuntimeIdentifier.msix"
    } else {
        $resolvedOutputPath
    }
    & $makeAppx pack /d $packageContentRoot /p $msixPath /o
    if ($LASTEXITCODE -ne 0) { throw "MakeAppx could not create the MSIX package." }

    if ($StoreUpload) {
        $uploadStaging = Join-Path $packageWorkRoot "upload"
        New-Item -ItemType Directory -Path $uploadStaging -Force | Out-Null
        Copy-Item -LiteralPath $msixPath -Destination $uploadStaging
        $uploadZip = Join-Path $packageWorkRoot "VaultKind-$Version-$RuntimeIdentifier.zip"
        Compress-Archive -Path (Join-Path $uploadStaging "*") -DestinationPath $uploadZip -CompressionLevel Optimal
        Move-Item -LiteralPath $uploadZip -Destination $resolvedOutputPath -Force
    }
    else {
        $signArguments = @("sign", "/sha1", $normalizedThumbprint, "/fd", "SHA256")
        if (-not $DevelopmentPackage) {
            if ([string]::IsNullOrWhiteSpace($TimestampUrl)) {
                throw "Production packages require an RFC 3161 timestamp URL."
            }
            $signArguments += @("/td", "SHA256", "/tr", $TimestampUrl)
        }
        $signArguments += $resolvedOutputPath
        & $signTool $signArguments
        if ($LASTEXITCODE -ne 0) { throw "SignTool could not sign the MSIX package." }

        & $signTool verify /pa /v $resolvedOutputPath
        if ($LASTEXITCODE -ne 0) { throw "The signed MSIX package did not pass SignTool verification." }
    }

    $hash = (Get-FileHash -LiteralPath $resolvedOutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$resolvedOutputPath.sha256" -Value "$hash  $([System.IO.Path]::GetFileName($resolvedOutputPath))" -Encoding ascii

    Write-Host $(if ($StoreUpload) { "Unsigned VaultKind Store upload created at $resolvedOutputPath" } else { "Signed VaultKind MSIX created at $resolvedOutputPath" })
    Write-Host "SHA-256: $hash"
    if ($DevelopmentPackage) {
        Write-Warning "This package uses the current-user development identity and is not a release candidate."
    }
    elseif ($StoreUpload) {
        Write-Warning "This upload is intentionally unsigned. Microsoft signs the package after Store certification."
    }
}
finally {
    if (Test-Path -LiteralPath $packageWorkRoot) {
        Remove-Item -LiteralPath $packageWorkRoot -Recurse -Force
    }
}
