[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeIdentifier = "win-x64",

    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string]$Version = "1.0.0",

    [string]$SigningThumbprint,

    [string]$PackagePublisher,

    [ValidatePattern("^[A-Za-z0-9.-]+$")]
    [string]$PackageName = "WebbiNetworks.VaultKind",

    [switch]$CreateMsix,

    [switch]$CreatePortableArchive,

    [switch]$SkipEngineBuild
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$stageRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "VaultKind-$Version-$RuntimeIdentifier"))
$requiredPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $stageRoot.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release staging must remain inside $artifactsRoot"
}

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stageRoot | Out-Null

$mavenWrapper = Join-Path $repositoryRoot "mvnw.cmd"
if (-not $SkipEngineBuild) {
    & $mavenWrapper -B -DskipTests package
    if ($LASTEXITCODE -ne 0) { throw "The Java engine build failed." }
}

$classpathFile = Join-Path $repositoryRoot "target\native-release-classpath.txt"
& $mavenWrapper -B dependency:build-classpath "-DincludeScope=runtime" "-Dmdep.outputFile=$classpathFile" "-Dmdep.regenerateFile=true"
if ($LASTEXITCODE -ne 0) { throw "Maven could not resolve the release runtime classpath." }

$forbiddenReleaseLibraries = @(
    "apiguardian-api-*.jar",
    "byte-buddy-*.jar",
    "hamcrest-*.jar",
    "javafx-swing-*.jar",
    "jimfs-*.jar",
    "junit-*.jar",
    "mockito-*.jar",
    "objenesis-*.jar",
    "opentest4j-*.jar"
)
$releaseClasspathEntries = (Get-Content -LiteralPath $classpathFile -Raw).Trim().Split([System.IO.Path]::PathSeparator, [System.StringSplitOptions]::RemoveEmptyEntries)
$unexpectedTestLibraries = @($releaseClasspathEntries | Where-Object {
    $fileName = [System.IO.Path]::GetFileName($_)
    $forbiddenReleaseLibraries | Where-Object { $fileName -like $_ }
})
if ($unexpectedTestLibraries.Count -gt 0) {
    $unexpectedNames = $unexpectedTestLibraries | ForEach-Object { [System.IO.Path]::GetFileName($_) }
    throw "The production classpath contains test-only libraries: $($unexpectedNames -join ', ')"
}

$javaHome = [Environment]::GetEnvironmentVariable("JAVA_HOME")
$jlink = if ([string]::IsNullOrWhiteSpace($javaHome)) { $null } else { Join-Path $javaHome "bin\jlink.exe" }
if ([string]::IsNullOrWhiteSpace($jlink) -or -not (Test-Path -LiteralPath $jlink)) {
    throw "JAVA_HOME must point to the reviewed JDK used to build the VaultKind release runtime."
}

$project = Join-Path $repositoryRoot "native\VaultKind.Windows\VaultKind.Windows.csproj"
& dotnet publish $project -c Release --no-restore -r $RuntimeIdentifier "-p:PublishDir=$stageRoot\" -p:PublishReadyToRun=false -p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) { throw "The native Windows publish failed." }

# The unpackaged WinUI publish target does not copy the app's compiled XAML and
# PRI resources into a custom PublishDir. Without them Microsoft.UI.Xaml fails
# during startup, even though the managed and Windows App SDK binaries exist.
$releaseOutputRoot = Join-Path (Split-Path $project -Parent) "bin\Release"
$compiledResourceSource = Get-ChildItem -LiteralPath $releaseOutputRoot -Filter "VaultKind.Windows.pri" -File -Recurse |
    Where-Object {
        $_.FullName -like "*\$RuntimeIdentifier\*" -and
        $_.DirectoryName -notlike "*\AppX*"
    } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $compiledResourceSource) {
    throw "The native Release output is missing VaultKind.Windows.pri for $RuntimeIdentifier."
}

foreach ($compiledResource in @("App.xbf", "MainPage.xbf", "MainWindow.xbf", "VaultKind.Windows.pri")) {
    $resourcePath = Join-Path $compiledResourceSource.DirectoryName $compiledResource
    if (-not (Test-Path -LiteralPath $resourcePath -PathType Leaf)) {
        throw "The native Release output is missing the compiled WinUI resource $compiledResource."
    }
    Copy-Item -LiteralPath $resourcePath -Destination (Join-Path $stageRoot $compiledResource) -Force
}

$compiledAssetsSource = Join-Path $compiledResourceSource.DirectoryName "Assets"
if (-not (Test-Path -LiteralPath (Join-Path $compiledAssetsSource "StoreLogo.png") -PathType Leaf)) {
    throw "The native Release output is missing its compiled Assets directory."
}
$compiledAssetsTarget = Join-Path $stageRoot "Assets"
New-Item -ItemType Directory -Path $compiledAssetsTarget -Force | Out-Null
Copy-Item -Path (Join-Path $compiledAssetsSource "*") -Destination $compiledAssetsTarget -Recurse -Force

foreach ($directory in Get-ChildItem -LiteralPath $stageRoot -Directory) {
    try {
        $culture = [System.Globalization.CultureInfo]::GetCultureInfo($directory.Name)
        if ($culture.Name -and -not $culture.Name.Equals("en-US", [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $directory.FullName -Recurse -Force
        }
    }
    catch [System.Globalization.CultureNotFoundException] {
        # Non-culture directories contain application/runtime assets and remain untouched.
    }
}

$engineRoot = Join-Path $stageRoot "Engine"
$classesTarget = Join-Path $engineRoot "classes"
$librariesTarget = Join-Path $engineRoot "lib"
$runtimeTarget = Join-Path $engineRoot "runtime"
New-Item -ItemType Directory -Path $classesTarget, $librariesTarget | Out-Null

$classesSource = Join-Path $repositoryRoot "target\classes"
if (-not (Test-Path -LiteralPath (Join-Path $classesSource "logback-native.xml"))) {
    throw "The engine classes are incomplete; logback-native.xml is missing."
}

# The native frontend never loads the inherited JavaFX FXML, CSS, fonts, or
# images. Keep every compiled engine class for now because Dagger, service
# loading, and reflection make class-level trimming a separate audited step.
Copy-Item -LiteralPath (Join-Path $classesSource "org") -Destination $classesTarget -Recurse -Force

$requiredRootResources = @("logback-native.xml", "module-info.class", "THIRD-PARTY.txt")
foreach ($resourceName in $requiredRootResources) {
    $resourcePath = Join-Path $classesSource $resourceName
    if (-not (Test-Path -LiteralPath $resourcePath -PathType Leaf)) {
        throw "The engine classes are incomplete; $resourceName is missing."
    }
    Copy-Item -LiteralPath $resourcePath -Destination $classesTarget -Force
}

$i18nTarget = Join-Path $classesTarget "i18n"
New-Item -ItemType Directory -Path $i18nTarget | Out-Null
foreach ($resourceName in @("strings.properties", "4096words_en.txt")) {
    $resourcePath = Join-Path $classesSource "i18n\$resourceName"
    if (-not (Test-Path -LiteralPath $resourcePath -PathType Leaf)) {
        throw "The engine classes are incomplete; i18n/$resourceName is missing."
    }
    Copy-Item -LiteralPath $resourcePath -Destination $i18nTarget -Force
}

foreach ($legacyUiDirectory in @("fxml", "css", "img")) {
    if (Test-Path -LiteralPath (Join-Path $classesTarget $legacyUiDirectory)) {
        throw "The native engine stage unexpectedly contains legacy UI resources: $legacyUiDirectory"
    }
}

$seenLibraries = @{}
foreach ($entry in $releaseClasspathEntries) {
    $resolvedEntry = [System.IO.Path]::GetFullPath($entry)
    if (-not (Test-Path -LiteralPath $resolvedEntry -PathType Leaf)) {
        throw "Runtime dependency is missing: $resolvedEntry"
    }

    $fileName = [System.IO.Path]::GetFileName($resolvedEntry)
    if ($seenLibraries.ContainsKey($fileName) -and $seenLibraries[$fileName] -ne $resolvedEntry) {
        throw "Two runtime dependencies share the file name $fileName; resolve the collision before packaging."
    }
    $seenLibraries[$fileName] = $resolvedEntry
    Copy-Item -LiteralPath $resolvedEntry -Destination (Join-Path $librariesTarget $fileName) -Force
}

$runtimeModules = "java.base,java.compiler,java.desktop,java.instrument,java.logging,java.management,java.naming,java.net.http,java.scripting,java.sql,java.xml,jdk.accessibility,jdk.crypto.cryptoki,jdk.crypto.ec,jdk.crypto.mscapi,jdk.management.jfr,jdk.unsupported"
& $jlink --output $runtimeTarget --add-modules $runtimeModules --no-header-files --no-man-pages --strip-debug --compress zip-0
if ($LASTEXITCODE -ne 0) { throw "jlink could not create the bundled Java runtime." }

$noticesTarget = Join-Path $stageRoot "Notices"
New-Item -ItemType Directory -Path $noticesTarget | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE.txt") -Destination $noticesTarget
$thirdPartyNotice = Join-Path $classesSource "THIRD-PARTY.txt"
if (Test-Path -LiteralPath $thirdPartyNotice) {
    Copy-Item -LiteralPath $thirdPartyNotice -Destination $noticesTarget
}

if (-not [string]::IsNullOrWhiteSpace($SigningThumbprint)) {
    [xml]$projectXml = Get-Content -LiteralPath $project -Raw
    $buildToolsReference = @($projectXml.Project.ItemGroup.PackageReference) |
        Where-Object { $_.Include -eq "Microsoft.Windows.SDK.BuildTools" } |
        Select-Object -First 1
    if ($null -eq $buildToolsReference) {
        throw "The native project does not reference Microsoft.Windows.SDK.BuildTools."
    }
    $buildToolsVersion = [string]$buildToolsReference.Version
    $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    $buildToolsRoot = Join-Path $userProfile ".nuget\packages\microsoft.windows.sdk.buildtools\$buildToolsVersion"
    $buildToolsProps = Join-Path $buildToolsRoot "build\Microsoft.Windows.SDK.BuildTools.props"
    if (-not (Test-Path -LiteralPath $buildToolsProps -PathType Leaf)) {
        throw "Restore the native project so the Windows SDK Build Tools package is available."
    }
    [xml]$propsXml = Get-Content -LiteralPath $buildToolsProps -Raw
    $sdkToolsVersion = [string]$propsXml.Project.PropertyGroup.WindowsSDKBuildToolsVersion
    $signTool = Join-Path $buildToolsRoot "bin\$sdkToolsVersion\x64\signtool.exe"
    if (-not (Test-Path -LiteralPath $signTool -PathType Leaf)) {
        throw "The Windows SDK SignTool is missing: $signTool"
    }

    foreach ($authoredBinary in @("VaultKind.Windows.exe", "VaultKind.Windows.dll")) {
        $binaryPath = Join-Path $stageRoot $authoredBinary
        & $signTool sign /sha1 $SigningThumbprint /fd SHA256 /td SHA256 /tr "http://timestamp.digicert.com" $binaryPath
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $authoredBinary." }
    }
}

$manifest = [ordered]@{
    product = "VaultKind"
    version = $Version
    runtimeIdentifier = $RuntimeIdentifier
    language = "en-US"
    signed = -not [string]::IsNullOrWhiteSpace($SigningThumbprint)
    distribution = if ($CreatePortableArchive) { "portable-zip" } else { "staged-layout" }
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stageRoot "release-manifest.json") -Encoding utf8

Get-ChildItem -LiteralPath $stageRoot -Filter "*.pdb" -File -Recurse | Remove-Item -Force

if ($CreatePortableArchive) {
    $archivePath = Join-Path $artifactsRoot "VaultKind-$Version-$RuntimeIdentifier.zip"
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }
    Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$archivePath.sha256" -Value "$archiveHash  $([System.IO.Path]::GetFileName($archivePath))" -Encoding ascii
    Write-Host "VaultKind portable archive created at $archivePath"
    Write-Host "SHA-256: $archiveHash"
}

if ($CreateMsix) {
    if ([string]::IsNullOrWhiteSpace($SigningThumbprint) -or [string]::IsNullOrWhiteSpace($PackagePublisher)) {
        throw "CreateMsix requires both SigningThumbprint and PackagePublisher."
    }

    $msixScript = Join-Path $repositoryRoot "scripts\build-native-msix.ps1"
    $msixVersion = "$Version.0"
    & $msixScript `
        -BinaryRoot $stageRoot `
        -RuntimeIdentifier $RuntimeIdentifier `
        -Version $msixVersion `
        -PackageName $PackageName `
        -Publisher $PackagePublisher `
        -SigningThumbprint $SigningThumbprint
    if ($LASTEXITCODE -ne 0) { throw "The signed MSIX build failed." }
}

Write-Host "VaultKind release layout created at $stageRoot"
if ([string]::IsNullOrWhiteSpace($SigningThumbprint)) {
    Write-Warning "The layout is unsigned. Windows may warn about or block it on some systems."
}
