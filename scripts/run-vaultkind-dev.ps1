$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$knownJavaHome = 'C:\Program Files\Eclipse Adoptium\jdk-26.0.1.8-hotspot'
$javaHomeCandidates = @($env:JAVA_HOME, $knownJavaHome) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'bin\javaw.exe')) }
$javaHome = $javaHomeCandidates | Select-Object -First 1

if (-not $javaHome) {
	throw 'Java 26 was not found. Install Eclipse Adoptium JDK 26 or set JAVA_HOME.'
}

$javaw = Join-Path $javaHome 'bin\javaw.exe'
$compiledModule = Join-Path $repoRoot 'target\classes\module-info.class'
$profileDir = Join-Path $repoRoot 'target\ui-dev-profile'
$settingsPath = Join-Path $profileDir 'settings.json'
$ipcSocketPath = Join-Path $profileDir 'ipc.socket'
$logDir = Join-Path $profileDir 'logs'
$pluginDir = Join-Path $profileDir 'plugins'

New-Item -ItemType Directory -Force -Path $profileDir, $logDir, $pluginDir | Out-Null

if (-not (Test-Path -LiteralPath $compiledModule)) {
	throw 'No compiled VaultKind build was found. Ask Codex to build the project once, then use this launcher again.'
}

Write-Host 'Starting VaultKind...'

$javaFxJars = Get-ChildItem -LiteralPath (Join-Path $env:USERPROFILE '.m2\repository\org\openjfx') -Recurse -Filter '*-25.0.3-win.jar' |
	ForEach-Object FullName
if (-not $javaFxJars) {
	throw 'JavaFX 25.0.3 was not found in the local Maven cache. Ask Codex to rebuild the project once.'
}

$modulePath = @(
	(Join-Path $repoRoot 'target\classes')
	(Join-Path $repoRoot 'target\mods')
	$javaFxJars
) -join ';'

$launchArguments = @(
	'--module-path', $modulePath
	'-Djavafx.enablePreview=true'
	"-Dcryptomator.settingsPath=$settingsPath"
	"-Dcryptomator.ipcSocketPath=$ipcSocketPath"
	"-Dcryptomator.logDir=$logDir"
	"-Dcryptomator.pluginDir=$pluginDir"
	'-m', 'org.cryptomator.desktop/org.cryptomator.launcher.Cryptomator'
)

$stdoutLog = Join-Path $profileDir 'launcher-output.log'
$stderrLog = Join-Path $profileDir 'launcher-error.log'
$process = Start-Process -FilePath $javaw -ArgumentList $launchArguments -WorkingDirectory $repoRoot -WindowStyle Hidden -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog -PassThru
Start-Sleep -Seconds 2

if ($process.HasExited -and $process.ExitCode -ne 0) {
	throw "VaultKind exited during startup. Details are in $stderrLog"
}

Write-Host 'VaultKind started. This launcher can now close.'
