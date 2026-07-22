$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$knownJavaHome = 'C:\Program Files\Eclipse Adoptium\jdk-26.0.1.8-hotspot'
$javaHomeCandidates = @($env:JAVA_HOME, $knownJavaHome) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'bin\javaw.exe')) }
$javaHome = $javaHomeCandidates | Select-Object -First 1

if (-not $javaHome) {
	throw 'Java 26 was not found. Install Eclipse Adoptium JDK 26 or set JAVA_HOME.'
}

$mavenWrapper = Join-Path $repoRoot 'mvnw.cmd'
$javaw = Join-Path $javaHome 'bin\javaw.exe'
$profileDir = Join-Path $repoRoot 'target\ui-dev-profile'
$settingsPath = Join-Path $profileDir 'settings.json'
$ipcSocketPath = Join-Path $profileDir 'ipc.socket'
$logDir = Join-Path $profileDir 'logs'
$pluginDir = Join-Path $profileDir 'plugins'

New-Item -ItemType Directory -Force -Path $profileDir, $logDir, $pluginDir | Out-Null

$previousJavaHome = $env:JAVA_HOME
$env:JAVA_HOME = $javaHome

try {
	Push-Location $repoRoot
	try {
		Write-Host 'Preparing the latest VaultKind development build...'
		& $mavenWrapper -q -DskipTests process-classes
		if ($LASTEXITCODE -ne 0) {
			throw "The VaultKind build failed with exit code $LASTEXITCODE."
		}

		$javaFxJars = Get-ChildItem -LiteralPath (Join-Path $env:USERPROFILE '.m2\repository\org\openjfx') -Recurse -Filter '*-25.0.3-win.jar' |
			ForEach-Object FullName
		if (-not $javaFxJars) {
			throw 'JavaFX 25.0.3 was not found in the local Maven cache. Run the build while online once.'
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

		Start-Process -FilePath $javaw -ArgumentList $launchArguments -WorkingDirectory $repoRoot -WindowStyle Hidden
		Write-Host 'VaultKind started. This launcher can now close.'
	} finally {
		Pop-Location
	}
} finally {
	$env:JAVA_HOME = $previousJavaHome
}

