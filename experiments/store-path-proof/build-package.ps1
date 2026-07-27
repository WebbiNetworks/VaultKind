[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern("^[A-Za-z0-9.-]+$")]
    [string]$PackageName,

    [Parameter(Mandatory)]
    [string]$Publisher,

    [string]$PublisherDisplayName = "Greg Tritton",

    [ValidatePattern("^\d+\.\d+\.\d+\.\d+$")]
    [string]$Version = "1.0.0.0",

    [ValidatePattern("^[A-Fa-f0-9]{40}$")]
    [string]$SigningThumbprint
)

$ErrorActionPreference = "Stop"
$experimentRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $experimentRoot "..\.."))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\store-path-proof"))
$contentRoot = Join-Path $artifactsRoot "content"
$project = Join-Path $experimentRoot "StorePathProof.csproj"

if (Test-Path -LiteralPath $artifactsRoot) {
    Remove-Item -LiteralPath $artifactsRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $contentRoot -Force | Out-Null

& dotnet publish $project -c Release --no-restore -r win-x64 "-p:PublishDir=$contentRoot\" -p:PublishReadyToRun=false -p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) { throw "The Store path proof publish failed." }

$releaseOutput = Join-Path $experimentRoot "bin\Release\net10.0-windows10.0.26100.0\win-x64"
foreach ($compiledResource in @("App.xbf", "MainWindow.xbf", "StorePathProof.pri")) {
    $resourcePath = Join-Path $releaseOutput $compiledResource
    if (-not (Test-Path -LiteralPath $resourcePath -PathType Leaf)) {
        throw "The compiled WinUI output is missing $compiledResource."
    }
    Copy-Item -LiteralPath $resourcePath -Destination (Join-Path $contentRoot $compiledResource) -Force
}

$assetsSource = Join-Path $releaseOutput "Assets"
$assetsTarget = Join-Path $contentRoot "Assets"
New-Item -ItemType Directory -Path $assetsTarget -Force | Out-Null
Copy-Item -Path (Join-Path $assetsSource "*") -Destination $assetsTarget -Recurse -Force
$assetAliases = [ordered]@{
    "SplashScreen.scale-200.png" = "SplashScreen.png"
    "Square150x150Logo.scale-200.png" = "Square150x150Logo.png"
    "Square44x44Logo.scale-200.png" = "Square44x44Logo.png"
    "Wide310x150Logo.scale-200.png" = "Wide310x150Logo.png"
}
foreach ($assetAlias in $assetAliases.GetEnumerator()) {
    Copy-Item -LiteralPath (Join-Path $assetsTarget $assetAlias.Key) -Destination (Join-Path $assetsTarget $assetAlias.Value) -Force
}

$template = Get-Content -LiteralPath (Join-Path $experimentRoot "AppxManifest.template.xml") -Raw
$manifest = $template.Replace("__PACKAGE_NAME__", [System.Security.SecurityElement]::Escape($PackageName))
$manifest = $manifest.Replace("__PUBLISHER__", [System.Security.SecurityElement]::Escape($Publisher))
$manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", [System.Security.SecurityElement]::Escape($PublisherDisplayName))
$manifest = $manifest.Replace("__VERSION__", $Version)
Set-Content -LiteralPath (Join-Path $contentRoot "AppxManifest.xml") -Value $manifest -Encoding utf8

[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$buildToolsReference = @($projectXml.Project.ItemGroup.PackageReference) |
    Where-Object { $_.Include -eq "Microsoft.Windows.SDK.BuildTools" } |
    Select-Object -First 1
$buildToolsVersion = [string]$buildToolsReference.Version
$userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$buildToolsRoot = Join-Path $userProfile ".nuget\packages\microsoft.windows.sdk.buildtools\$buildToolsVersion"
[xml]$propsXml = Get-Content -LiteralPath (Join-Path $buildToolsRoot "build\Microsoft.Windows.SDK.BuildTools.props") -Raw
$sdkToolsVersion = [string]$propsXml.Project.PropertyGroup.WindowsSDKBuildToolsVersion
$toolRoot = Join-Path $buildToolsRoot "bin\$sdkToolsVersion\x64"
$makeAppx = Join-Path $toolRoot "MakeAppx.exe"
$signTool = Join-Path $toolRoot "signtool.exe"

$msixPath = Join-Path $artifactsRoot "StorePathProof_$($Version)_x64.msix"
& $makeAppx pack /d $contentRoot /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx could not create the Store path proof package." }

if (-not [string]::IsNullOrWhiteSpace($SigningThumbprint)) {
    & $signTool sign /sha1 $SigningThumbprint /fd SHA256 $msixPath
    if ($LASTEXITCODE -ne 0) { throw "SignTool could not sign the local proof package." }
    & $signTool verify /pa $msixPath
    if ($LASTEXITCODE -ne 0) { throw "The local proof package signature could not be verified." }
}

Write-Host "MSIX: $msixPath"
$uploadStaging = Join-Path $artifactsRoot "upload"
New-Item -ItemType Directory -Path $uploadStaging -Force | Out-Null
Copy-Item -LiteralPath $msixPath -Destination $uploadStaging
$uploadZip = Join-Path $artifactsRoot "StorePathProof_$($Version)_x64.zip"
$uploadPath = Join-Path $artifactsRoot "StorePathProof_$($Version)_x64.msixupload"
Compress-Archive -Path (Join-Path $uploadStaging "*") -DestinationPath $uploadZip -CompressionLevel Optimal
Move-Item -LiteralPath $uploadZip -Destination $uploadPath
Remove-Item -LiteralPath $uploadStaging -Recurse -Force

Write-Host "Store upload: $uploadPath"
Write-Host "SHA-256: $((Get-FileHash -LiteralPath $uploadPath -Algorithm SHA256).Hash.ToLowerInvariant())"
