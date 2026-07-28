[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BinaryRoot
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$resolvedBinaryRoot = [System.IO.Path]::GetFullPath($BinaryRoot)
$artifactsPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedBinaryRoot.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Bundled-engine tests are limited to staged layouts under $artifactsRoot"
}

$engineRoot = Join-Path $resolvedBinaryRoot "Engine"
$javaExecutable = Join-Path $engineRoot "runtime\bin\java.exe"
$classesDirectory = Join-Path $engineRoot "classes"
$librariesDirectory = Join-Path $engineRoot "lib"
foreach ($requiredPath in @($javaExecutable, $classesDirectory, $librariesDirectory)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Bundled-engine input is missing: $requiredPath"
    }
}

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryPrefix = $temporaryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$testRoot = [System.IO.Path]::GetFullPath((Join-Path $temporaryRoot ("VK-es-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))))
if (-not $testRoot.StartsWith($temporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The temporary engine test profile must remain inside $temporaryRoot"
}
$isolatedLocalAppData = $testRoot
$profileRoot = Join-Path $isolatedLocalAppData "VaultKind\engine"
$socketPath = Join-Path $isolatedLocalAppData "VaultKind\bridge\native-bridge-v1.sock"
New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null

$engineProcess = $null
try {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $javaExecutable
    $startInfo.WorkingDirectory = $engineRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $engineArguments = @(
        "-Dlogback.configurationFile=$(Join-Path $classesDirectory 'logback-native.xml')",
        "-Dcryptomator.settingsPath=$(Join-Path $profileRoot 'settings.json')",
        "-Dcryptomator.pluginDir=$(Join-Path $profileRoot 'plugins')",
        "-Dcryptomator.logDir=$(Join-Path $profileRoot 'logs')",
        "-Dcryptomator.mountPointsDir=$(Join-Path $profileRoot 'mnt')",
        "-Dcryptomator.disableUpdateCheck=true",
        "-cp",
        "$classesDirectory;$librariesDirectory\*",
        "org.cryptomator.launcher.NativeBackendMain"
    )
    $startInfo.Arguments = ($engineArguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join " "

    # Windows PowerShell 5.1 exposes a null ProcessStartInfo.EnvironmentVariables
    # collection. Scope the override to Process.Start so only the child inherits it.
    $previousLocalAppData = $env:LOCALAPPDATA
    try {
        $env:LOCALAPPDATA = $isolatedLocalAppData
        $engineProcess = [System.Diagnostics.Process]::Start($startInfo)
    }
    finally {
        $env:LOCALAPPDATA = $previousLocalAppData
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $socketPath) -and [DateTime]::UtcNow -lt $deadline) {
        if ($engineProcess.HasExited) { throw "The bundled engine exited with code $($engineProcess.ExitCode) before opening its socket." }
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $socketPath)) {
        throw "The bundled engine did not open its isolated socket within 20 seconds."
    }

    $probeProject = Join-Path $repositoryRoot "native\VaultKind.Windows.Tests\VaultKind.Windows.Tests.csproj"
    & dotnet run --project $probeProject -c Release --no-restore -- --probe-socket $socketPath
    if ($LASTEXITCODE -ne 0) { throw "The bundled engine protocol probe failed." }
    if (-not $engineProcess.WaitForExit(10000)) {
        throw "The bundled engine accepted shutdown but did not exit within 10 seconds."
    }
    $engineProcess.WaitForExit()

    Write-Host "Bundled engine smoke test passed."
    Write-Host "Runtime: $javaExecutable"
}
finally {
    if ($null -ne $engineProcess) {
        if (-not $engineProcess.HasExited) { $engineProcess.Kill() }
        $engineProcess.Dispose()
    }
    if (Test-Path -LiteralPath $testRoot) {
        for ($cleanupAttempt = 1; $cleanupAttempt -le 20; $cleanupAttempt++) {
            try {
                Remove-Item -LiteralPath $testRoot -Recurse -Force
                break
            }
            catch [System.IO.IOException] {
                if ($cleanupAttempt -eq 20) { throw }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
