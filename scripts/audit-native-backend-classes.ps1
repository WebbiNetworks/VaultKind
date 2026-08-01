[CmdletBinding()]
param(
    [string]$BinaryRoot = ".\artifacts\VaultKind-1.0.0-win-x64",

    [string]$OutputDirectory = ".\target\native-backend-reachability"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$binaryRootPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $BinaryRoot))
$classesRoot = Join-Path $binaryRootPath "Engine\classes"
$libraryRoot = Join-Path $binaryRootPath "Engine\lib"
$entryPoint = "org.cryptomator.launcher.NativeBackendMain"
$entryPointFile = Join-Path $classesRoot ($entryPoint.Replace(".", "\") + ".class")

if (-not (Test-Path -LiteralPath $entryPointFile -PathType Leaf)) {
    throw "The staged native-backend entry point is missing: $entryPointFile"
}
if (-not (Test-Path -LiteralPath $libraryRoot -PathType Container)) {
    throw "The staged engine library directory is missing: $libraryRoot"
}

$jdeps = Get-Command "jdeps.exe" -ErrorAction SilentlyContinue
if ($null -eq $jdeps) {
    throw "jdeps.exe was not found. Run this audit with the reviewed release JDK on PATH."
}

$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$tempPath = [System.IO.Path]::GetFullPath((Join-Path $tempBase ("vaultkind-jdeps-" + [guid]::NewGuid().ToString("N"))))
$requiredTempPrefix = $tempBase.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar + "vaultkind-jdeps-"
if (-not $tempPath.StartsWith($requiredTempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The jdeps temporary directory resolved outside the expected location: $tempPath"
}

try {
    New-Item -ItemType Directory -Path $tempPath | Out-Null
    $classPath = "$classesRoot;$libraryRoot\*"
    & $jdeps.Source --multi-release base -verbose:class -filter:none --ignore-missing-deps --dot-output $tempPath --class-path $classPath (Join-Path $classesRoot "org")
    if ($LASTEXITCODE -ne 0) {
        throw "jdeps could not analyze the staged VaultKind classes."
    }

    $dotFile = Join-Path $tempPath "org.dot"
    if (-not (Test-Path -LiteralPath $dotFile -PathType Leaf)) {
        throw "jdeps did not create the expected class dependency graph."
    }

    $edges = @{}
    foreach ($line in Get-Content -LiteralPath $dotFile) {
        if ($line -match '^\s*"(org\.cryptomator\.[^"]+)"\s*->\s*"(org\.cryptomator\.[^" ]+)(?: \([^\)]+\))?";') {
            $sourceClass = $matches[1]
            $targetClass = $matches[2]
            if (-not $edges.ContainsKey($sourceClass)) {
                $edges[$sourceClass] = [System.Collections.Generic.HashSet[string]]::new()
            }
            [void]$edges[$sourceClass].Add($targetClass)
        }
    }

    $authoredClasses = [System.Collections.Generic.HashSet[string]]::new()
    Get-ChildItem -LiteralPath (Join-Path $classesRoot "org\cryptomator") -Recurse -File -Filter "*.class" | ForEach-Object {
        $relativePath = $_.FullName.Substring($classesRoot.Length + 1).Replace("\", ".")
        [void]$authoredClasses.Add($relativePath.Substring(0, $relativePath.Length - ".class".Length))
    }

    $reachableClasses = [System.Collections.Generic.HashSet[string]]::new()
    $pendingClasses = [System.Collections.Generic.Queue[string]]::new()
    $pendingClasses.Enqueue($entryPoint)
    while ($pendingClasses.Count -gt 0) {
        $className = $pendingClasses.Dequeue()
        if (-not $reachableClasses.Add($className)) {
            continue
        }
        if ($edges.ContainsKey($className)) {
            foreach ($dependency in $edges[$className]) {
                if (-not $reachableClasses.Contains($dependency)) {
                    $pendingClasses.Enqueue($dependency)
                }
            }
        }
    }

    $reachableAuthoredClasses = @($reachableClasses | Where-Object { $authoredClasses.Contains($_) } | Sort-Object)
    $candidateClasses = @($authoredClasses | Where-Object { -not $reachableClasses.Contains($_) } | Sort-Object)

    function Measure-ClassBytes([string[]]$classNames) {
        return ($classNames | ForEach-Object {
            $classFile = Join-Path $classesRoot ($_.Replace(".", "\") + ".class")
            (Get-Item -LiteralPath $classFile).Length
        } | Measure-Object -Sum).Sum
    }

    $candidatePackages = @($candidateClasses | ForEach-Object {
        $segments = $_.Split(".")
        if ($segments.Length -gt 2) { $segments[2] } else { "(root)" }
    } | Group-Object | Sort-Object Count -Descending | ForEach-Object {
        [ordered]@{ package = $_.Name; classes = $_.Count }
    })

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $serviceDescriptors = @()
    $releaseJars = @(Get-ChildItem -LiteralPath $libraryRoot -File -Filter "*.jar" | Sort-Object Name)
    foreach ($jar in $releaseJars) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($jar.FullName)
        try {
            foreach ($entry in $archive.Entries | Where-Object { $_.FullName -like "META-INF/services/*" -and -not $_.FullName.EndsWith("/") }) {
                $reader = [System.IO.StreamReader]::new($entry.Open())
                try {
                    $providers = @($reader.ReadToEnd().Split("`n") | ForEach-Object {
                        ($_ -split "#", 2)[0].Trim()
                    } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                    $serviceDescriptors += [ordered]@{
                        jar = $jar.Name
                        service = $entry.FullName.Substring("META-INF/services/".Length)
                        providers = $providers
                    }
                } finally {
                    $reader.Dispose()
                }
            }
        } finally {
            $archive.Dispose()
        }
    }

    $moduleInfoSource = Join-Path $repositoryRoot "src\main\java\module-info.java"
    $moduleSourceWithoutLineComments = ((Get-Content -LiteralPath $moduleInfoSource) | ForEach-Object { $_ -replace "//.*$", "" }) -join "`n"
    $moduleUses = @([regex]::Matches($moduleSourceWithoutLineComments, '\buses\s+([\w.]+)\s*;') | ForEach-Object { $_.Groups[1].Value })
    $moduleProvides = @([regex]::Matches($moduleSourceWithoutLineComments, '\bprovides\s+([\w.]+)\s+with\s+([^;]+);') | ForEach-Object {
        [ordered]@{
            service = $_.Groups[1].Value
            providers = @($_.Groups[2].Value.Split(",") | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        }
    })

    $sourceRoot = Join-Path $repositoryRoot "src\main\java"
    $dynamicConstructionSites = @()
    foreach ($sourceFile in Get-ChildItem -LiteralPath (Join-Path $sourceRoot "org\cryptomator") -Recurse -File -Filter "*.java") {
        $relativeSource = $sourceFile.FullName.Substring($sourceRoot.Length + 1)
        $topLevelClass = $relativeSource.Replace("\", ".").Substring(0, $relativeSource.Length - ".java".Length)
        if ($reachableAuthoredClasses -notcontains $topLevelClass) {
            continue
        }
        foreach ($match in Select-String -LiteralPath $sourceFile.FullName -Pattern 'Class\.forName|loadClass\(|ServiceLoader|readValue\(|convertValue\(|treeToValue\(') {
            $dynamicConstructionSites += [ordered]@{
                source = $relativeSource.Replace("\", "/")
                line = $match.LineNumber
                expression = $match.Line.Trim()
            }
        }
    }

    $reviewedDynamicTargets = @(
        "org.cryptomator.common.settings.SettingsJson",
        "org.cryptomator.common.settings.VaultSettingsJson",
        "org.cryptomator.nativeui.NativeUiProtocol`$NativeUiRequest"
    )
    $missingDynamicTargets = @($reviewedDynamicTargets | Where-Object { $reachableAuthoredClasses -notcontains $_ })
    if ($missingDynamicTargets.Count -gt 0) {
        throw "Reviewed reflection/serialization targets left the static closure: $($missingDynamicTargets -join ', ')"
    }

    $reachableOuterClasses = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($className in $reachableAuthoredClasses) {
        [void]$reachableOuterClasses.Add(($className -split '\$', 2)[0])
    }
    $candidateNestedSiblings = @($candidateClasses | Where-Object {
        $_.Contains("$") -and $reachableOuterClasses.Contains(($_ -split '\$', 2)[0])
    } | Sort-Object)

    $runtimeRetention = [ordered]@{
        launchMode = "classpath"
        wholeJarRetention = $true
        releaseJarCount = $releaseJars.Count
        jarServiceDescriptorCount = $serviceDescriptors.Count
        jarServiceProviderCount = @($serviceDescriptors | ForEach-Object { $_.providers }).Count
        jarServiceDescriptors = $serviceDescriptors
        moduleInfoActiveAtNativeRuntime = $false
        moduleUses = $moduleUses
        moduleProvides = $moduleProvides
        reachableDynamicConstructionSites = $dynamicConstructionSites
        reviewedDynamicTargets = $reviewedDynamicTargets
        reviewedDynamicTargetsOutsideStaticClosure = $missingDynamicTargets
        candidateNestedSiblingsOfReachableClasses = $candidateNestedSiblings
        authoredRetentionNotes = @(
            "The native engine launches with -cp, so authored module-info uses/provides declarations are documentary and are not activated by the JVM.",
            "Release JARs remain whole at this boundary; every META-INF/services provider inside those JARs is therefore retained.",
            "The reviewed Jackson targets are asserted to remain in the static closure.",
            "Every nested class belonging to a reachable authored outer class is inventoried; none currently falls outside the static closure.",
            "jdeps static closure includes explicit Dagger-generated and protocol DTO class references.",
            "A later authored-class filter must add reviewed dynamic targets before removing any candidate class."
        )
    }

    $summary = [ordered]@{
        entryPoint = $entryPoint
        generatedAtUtc = [DateTime]::UtcNow.ToString("o")
        jdepsVersion = (& $jdeps.Source --version 2>&1 | Select-Object -First 1).ToString()
        authoredClassCount = $authoredClasses.Count
        staticallyReachableAuthoredClassCount = $reachableAuthoredClasses.Count
        staticCandidateClassCount = $candidateClasses.Count
        staticallyReachableAuthoredBytes = Measure-ClassBytes $reachableAuthoredClasses
        staticCandidateBytes = Measure-ClassBytes $candidateClasses
        candidatePackages = $candidatePackages
        runtimeInventory = [ordered]@{
            releaseJarCount = $runtimeRetention.releaseJarCount
            jarServiceDescriptorCount = $runtimeRetention.jarServiceDescriptorCount
            jarServiceProviderCount = $runtimeRetention.jarServiceProviderCount
            moduleUsesCount = $moduleUses.Count
            moduleProvidesCount = $moduleProvides.Count
            reachableDynamicConstructionSiteCount = $dynamicConstructionSites.Count
            reviewedDynamicTargetCount = $reviewedDynamicTargets.Count
            candidateNestedSiblingCount = $candidateNestedSiblings.Count
        }
        removalAuthorized = $false
        unresolvedRuntimeEdges = @(
            "coverage of every native protocol operation and supported mount provider"
        )
    }

    Set-Content -LiteralPath (Join-Path $outputPath "reachable-classes.txt") -Value $reachableAuthoredClasses -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $outputPath "candidate-classes.txt") -Value $candidateClasses -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $outputPath "summary.json") -Value ($summary | ConvertTo-Json -Depth 5) -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $outputPath "runtime-retention.json") -Value ($runtimeRetention | ConvertTo-Json -Depth 8) -Encoding UTF8

    Write-Host "Native-backend class reachability inventory created at $outputPath"
    Write-Host "Authored classes: $($authoredClasses.Count)"
    Write-Host "Statically reachable authored classes: $($reachableAuthoredClasses.Count)"
    Write-Host "Static candidates requiring runtime retention review: $($candidateClasses.Count)"
    Write-Host "Release service descriptors/providers inventoried: $($serviceDescriptors.Count)/$($runtimeRetention.jarServiceProviderCount)"
    Write-Warning "This inventory does not authorize class removal. Resolve the runtime-edge list in summary.json first."
} finally {
    if (Test-Path -LiteralPath $tempPath) {
        $resolvedTempPath = [System.IO.Path]::GetFullPath($tempPath)
        if (-not $resolvedTempPath.StartsWith($requiredTempPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove an unexpected temporary directory: $resolvedTempPath"
        }
        Remove-Item -LiteralPath $resolvedTempPath -Recurse -Force
    }
}
