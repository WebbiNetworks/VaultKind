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
# images. Copy the authored classes first, then remove only exact class files
# that have passed the native reachability and runtime-retention audit.
Copy-Item -LiteralPath (Join-Path $classesSource "org") -Destination $classesTarget -Recurse -Force

function Remove-ReviewedClassPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageName,

        [Parameter(Mandatory = $true)]
        [string[]]$ReviewedClassFiles
    )

    $packageSourceDirectory = Join-Path $classesSource ($PackageName.Replace('.', '\'))
    $actualClassFiles = @(Get-ChildItem -LiteralPath $packageSourceDirectory -File -Filter "*.class" | ForEach-Object {
        $_.FullName.Substring($classesSource.Length + 1)
    })
    $classDifference = @(Compare-Object -ReferenceObject $ReviewedClassFiles -DifferenceObject $actualClassFiles)
    if ($classDifference.Count -ne 0) {
        throw "The inherited $PackageName class set changed. Re-run the native reachability audit before updating the exact release exclusion."
    }

    foreach ($relativeClassFile in $ReviewedClassFiles) {
        $stagedClassFile = Join-Path $classesTarget $relativeClassFile
        if (-not (Test-Path -LiteralPath $stagedClassFile -PathType Leaf)) {
            throw "The reviewed inherited class is missing from the stage: $relativeClassFile"
        }
        Remove-Item -LiteralPath $stagedClassFile -Force
    }
}

function Remove-ReviewedClassSlice {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageName,

        [Parameter(Mandatory = $true)]
        [string[]]$RemovedClassFiles,

        [Parameter(Mandatory = $true)]
        [string[]]$RetainedClassFiles
    )

    $reviewedClassFiles = @($RemovedClassFiles) + @($RetainedClassFiles)
    $duplicateReviewedFiles = @($reviewedClassFiles | Group-Object | Where-Object Count -gt 1)
    if ($duplicateReviewedFiles.Count -ne 0) {
        throw "The reviewed $PackageName class slice contains duplicate entries."
    }

    $packageSourceDirectory = Join-Path $classesSource ($PackageName.Replace('.', '\'))
    $actualClassFiles = @(Get-ChildItem -LiteralPath $packageSourceDirectory -File -Filter "*.class" | ForEach-Object {
        $_.FullName.Substring($classesSource.Length + 1)
    })
    $classDifference = @(Compare-Object -ReferenceObject $reviewedClassFiles -DifferenceObject $actualClassFiles)
    if ($classDifference.Count -ne 0) {
        throw "The inherited $PackageName class set changed. Re-run the native reachability audit before updating the exact release exclusion."
    }

    foreach ($relativeClassFile in $reviewedClassFiles) {
        $stagedClassFile = Join-Path $classesTarget $relativeClassFile
        if (-not (Test-Path -LiteralPath $stagedClassFile -PathType Leaf)) {
            throw "The reviewed inherited class is missing from the stage: $relativeClassFile"
        }
    }

    foreach ($relativeClassFile in $RemovedClassFiles) {
        Remove-Item -LiteralPath (Join-Path $classesTarget $relativeClassFile) -Force
    }
}

$reviewedDialogClassFiles = @(
    "org\cryptomator\ui\dialogs\Dialogs`$1.class",
    "org\cryptomator\ui\dialogs\Dialogs.class",
    "org\cryptomator\ui\dialogs\Dialogs_Factory.class",
    "org\cryptomator\ui\dialogs\SimpleDialog`$Builder.class",
    "org\cryptomator\ui\dialogs\SimpleDialog.class",
    "org\cryptomator\ui\dialogs\SimpleDialogController.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.dialogs" -ReviewedClassFiles $reviewedDialogClassFiles

$reviewedWrongFileAlertClassFiles = @(
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertComponent`$Builder.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertComponent.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertController.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertController_Factory.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertModule.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertModule_ProvideStageFactory.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertModule_ProvideWrongFileAlertSceneFactory.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertScoped.class",
    "org\cryptomator\ui\wrongfilealert\WrongFileAlertWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.wrongfilealert" -ReviewedClassFiles $reviewedWrongFileAlertClassFiles

$reviewedUpdateReminderClassFiles = @(
    "org\cryptomator\ui\updatereminder\UpdateReminderComponent`$Factory.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderComponent.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderController.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderController_Factory.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderModule.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderModule_ProvideStageFactory.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderModule_ProvideUpdateReminderSceneFactory.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderScoped.class",
    "org\cryptomator\ui\updatereminder\UpdateReminderWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.updatereminder" -ReviewedClassFiles $reviewedUpdateReminderClassFiles

$reviewedShareVaultClassFiles = @(
    "org\cryptomator\ui\sharevault\ShareVaultComponent`$Factory.class",
    "org\cryptomator\ui\sharevault\ShareVaultComponent.class",
    "org\cryptomator\ui\sharevault\ShareVaultController.class",
    "org\cryptomator\ui\sharevault\ShareVaultController_Factory.class",
    "org\cryptomator\ui\sharevault\ShareVaultModule.class",
    "org\cryptomator\ui\sharevault\ShareVaultModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\sharevault\ShareVaultModule_ProvideShareVaultSceneFactory.class",
    "org\cryptomator\ui\sharevault\ShareVaultModule_ProvideStageFactory.class",
    "org\cryptomator\ui\sharevault\ShareVaultScoped.class",
    "org\cryptomator\ui\sharevault\ShareVaultWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.sharevault" -ReviewedClassFiles $reviewedShareVaultClassFiles

$reviewedVaultStatisticsClassFiles = @(
    "org\cryptomator\ui\stats\VaultStatisticsComponent`$Builder.class",
    "org\cryptomator\ui\stats\VaultStatisticsComponent.class",
    "org\cryptomator\ui\stats\VaultStatisticsController`$IoSamplingAnimationHandler.class",
    "org\cryptomator\ui\stats\VaultStatisticsController.class",
    "org\cryptomator\ui\stats\VaultStatisticsController_Factory.class",
    "org\cryptomator\ui\stats\VaultStatisticsModule`$1.class",
    "org\cryptomator\ui\stats\VaultStatisticsModule.class",
    "org\cryptomator\ui\stats\VaultStatisticsModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\stats\VaultStatisticsModule_ProvideStageFactory.class",
    "org\cryptomator\ui\stats\VaultStatisticsModule_ProvideVaultStatisticsSceneFactory.class",
    "org\cryptomator\ui\stats\VaultStatisticsScoped.class",
    "org\cryptomator\ui\stats\VaultStatisticsWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.stats" -ReviewedClassFiles $reviewedVaultStatisticsClassFiles

$reviewedDecryptNameClassFiles = @(
    "org\cryptomator\ui\decryptname\CipherAndCleartext.class",
    "org\cryptomator\ui\decryptname\DecryptFileNamesViewController.class",
    "org\cryptomator\ui\decryptname\DecryptFileNamesViewController_Factory.class",
    "org\cryptomator\ui\decryptname\DecryptNameComponent`$Factory.class",
    "org\cryptomator\ui\decryptname\DecryptNameComponent.class",
    "org\cryptomator\ui\decryptname\DecryptNameModule.class",
    "org\cryptomator\ui\decryptname\DecryptNameModule_ProvideDecryptNamesViewSceneFactory.class",
    "org\cryptomator\ui\decryptname\DecryptNameModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\decryptname\DecryptNameModule_ProvideStageFactory.class",
    "org\cryptomator\ui\decryptname\DecryptNameScoped.class",
    "org\cryptomator\ui\decryptname\DecryptNameWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.decryptname" -ReviewedClassFiles $reviewedDecryptNameClassFiles

$reviewedErrorWindowClassFiles = @(
    "org\cryptomator\ui\error\ErrorComponent`$Factory.class",
    "org\cryptomator\ui\error\ErrorComponent.class",
    "org\cryptomator\ui\error\ErrorController.class",
    "org\cryptomator\ui\error\ErrorController_Factory.class",
    "org\cryptomator\ui\error\ErrorDiscussion`$Answer.class",
    "org\cryptomator\ui\error\ErrorDiscussion.class",
    "org\cryptomator\ui\error\ErrorModule.class",
    "org\cryptomator\ui\error\ErrorModule_ProvideErrorCodeFactory.class",
    "org\cryptomator\ui\error\ErrorModule_ProvideErrorSceneFactory.class",
    "org\cryptomator\ui\error\ErrorModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\error\ErrorModule_ProvideStackTraceFactory.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.error" -ReviewedClassFiles $reviewedErrorWindowClassFiles

$reviewedJavaFxControlClassFiles = @(
    "org\cryptomator\ui\controls\AlphanumericTextField.class",
    "org\cryptomator\ui\controls\DataLabel.class",
    "org\cryptomator\ui\controls\DraggableListCell.class",
    "org\cryptomator\ui\controls\FontAwesome5Icon.class",
    "org\cryptomator\ui\controls\FontAwesome5IconView.class",
    "org\cryptomator\ui\controls\FontAwesome5Spinner.class",
    "org\cryptomator\ui\controls\FormattedLabel.class",
    "org\cryptomator\ui\controls\FormattedString.class",
    "org\cryptomator\ui\controls\InfoBar.class",
    "org\cryptomator\ui\controls\NiceSecurePasswordField.class",
    "org\cryptomator\ui\controls\NumericTextField.class",
    "org\cryptomator\ui\controls\PasswordStrengthIndicator.class",
    "org\cryptomator\ui\controls\SecurePasswordField`$1.class",
    "org\cryptomator\ui\controls\SecurePasswordField.class",
    "org\cryptomator\ui\controls\ThroughputLabel.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.controls" -ReviewedClassFiles $reviewedJavaFxControlClassFiles

$reviewedQuitWindowClassFiles = @(
    "org\cryptomator\ui\quit\QuitComponent`$Builder.class",
    "org\cryptomator\ui\quit\QuitComponent.class",
    "org\cryptomator\ui\quit\QuitController.class",
    "org\cryptomator\ui\quit\QuitController_Factory.class",
    "org\cryptomator\ui\quit\QuitForcedController.class",
    "org\cryptomator\ui\quit\QuitForcedController_Factory.class",
    "org\cryptomator\ui\quit\QuitModule.class",
    "org\cryptomator\ui\quit\QuitModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\quit\QuitModule_ProvideQuitForcedSceneFactory.class",
    "org\cryptomator\ui\quit\QuitModule_ProvideQuitResponseFactory`$InstanceHolder.class",
    "org\cryptomator\ui\quit\QuitModule_ProvideQuitResponseFactory.class",
    "org\cryptomator\ui\quit\QuitModule_ProvideQuitSceneFactory.class",
    "org\cryptomator\ui\quit\QuitModule_ProvideStageFactory.class",
    "org\cryptomator\ui\quit\QuitScoped.class",
    "org\cryptomator\ui\quit\QuitWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.quit" -ReviewedClassFiles $reviewedQuitWindowClassFiles

$reviewedLockWindowClassFiles = @(
    "org\cryptomator\ui\lock\LockComponent`$Factory.class",
    "org\cryptomator\ui\lock\LockComponent.class",
    "org\cryptomator\ui\lock\LockFailedController.class",
    "org\cryptomator\ui\lock\LockFailedController_Factory.class",
    "org\cryptomator\ui\lock\LockForcedController.class",
    "org\cryptomator\ui\lock\LockForcedController_Factory.class",
    "org\cryptomator\ui\lock\LockModule.class",
    "org\cryptomator\ui\lock\LockModule_ProvideForceLockSceneFactory.class",
    "org\cryptomator\ui\lock\LockModule_ProvideForceRetryDecisionRefFactory`$InstanceHolder.class",
    "org\cryptomator\ui\lock\LockModule_ProvideForceRetryDecisionRefFactory.class",
    "org\cryptomator\ui\lock\LockModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\lock\LockModule_ProvideLockFailedSceneFactory.class",
    "org\cryptomator\ui\lock\LockModule_ProvideWindowFactory.class",
    "org\cryptomator\ui\lock\LockScoped.class",
    "org\cryptomator\ui\lock\LockWindow.class",
    "org\cryptomator\ui\lock\LockWorkflow.class",
    "org\cryptomator\ui\lock\LockWorkflow_Factory.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.lock" -ReviewedClassFiles $reviewedLockWindowClassFiles

$reviewedChangePasswordWindowClassFiles = @(
    "org\cryptomator\ui\changepassword\ChangePasswordComponent`$Builder.class",
    "org\cryptomator\ui\changepassword\ChangePasswordComponent.class",
    "org\cryptomator\ui\changepassword\ChangePasswordController.class",
    "org\cryptomator\ui\changepassword\ChangePasswordController_Factory.class",
    "org\cryptomator\ui\changepassword\ChangePasswordModule.class",
    "org\cryptomator\ui\changepassword\ChangePasswordModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\changepassword\ChangePasswordModule_ProvideNewPasswordControllerFactory.class",
    "org\cryptomator\ui\changepassword\ChangePasswordModule_ProvideStageFactory.class",
    "org\cryptomator\ui\changepassword\ChangePasswordModule_ProvideUnlockSceneFactory.class",
    "org\cryptomator\ui\changepassword\ChangePasswordScoped.class",
    "org\cryptomator\ui\changepassword\ChangePasswordWindow.class",
    "org\cryptomator\ui\changepassword\NewPasswordController.class",
    "org\cryptomator\ui\changepassword\PasswordStrengthUtil.class",
    "org\cryptomator\ui\changepassword\PasswordStrengthUtil_Factory.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.changepassword" -ReviewedClassFiles $reviewedChangePasswordWindowClassFiles

$reviewedRecoveryKeyWindowClassFiles = @(
    "org\cryptomator\ui\recoverykey\AutoCompleter.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyComponent`$Factory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyComponent.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyCreationController`$RecoveryKeyCreationTask.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyCreationController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyCreationController_Factory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyDisplayController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyExpertSettingsController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyExpertSettingsController_Factory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_BindRecoveryKeyValidateControllerFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideCipherComboFactory`$InstanceHolder.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideCipherComboFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideNewPasswordControllerFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyCreationSceneFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyDisplayControllerFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyExpertSettingsSceneFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyOnboardingSceneFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyPropertyFactory`$InstanceHolder.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyPropertyFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyRecoverSceneFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeyResetPasswordSceneFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideRecoveryKeySuccessSceneFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideShorteningThresholdFactory`$InstanceHolder.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideShorteningThresholdFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_ProvideStageFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyModule_VaultConfigFactory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyOnboardingController`$1.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyOnboardingController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyOnboardingController_Factory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyRecoverController`$1.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyRecoverController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyRecoverController_Factory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyResetPasswordController`$1.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyResetPasswordController`$ResetPasswordTask.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyResetPasswordController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyResetPasswordController_Factory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyScoped.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeySuccessController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeySuccessController_Factory.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyTasks`$1.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyTasks`$TaskAction.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyTasks.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyValidateController`$1.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyValidateController`$RecoveryKeyState.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyValidateController.class",
    "org\cryptomator\ui\recoverykey\RecoveryKeyWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.recoverykey" -ReviewedClassFiles $reviewedRecoveryKeyWindowClassFiles

$reviewedAddVaultWizardClassFiles = @(
    "org\cryptomator\ui\addvaultwizard\AddVaultModule.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideAddVaultStartSceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideChooseExistingVaultSceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideCreateNewVaultExpertSettingsSceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideCreateNewVaultLocationSceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideCreateNewVaultNameSceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideCreateNewVaultPasswordSceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideCreateNewVaultRecoveryKeySceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideCreateNewVaultSuccessSceneFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideNewPasswordControllerFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideRecoveryKeyDisplayControllerFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideRecoveryKeyFactory`$InstanceHolder.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideRecoveryKeyFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideShorteningThresholdFactory`$InstanceHolder.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideShorteningThresholdFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideStageFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideVaultFactory`$InstanceHolder.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideVaultFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideVaultNameFactory`$InstanceHolder.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideVaultNameFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideVaultPathFactory`$InstanceHolder.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultModule_ProvideVaultPathFactory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultStartController.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultStartController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultSuccessController.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultSuccessController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultWizardComponent`$Builder.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultWizardComponent.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultWizardScoped.class",
    "org\cryptomator\ui\addvaultwizard\AddVaultWizardWindow.class",
    "org\cryptomator\ui\addvaultwizard\ChooseExistingVaultController`$1.class",
    "org\cryptomator\ui\addvaultwizard\ChooseExistingVaultController.class",
    "org\cryptomator\ui\addvaultwizard\ChooseExistingVaultController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultExpertSettingsController.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultExpertSettingsController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultLocationController`$VaultPathStatus.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultLocationController.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultLocationController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultNameController.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultNameController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultPasswordController.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultPasswordController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultRecoveryKeyController.class",
    "org\cryptomator\ui\addvaultwizard\CreateNewVaultRecoveryKeyController_Factory.class",
    "org\cryptomator\ui\addvaultwizard\ReadmeGenerator.class",
    "org\cryptomator\ui\addvaultwizard\ReadmeGenerator_Factory.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.addvaultwizard" -ReviewedClassFiles $reviewedAddVaultWizardClassFiles

$reviewedForgetPasswordWindowClassFiles = @(
    "org\cryptomator\ui\forgetpassword\ForgetPasswordComponent`$Builder.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordComponent.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordController.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordController_Factory.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordModule.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordModule_ProvideConfirmedPropertyFactory`$InstanceHolder.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordModule_ProvideConfirmedPropertyFactory.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordModule_ProvideForgetPasswordSceneFactory.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordModule_ProvideStageFactory.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordScoped.class",
    "org\cryptomator\ui\forgetpassword\ForgetPasswordWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.forgetpassword" -ReviewedClassFiles $reviewedForgetPasswordWindowClassFiles

$reviewedLegacyUpdaterClassFiles = @(
    "org\cryptomator\updater\DelegatingHttpClient.class",
    "org\cryptomator\updater\FallbackUpdateInfo.class",
    "org\cryptomator\updater\FallbackUpdateMechanism.class",
    "org\cryptomator\updater\FallbackUpdateMechanism_Factory.class",
    "org\cryptomator\updater\UpdateChecker`$1.class",
    "org\cryptomator\updater\UpdateChecker`$UpdateCheckState.class",
    "org\cryptomator\updater\UpdateChecker`$UpdateCheckTask.class",
    "org\cryptomator\updater\UpdateChecker.class",
    "org\cryptomator\updater\UpdateCheckerHttpClient.class",
    "org\cryptomator\updater\UpdateChecker_Factory.class",
    "org\cryptomator\updater\UpdateService`$RunAllStepsTask.class",
    "org\cryptomator\updater\UpdateService.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.updater" -ReviewedClassFiles $reviewedLegacyUpdaterClassFiles

$reviewedConvertVaultWindowClassFiles = @(
    "org\cryptomator\ui\convertvault\ConvertVaultComponent`$Factory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultComponent.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideHubToPasswordConvertSceneFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideHubToPasswordStartSceneFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideHubToPasswordSuccessSceneFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideNewPasswordControllerFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideRecoveryKeyPropertyFactory`$InstanceHolder.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideRecoveryKeyPropertyFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideRecoveryKeyValidateControllerFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_ProvideStageFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultModule_VaultConfigFactory.class",
    "org\cryptomator\ui\convertvault\ConvertVaultScoped.class",
    "org\cryptomator\ui\convertvault\ConvertVaultWindow.class",
    "org\cryptomator\ui\convertvault\HubToPasswordConvertController.class",
    "org\cryptomator\ui\convertvault\HubToPasswordConvertController_Factory.class",
    "org\cryptomator\ui\convertvault\HubToPasswordStartController.class",
    "org\cryptomator\ui\convertvault\HubToPasswordStartController_Factory.class",
    "org\cryptomator\ui\convertvault\HubToPasswordSuccessController.class",
    "org\cryptomator\ui\convertvault\HubToPasswordSuccessController_Factory.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.convertvault" -ReviewedClassFiles $reviewedConvertVaultWindowClassFiles

$reviewedVaultOptionsWindowClassFiles = @(
    "org\cryptomator\ui\vaultoptions\GeneralVaultOptionsController`$IdleTimeSecondsConverter.class",
    "org\cryptomator\ui\vaultoptions\GeneralVaultOptionsController`$WhenUnlockedConverter.class",
    "org\cryptomator\ui\vaultoptions\GeneralVaultOptionsController.class",
    "org\cryptomator\ui\vaultoptions\GeneralVaultOptionsController_Factory.class",
    "org\cryptomator\ui\vaultoptions\HubOptionsController.class",
    "org\cryptomator\ui\vaultoptions\HubOptionsController_Factory.class",
    "org\cryptomator\ui\vaultoptions\MasterkeyOptionsController.class",
    "org\cryptomator\ui\vaultoptions\MasterkeyOptionsController_Factory.class",
    "org\cryptomator\ui\vaultoptions\MountOptionsController`$MountServiceConverter.class",
    "org\cryptomator\ui\vaultoptions\MountOptionsController`$NoDirSelectedException.class",
    "org\cryptomator\ui\vaultoptions\MountOptionsController`$WinDriveLetterLabelConverter.class",
    "org\cryptomator\ui\vaultoptions\MountOptionsController.class",
    "org\cryptomator\ui\vaultoptions\MountOptionsController_Factory.class",
    "org\cryptomator\ui\vaultoptions\SelectedVaultOptionsTab.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsComponent`$Factory.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsComponent.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsController`$1.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsController.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsController_Factory.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsModule.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsModule_ProvideSelectedTabPropertyFactory`$InstanceHolder.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsModule_ProvideSelectedTabPropertyFactory.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsModule_ProvideStageFactory.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsModule_ProvideVaultOptionsSceneFactory.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsScoped.class",
    "org\cryptomator\ui\vaultoptions\VaultOptionsWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.vaultoptions" -ReviewedClassFiles $reviewedVaultOptionsWindowClassFiles

$reviewedPreferencesWindowClassFiles = @(
    "org\cryptomator\ui\preferences\AboutController.class",
    "org\cryptomator\ui\preferences\AboutController_Factory.class",
    "org\cryptomator\ui\preferences\GeneralPreferencesController`$NamedServiceConverter.class",
    "org\cryptomator\ui\preferences\GeneralPreferencesController`$ServiceToSettingsConverter.class",
    "org\cryptomator\ui\preferences\GeneralPreferencesController.class",
    "org\cryptomator\ui\preferences\GeneralPreferencesController_Factory.class",
    "org\cryptomator\ui\preferences\InterfacePreferencesController`$UiThemeConverter.class",
    "org\cryptomator\ui\preferences\InterfacePreferencesController.class",
    "org\cryptomator\ui\preferences\InterfacePreferencesController_Factory.class",
    "org\cryptomator\ui\preferences\PreferencesComponent`$Builder.class",
    "org\cryptomator\ui\preferences\PreferencesComponent.class",
    "org\cryptomator\ui\preferences\PreferencesController`$1.class",
    "org\cryptomator\ui\preferences\PreferencesController.class",
    "org\cryptomator\ui\preferences\PreferencesController_Factory.class",
    "org\cryptomator\ui\preferences\PreferencesModule.class",
    "org\cryptomator\ui\preferences\PreferencesModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\preferences\PreferencesModule_ProvidePreferencesSceneFactory.class",
    "org\cryptomator\ui\preferences\PreferencesModule_ProvideSelectedTabPropertyFactory`$InstanceHolder.class",
    "org\cryptomator\ui\preferences\PreferencesModule_ProvideSelectedTabPropertyFactory.class",
    "org\cryptomator\ui\preferences\PreferencesModule_ProvideStageFactory.class",
    "org\cryptomator\ui\preferences\PreferencesScoped.class",
    "org\cryptomator\ui\preferences\PreferencesWindow.class",
    "org\cryptomator\ui\preferences\SelectedPreferencesTab.class",
    "org\cryptomator\ui\preferences\UpdatesPreferencesController`$1.class",
    "org\cryptomator\ui\preferences\UpdatesPreferencesController.class",
    "org\cryptomator\ui\preferences\UpdatesPreferencesController_Factory.class",
    "org\cryptomator\ui\preferences\VolumePreferencesController`$MountServiceConverter.class",
    "org\cryptomator\ui\preferences\VolumePreferencesController.class",
    "org\cryptomator\ui\preferences\VolumePreferencesController_Factory.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.preferences" -ReviewedClassFiles $reviewedPreferencesWindowClassFiles

$reviewedUnlockWindowClassFiles = @(
    "org\cryptomator\ui\unlock\UnlockComponent`$Factory.class",
    "org\cryptomator\ui\unlock\UnlockComponent.class",
    "org\cryptomator\ui\unlock\UnlockInvalidMountPointController`$ButtonAction.class",
    "org\cryptomator\ui\unlock\UnlockInvalidMountPointController`$ExceptionType.class",
    "org\cryptomator\ui\unlock\UnlockInvalidMountPointController.class",
    "org\cryptomator\ui\unlock\UnlockInvalidMountPointController_Factory.class",
    "org\cryptomator\ui\unlock\UnlockModule.class",
    "org\cryptomator\ui\unlock\UnlockModule_IllegalMountPointExceptionFactory`$InstanceHolder.class",
    "org\cryptomator\ui\unlock\UnlockModule_IllegalMountPointExceptionFactory.class",
    "org\cryptomator\ui\unlock\UnlockModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\unlock\UnlockModule_ProvideInvalidMountPointSceneFactory.class",
    "org\cryptomator\ui\unlock\UnlockModule_ProvideKeyLoadingStrategyFactory.class",
    "org\cryptomator\ui\unlock\UnlockModule_ProvideRestartRequiredSceneFactory.class",
    "org\cryptomator\ui\unlock\UnlockModule_ProvideStageFactory.class",
    "org\cryptomator\ui\unlock\UnlockModule_ProvideUnlockSuccessSceneFactory.class",
    "org\cryptomator\ui\unlock\UnlockRequiresRestartController.class",
    "org\cryptomator\ui\unlock\UnlockRequiresRestartController_Factory.class",
    "org\cryptomator\ui\unlock\UnlockScoped.class",
    "org\cryptomator\ui\unlock\UnlockSuccessController.class",
    "org\cryptomator\ui\unlock\UnlockSuccessController_Factory.class",
    "org\cryptomator\ui\unlock\UnlockWindow.class",
    "org\cryptomator\ui\unlock\UnlockWorkflow`$1.class",
    "org\cryptomator\ui\unlock\UnlockWorkflow.class",
    "org\cryptomator\ui\unlock\UnlockWorkflow_Factory.class"
)
$retainedUnlockCompatibilityClassFiles = @(
    "org\cryptomator\ui\unlock\UnlockCancelledException.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.ui.unlock" -RemovedClassFiles $reviewedUnlockWindowClassFiles -RetainedClassFiles $retainedUnlockCompatibilityClassFiles

$reviewedNotificationWindowClassFiles = @(
    "org\cryptomator\ui\notification\NotificationComponent`$Factory.class",
    "org\cryptomator\ui\notification\NotificationComponent.class",
    "org\cryptomator\ui\notification\NotificationController.class",
    "org\cryptomator\ui\notification\NotificationController_Factory.class",
    "org\cryptomator\ui\notification\NotificationModule`$1.class",
    "org\cryptomator\ui\notification\NotificationModule.class",
    "org\cryptomator\ui\notification\NotificationModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\notification\NotificationModule_ProvideNotificationSceneFactory.class",
    "org\cryptomator\ui\notification\NotificationModule_ProvideStageFactory.class",
    "org\cryptomator\ui\notification\NotificationScoped.class",
    "org\cryptomator\ui\notification\NotificationWindow.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.notification" -ReviewedClassFiles $reviewedNotificationWindowClassFiles

$reviewedEventViewWindowClassFiles = @(
    "org\cryptomator\ui\eventview\EventListCellController.class",
    "org\cryptomator\ui\eventview\EventListCellController_Factory.class",
    "org\cryptomator\ui\eventview\EventListCellFactory`$Cell.class",
    "org\cryptomator\ui\eventview\EventListCellFactory.class",
    "org\cryptomator\ui\eventview\EventListCellFactory_Factory.class",
    "org\cryptomator\ui\eventview\EventViewComponent`$Factory.class",
    "org\cryptomator\ui\eventview\EventViewComponent.class",
    "org\cryptomator\ui\eventview\EventViewController`$VaultConverter.class",
    "org\cryptomator\ui\eventview\EventViewController.class",
    "org\cryptomator\ui\eventview\EventViewController_Factory.class",
    "org\cryptomator\ui\eventview\EventViewModule.class",
    "org\cryptomator\ui\eventview\EventViewModule_ProvideEventViewerSceneFactory.class",
    "org\cryptomator\ui\eventview\EventViewModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\eventview\EventViewModule_ProvideStageFactory.class",
    "org\cryptomator\ui\eventview\EventViewScoped.class",
    "org\cryptomator\ui\eventview\EventViewWindow.class",
    "org\cryptomator\ui\eventview\UpdateEventViewController.class",
    "org\cryptomator\ui\eventview\UpdateEventViewController_Factory`$InstanceHolder.class",
    "org\cryptomator\ui\eventview\UpdateEventViewController_Factory.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ui.eventview" -ReviewedClassFiles $reviewedEventViewWindowClassFiles

$reviewedHealthPresentationClassFiles = @(
    "org\cryptomator\ui\health\CheckDetailController_Factory.class",
    "org\cryptomator\ui\health\CheckExecutor_Factory.class",
    "org\cryptomator\ui\health\CheckListCellController.class",
    "org\cryptomator\ui\health\CheckListCellController_Factory`$InstanceHolder.class",
    "org\cryptomator\ui\health\CheckListCellController_Factory.class",
    "org\cryptomator\ui\health\CheckListCellFactory`$Cell.class",
    "org\cryptomator\ui\health\CheckListCellFactory.class",
    "org\cryptomator\ui\health\CheckListCellFactory_Factory.class",
    "org\cryptomator\ui\health\CheckListController_Factory.class",
    "org\cryptomator\ui\health\CheckStateIconView`$1.class",
    "org\cryptomator\ui\health\CheckStateIconView.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideHealthCheckListSceneFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideHealthStartSceneFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideStageFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideUnlockWindowFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideWindowShowingChangeListenerFactory.class",
    "org\cryptomator\ui\health\ResultListCellFactory`$Cell.class",
    "org\cryptomator\ui\health\ResultListCellFactory.class",
    "org\cryptomator\ui\health\ResultListCellFactory_Factory.class",
    "org\cryptomator\ui\health\ResultListCellController_Factory.class",
    "org\cryptomator\ui\health\ReportWriter_Factory.class",
    "org\cryptomator\ui\health\ResultFixApplier_Factory.class",
    "org\cryptomator\ui\health\StartController_Factory.class"
)
$retainedHealthFunctionalClassFiles = @(
    "org\cryptomator\ui\health\Check`$CheckState.class",
    "org\cryptomator\ui\health\Check.class",
    "org\cryptomator\ui\health\CheckDetailController`$1.class",
    "org\cryptomator\ui\health\CheckDetailController`$FixStateStringifier.class",
    "org\cryptomator\ui\health\CheckDetailController`$SeverityStringifier.class",
    "org\cryptomator\ui\health\CheckDetailController.class",
    "org\cryptomator\ui\health\CheckExecutor`$CheckTask.class",
    "org\cryptomator\ui\health\CheckExecutor.class",
    "org\cryptomator\ui\health\CheckListController.class",
    "org\cryptomator\ui\health\HealthCheckComponent`$Builder.class",
    "org\cryptomator\ui\health\HealthCheckComponent.class",
    "org\cryptomator\ui\health\HealthCheckModule.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideAvailableChecksFactory`$InstanceHolder.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideAvailableChecksFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideKeyLoadingStrategyFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideMasterkeyRefFactory`$InstanceHolder.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideMasterkeyRefFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideSelectedCheckFactory`$InstanceHolder.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideSelectedCheckFactory.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideVaultConfigRefFactory`$InstanceHolder.class",
    "org\cryptomator\ui\health\HealthCheckModule_ProvideVaultConfigRefFactory.class",
    "org\cryptomator\ui\health\HealthCheckScoped.class",
    "org\cryptomator\ui\health\HealthCheckWindow.class",
    "org\cryptomator\ui\health\ReportWriter`$1.class",
    "org\cryptomator\ui\health\ReportWriter.class",
    "org\cryptomator\ui\health\Result`$FixState.class",
    "org\cryptomator\ui\health\Result.class",
    "org\cryptomator\ui\health\ResultFixApplier`$FixFailedException.class",
    "org\cryptomator\ui\health\ResultFixApplier.class",
    "org\cryptomator\ui\health\ResultListCellController`$1.class",
    "org\cryptomator\ui\health\ResultListCellController.class",
    "org\cryptomator\ui\health\StartController`$LoadingFailedException.class",
    "org\cryptomator\ui\health\StartController.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.ui.health" -RemovedClassFiles $reviewedHealthPresentationClassFiles -RetainedClassFiles $retainedHealthFunctionalClassFiles

$reviewedMasterkeyFilePresentationClassFiles = @(
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileController_Factory.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileModule_ProvideChooseMasterkeySceneFactory.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileModule_ProvideResultFactory`$InstanceHolder.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileModule_ProvideResultFactory.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryController_Factory.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryModule_ProvideResultFactory`$InstanceHolder.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryModule_ProvideResultFactory.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryModule_ProvideUnlockSceneFactory.class"
)
$retainedMasterkeyFileFunctionalClassFiles = @(
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileComponent`$Builder.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileComponent.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileController.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileModule.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\ChooseMasterkeyFileScoped.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\MasterkeyFileLoadingModule.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\MasterkeyFileLoadingModule_ProvideStoredPasswordFactory.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\MasterkeyFileLoadingStrategy.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\MasterkeyFileLoadingStrategy_Factory.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryComponent`$Builder.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryComponent.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryController.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryModule.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryResult.class",
    "org\cryptomator\ui\keyloading\masterkeyfile\PassphraseEntryScoped.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.ui.keyloading.masterkeyfile" -RemovedClassFiles $reviewedMasterkeyFilePresentationClassFiles -RetainedClassFiles $retainedMasterkeyFileFunctionalClassFiles

$reviewedKeyLoadingPresentationClassFiles = @(
    "org\cryptomator\ui\keyloading\KeyLoadingModule_ProvideFxmlLoaderFactoryFactory.class"
)
$retainedKeyLoadingFunctionalClassFiles = @(
    "org\cryptomator\ui\keyloading\KeyLoading.class",
    "org\cryptomator\ui\keyloading\KeyLoadingComponent`$Factory.class",
    "org\cryptomator\ui\keyloading\KeyLoadingComponent.class",
    "org\cryptomator\ui\keyloading\KeyLoadingModule.class",
    "org\cryptomator\ui\keyloading\KeyLoadingModule_ProvideKeyLoadingStrategyFactory.class",
    "org\cryptomator\ui\keyloading\KeyLoadingScoped.class",
    "org\cryptomator\ui\keyloading\KeyLoadingStrategy`$KeyLoadingStrategyUser.class",
    "org\cryptomator\ui\keyloading\KeyLoadingStrategy.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.ui.keyloading" -RemovedClassFiles $reviewedKeyLoadingPresentationClassFiles -RetainedClassFiles $retainedKeyLoadingFunctionalClassFiles

$reviewedMainWindowPresentationClassFiles = @(
    "org\cryptomator\ui\mainwindow\ActivityController.class",
    "org\cryptomator\ui\mainwindow\ActivityController_Factory.class",
    "org\cryptomator\ui\mainwindow\HowItWorksController_Factory.class",
    "org\cryptomator\ui\mainwindow\MainWindowController_Factory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideEmbeddedEventFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideEmbeddedPreferencesWindowFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideErrorStageFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideFxmlLoaderFactoryFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideMainSceneFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideMainWindowFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideSelectedPreferencesTabFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideSelectedVaultFactory`$InstanceHolder.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule_ProvideSelectedVaultFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowNavigation_Factory`$InstanceHolder.class",
    "org\cryptomator\ui\mainwindow\MainWindowNavigation_Factory.class",
    "org\cryptomator\ui\mainwindow\MainWindowSceneFactory_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultDetailController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultDetailLockedController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultDetailMissingVaultController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultDetailNeedsMigrationController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultDetailUnknownErrorController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultDetailUnlockedController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultListContextMenuController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultListController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultListCellController`$1.class",
    "org\cryptomator\ui\mainwindow\VaultListCellController.class",
    "org\cryptomator\ui\mainwindow\VaultListCellController_Factory.class",
    "org\cryptomator\ui\mainwindow\VaultListCellFactory`$Cell.class",
    "org\cryptomator\ui\mainwindow\VaultListCellFactory.class",
    "org\cryptomator\ui\mainwindow\VaultListCellFactory_Factory.class",
    "org\cryptomator\ui\mainwindow\WelcomeController_Factory.class"
)
$retainedMainWindowWorkflowClassFiles = @(
    "org\cryptomator\ui\mainwindow\DiagnosticCase`$Category.class",
    "org\cryptomator\ui\mainwindow\DiagnosticCase`$Confidence.class",
    "org\cryptomator\ui\mainwindow\DiagnosticCase`$DiagnosticMatch.class",
    "org\cryptomator\ui\mainwindow\DiagnosticCase.class",
    "org\cryptomator\ui\mainwindow\DiagnosticCatalog.class",
    "org\cryptomator\ui\mainwindow\HowItWorksController.class",
    "org\cryptomator\ui\mainwindow\MainWindow.class",
    "org\cryptomator\ui\mainwindow\MainWindowComponent`$Builder.class",
    "org\cryptomator\ui\mainwindow\MainWindowComponent.class",
    "org\cryptomator\ui\mainwindow\MainWindowController`$1.class",
    "org\cryptomator\ui\mainwindow\MainWindowController.class",
    "org\cryptomator\ui\mainwindow\MainWindowModule.class",
    "org\cryptomator\ui\mainwindow\MainWindowNavigation`$Destination.class",
    "org\cryptomator\ui\mainwindow\MainWindowNavigation.class",
    "org\cryptomator\ui\mainwindow\MainWindowSceneFactory.class",
    "org\cryptomator\ui\mainwindow\MainWindowScoped.class",
    "org\cryptomator\ui\mainwindow\VaultDetailController`$1.class",
    "org\cryptomator\ui\mainwindow\VaultDetailController.class",
    "org\cryptomator\ui\mainwindow\VaultDetailLockedController.class",
    "org\cryptomator\ui\mainwindow\VaultDetailMissingVaultController.class",
    "org\cryptomator\ui\mainwindow\VaultDetailNeedsMigrationController.class",
    "org\cryptomator\ui\mainwindow\VaultDetailUnknownErrorController.class",
    "org\cryptomator\ui\mainwindow\VaultDetailUnlockedController.class",
    "org\cryptomator\ui\mainwindow\VaultListContextMenuController.class",
    "org\cryptomator\ui\mainwindow\VaultListController`$1.class",
    "org\cryptomator\ui\mainwindow\VaultListController.class",
    "org\cryptomator\ui\mainwindow\WelcomeController.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.ui.mainwindow" -RemovedClassFiles $reviewedMainWindowPresentationClassFiles -RetainedClassFiles $retainedMainWindowWorkflowClassFiles

$reviewedFxApplicationRootClassFiles = @(
    "org\cryptomator\ui\fxapp\AppLaunchEventHandler_Factory.class",
    "org\cryptomator\ui\fxapp\AutoUnlocker_Factory.class",
    "org\cryptomator\ui\fxapp\FxApplication.class",
    "org\cryptomator\ui\fxapp\FxApplication_Factory.class",
    "org\cryptomator\ui\fxapp\FxApplicationComponent`$Builder.class",
    "org\cryptomator\ui\fxapp\FxApplicationComponent.class",
    "org\cryptomator\ui\fxapp\FxApplicationModule.class",
    "org\cryptomator\ui\fxapp\FxApplicationModule_ProvideAppearanceProviderFactory`$InstanceHolder.class",
    "org\cryptomator\ui\fxapp\FxApplicationModule_ProvideAppearanceProviderFactory.class",
    "org\cryptomator\ui\fxapp\FxApplicationModule_ProvideEventViewComponentFactory.class",
    "org\cryptomator\ui\fxapp\FxApplicationModule_ProvideQuitComponentFactory.class",
    "org\cryptomator\ui\fxapp\FxApplicationModule_ProvideTrayMenuComponentFactory.class",
    "org\cryptomator\ui\fxapp\FxApplicationStyle`$1.class",
    "org\cryptomator\ui\fxapp\FxApplicationStyle.class",
    "org\cryptomator\ui\fxapp\FxApplicationStyle_Factory.class",
    "org\cryptomator\ui\fxapp\FxApplicationTerminator_Factory.class",
    "org\cryptomator\ui\fxapp\FxApplicationWindows_Factory.class",
    "org\cryptomator\ui\fxapp\FxFSEventList_Factory.class",
    "org\cryptomator\ui\fxapp\FxNotificationManager_Factory.class"
)
$retainedFxApplicationOperationalClassFiles = @(
    "org\cryptomator\ui\fxapp\AppLaunchEventHandler`$1.class",
    "org\cryptomator\ui\fxapp\AppLaunchEventHandler.class",
    "org\cryptomator\ui\fxapp\AutoUnlocker.class",
    "org\cryptomator\ui\fxapp\ExitingQuitResponse.class",
    "org\cryptomator\ui\fxapp\FxApplicationScoped.class",
    "org\cryptomator\ui\fxapp\FxApplicationTerminator`$NoopQuitResponse.class",
    "org\cryptomator\ui\fxapp\FxApplicationTerminator.class",
    "org\cryptomator\ui\fxapp\FxApplicationWindows`$CachedLazy.class",
    "org\cryptomator\ui\fxapp\FxApplicationWindows.class",
    "org\cryptomator\ui\fxapp\FxFSEventList.class",
    "org\cryptomator\ui\fxapp\FxNotificationManager.class",
    "org\cryptomator\ui\fxapp\JfxRevealPathService.class",
    "org\cryptomator\ui\fxapp\JfxUiAppearanceProvider`$1.class",
    "org\cryptomator\ui\fxapp\JfxUiAppearanceProvider.class",
    "org\cryptomator\ui\fxapp\PrimaryStage.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.ui.fxapp" -RemovedClassFiles $reviewedFxApplicationRootClassFiles -RetainedClassFiles $retainedFxApplicationOperationalClassFiles

$reviewedLegacyLauncherClassFiles = @(
    "org\cryptomator\launcher\AppLaunchEvent.class",
    "org\cryptomator\launcher\AppLaunchEvent`$EventType.class",
    "org\cryptomator\launcher\Cryptomator.class",
    "org\cryptomator\launcher\Cryptomator`$MainApp.class",
    "org\cryptomator\launcher\Cryptomator_Factory.class",
    "org\cryptomator\launcher\CryptomatorComponent.class",
    "org\cryptomator\launcher\CryptomatorComponent`$Factory.class",
    "org\cryptomator\launcher\CryptomatorModule.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideAutostartProviderFactory.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideAutostartProviderFactory`$InstanceHolder.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideFileOpenRequestsFactory.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideFileOpenRequestsFactory`$InstanceHolder.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideLocalizationFactory.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideLocalizationFactory`$InstanceHolder.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideTrayIntegrationProviderFactory.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideTrayIntegrationProviderFactory`$InstanceHolder.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideVaultMutationDispatcherFactory.class",
    "org\cryptomator\launcher\CryptomatorModule_ProvideVaultMutationDispatcherFactory`$InstanceHolder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$AddVaultWizardComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$AddVaultWizardComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$AddVaultWizardComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ChangePasswordComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ChangePasswordComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ChangePasswordComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ConvertVaultComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ConvertVaultComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ConvertVaultComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$CryptomatorComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$CryptomatorComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$DecryptNameComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$DecryptNameComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$DecryptNameComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ErrorComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ErrorComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ErrorComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$EventViewComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$EventViewComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$EventViewComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$Factory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$FxApplicationComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$FxApplicationComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$FxApplicationComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$HealthCheckComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$HealthCheckComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$HealthCheckComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$LockComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$LockComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$LockComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$MainWindowComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$MainWindowComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$MainWindowComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$MigrationComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$MigrationComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$MigrationComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$NotificationComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$NotificationComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$NotificationComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf_ForgetPasswordComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf_ForgetPasswordComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf_ForgetPasswordComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf2_ForgetPasswordComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf2_ForgetPasswordComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf2_ForgetPasswordComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf3_ForgetPasswordComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf3_ForgetPasswordComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuf3_ForgetPasswordComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuk_KeyLoadingComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuk_KeyLoadingComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuk_KeyLoadingComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuk2_KeyLoadingComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuk2_KeyLoadingComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocuk2_KeyLoadingComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm_ChooseMasterkeyFileComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm_ChooseMasterkeyFileComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm_ChooseMasterkeyFileComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm_PassphraseEntryComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm_PassphraseEntryComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm_PassphraseEntryComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm2_ChooseMasterkeyFileComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm2_ChooseMasterkeyFileComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm2_ChooseMasterkeyFileComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm2_PassphraseEntryComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm2_PassphraseEntryComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocukm2_PassphraseEntryComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur_RecoveryKeyComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur_RecoveryKeyComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur_RecoveryKeyComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur2_RecoveryKeyComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur2_RecoveryKeyComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur2_RecoveryKeyComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur3_RecoveryKeyComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur3_RecoveryKeyComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur3_RecoveryKeyComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur4_RecoveryKeyComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur4_RecoveryKeyComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ocur4_RecoveryKeyComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$PreferencesComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$PreferencesComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$PreferencesComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$QuitComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$QuitComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$QuitComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ShareVaultComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ShareVaultComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$ShareVaultComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$TrayMenuComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$TrayMenuComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$TrayMenuComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$UnlockComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$UnlockComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$UnlockComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$UpdateReminderComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$UpdateReminderComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$UpdateReminderComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultOptionsComponentFactory.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultOptionsComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultOptionsComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultStatisticsComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultStatisticsComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$VaultStatisticsComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$WrongFileAlertComponentBuilder.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$WrongFileAlertComponentImpl.class",
    "org\cryptomator\launcher\DaggerCryptomatorComponent`$WrongFileAlertComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\FileOpenRequestHandler.class",
    "org\cryptomator\launcher\FileOpenRequestHandler_Factory.class",
    "org\cryptomator\launcher\IpcMessageHandler.class",
    "org\cryptomator\launcher\IpcMessageHandler_Factory.class"
)
$retainedNativeLauncherClassFiles = @(
    "org\cryptomator\launcher\AdminPropertiesFactory.class",
    "org\cryptomator\launcher\DaggerNativeBackendComponent.class",
    "org\cryptomator\launcher\DaggerNativeBackendComponent`$Builder.class",
    "org\cryptomator\launcher\DaggerNativeBackendComponent`$NativeBackendComponentImpl.class",
    "org\cryptomator\launcher\DaggerNativeBackendComponent`$NativeBackendComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\DaggerNativeBackendComponent`$VaultComponentFactory.class",
    "org\cryptomator\launcher\DaggerNativeBackendComponent`$VaultComponentImpl.class",
    "org\cryptomator\launcher\DaggerNativeBackendComponent`$VaultComponentImpl`$SwitchingProvider.class",
    "org\cryptomator\launcher\EventualLogger.class",
    "org\cryptomator\launcher\NativeBackendComponent.class",
    "org\cryptomator\launcher\NativeBackendMain.class",
    "org\cryptomator\launcher\NativeBackendModule.class",
    "org\cryptomator\launcher\NativeBackendModule`$1.class",
    "org\cryptomator\launcher\NativeBackendModule_ProvideLocalizationFactory.class",
    "org\cryptomator\launcher\NativeBackendModule_ProvideLocalizationFactory`$InstanceHolder.class",
    "org\cryptomator\launcher\NativeBackendModule_ProvideVaultListFactory.class",
    "org\cryptomator\launcher\NativeBackendModule_ProvideVaultListFactory`$InstanceHolder.class",
    "org\cryptomator\launcher\NativeBackendModule_ProvideVaultListPersistenceFactory.class",
    "org\cryptomator\launcher\NativeBackendModule_ProvideVaultMutationDispatcherFactory.class",
    "org\cryptomator\launcher\NativeBackendModule_ProvideVaultMutationDispatcherFactory`$InstanceHolder.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.launcher" -RemovedClassFiles $reviewedLegacyLauncherClassFiles -RetainedClassFiles $retainedNativeLauncherClassFiles

$reviewedCommonPresentationClassFiles = @(
    "org\cryptomator\ui\common\DefaultSceneFactory_Factory`$InstanceHolder.class",
    "org\cryptomator\ui\common\DefaultSceneFactory_Factory.class",
    "org\cryptomator\ui\common\FontLoader`$FontLoaderException.class",
    "org\cryptomator\ui\common\FontLoader.class",
    "org\cryptomator\ui\common\StageFactory_Factory.class",
    "org\cryptomator\ui\common\StageInitializer_Factory`$InstanceHolder.class",
    "org\cryptomator\ui\common\StageInitializer_Factory.class",
    "org\cryptomator\ui\common\SystemBarUtil`$Placement.class",
    "org\cryptomator\ui\common\SystemBarUtil.class",
    "org\cryptomator\ui\common\VaultService_Factory.class"
)
$retainedCommonWorkflowClassFiles = @(
    "org\cryptomator\ui\common\Animations`$1.class",
    "org\cryptomator\ui\common\Animations.class",
    "org\cryptomator\ui\common\AutoAnimator`$Builder.class",
    "org\cryptomator\ui\common\AutoAnimator.class",
    "org\cryptomator\ui\common\DefaultSceneFactory.class",
    "org\cryptomator\ui\common\FxController.class",
    "org\cryptomator\ui\common\FxControllerKey.class",
    "org\cryptomator\ui\common\FxmlFile.class",
    "org\cryptomator\ui\common\FxmlLoaderFactory.class",
    "org\cryptomator\ui\common\FxmlScene.class",
    "org\cryptomator\ui\common\MicroInteractionSupport.class",
    "org\cryptomator\ui\common\StageFactory.class",
    "org\cryptomator\ui\common\StageInitializer.class",
    "org\cryptomator\ui\common\Tasks`$ErrorHandler.class",
    "org\cryptomator\ui\common\Tasks`$RestartingService.class",
    "org\cryptomator\ui\common\Tasks`$TaskBuilder.class",
    "org\cryptomator\ui\common\Tasks`$TaskImpl.class",
    "org\cryptomator\ui\common\Tasks`$VoidCallable.class",
    "org\cryptomator\ui\common\Tasks.class",
    "org\cryptomator\ui\common\VaultKindUrls.class",
    "org\cryptomator\ui\common\VaultService`$LockVaultTask.class",
    "org\cryptomator\ui\common\VaultService`$RevealVaultTask.class",
    "org\cryptomator\ui\common\VaultService`$WaitForTasksTask.class",
    "org\cryptomator\ui\common\VaultService.class",
    "org\cryptomator\ui\common\WeakBindings`$1.class",
    "org\cryptomator\ui\common\WeakBindings`$2.class",
    "org\cryptomator\ui\common\WeakBindings`$3.class",
    "org\cryptomator\ui\common\WeakBindings`$4.class",
    "org\cryptomator\ui\common\WeakBindings.class",
    "org\cryptomator\ui\common\WindowsCaptionSupport.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.ui.common" -RemovedClassFiles $reviewedCommonPresentationClassFiles -RetainedClassFiles $retainedCommonWorkflowClassFiles

$reviewedNonWindowsSslContextClassFiles = @(
    "org\cryptomator\networking\SSLContextWithMacKeychain`$1.class",
    "org\cryptomator\networking\SSLContextWithMacKeychain`$2.class",
    "org\cryptomator\networking\SSLContextWithMacKeychain.class",
    "org\cryptomator\networking\SSLContextWithPKCS12TrustStore.class"
)
$retainedWindowsSslContextClassFiles = @(
    "org\cryptomator\networking\CombinedKeyStoreSpi.class",
    "org\cryptomator\networking\SSLContextDifferentTrustStoreBase.class",
    "org\cryptomator\networking\SSLContextProvider`$SSLContextBuildException.class",
    "org\cryptomator\networking\SSLContextProvider.class",
    "org\cryptomator\networking\SSLContextWithWindowsCertStore`$1.class",
    "org\cryptomator\networking\SSLContextWithWindowsCertStore`$2.class",
    "org\cryptomator\networking\SSLContextWithWindowsCertStore.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.networking" -RemovedClassFiles $reviewedNonWindowsSslContextClassFiles -RetainedClassFiles $retainedWindowsSslContextClassFiles

$reviewedLegacyIpcClassFiles = @(
    "org\cryptomator\ipc\Client.class",
    "org\cryptomator\ipc\HandleLaunchArgsMessage.class",
    "org\cryptomator\ipc\IpcCommunicator.class",
    "org\cryptomator\ipc\IpcMessage`$MessageType.class",
    "org\cryptomator\ipc\IpcMessage.class",
    "org\cryptomator\ipc\IpcMessageListener.class",
    "org\cryptomator\ipc\LoopbackCommunicator.class",
    "org\cryptomator\ipc\RevealRunningAppMessage.class",
    "org\cryptomator\ipc\Server.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator.ipc" -ReviewedClassFiles $reviewedLegacyIpcClassFiles

$reviewedTestSupportClassFiles = @(
    "org\cryptomator\JavaFXUtil.class"
)
Remove-ReviewedClassPackage -PackageName "org.cryptomator" -ReviewedClassFiles $reviewedTestSupportClassFiles

$reviewedUnusedNativeFactoryClassFiles = @(
    "org\cryptomator\nativeui\NativeBackendApplication_Factory.class",
    "org\cryptomator\nativeui\NativeBackendTerminator_Factory`$InstanceHolder.class",
    "org\cryptomator\nativeui\NativeBackendTerminator_Factory.class",
    "org\cryptomator\nativeui\NativeMountSettings_Factory.class",
    "org\cryptomator\nativeui\NativeUiBridge_Factory.class",
    "org\cryptomator\nativeui\NativeUiProtocol_Factory.class",
    "org\cryptomator\nativeui\NativeVaultCreator_Factory.class",
    "org\cryptomator\nativeui\NativeVaultOperations_Factory.class",
    "org\cryptomator\nativeui\VaultListSnapshotProvider_Factory.class"
)
$retainedNativeEngineClassFiles = @(
    "org\cryptomator\nativeui\NativeBackendApplication.class",
    "org\cryptomator\nativeui\NativeBackendTerminator.class",
    "org\cryptomator\nativeui\NativeMountSettings`$NativeMountService.class",
    "org\cryptomator\nativeui\NativeMountSettings`$NativeMountSettingsResult.class",
    "org\cryptomator\nativeui\NativeMountSettings.class",
    "org\cryptomator\nativeui\NativeUiBridge.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$MountSettingsSource.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$NativeUiRequest.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$NativeUiResponse.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$ShutdownSource.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$VaultCommandSource.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$VaultConnectSource.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$VaultCreateSource.class",
    "org\cryptomator\nativeui\NativeUiProtocol`$VaultSummarySource.class",
    "org\cryptomator\nativeui\NativeUiProtocol.class",
    "org\cryptomator\nativeui\NativeVaultCreator`$NativeCreateResult.class",
    "org\cryptomator\nativeui\NativeVaultCreator.class",
    "org\cryptomator\nativeui\NativeVaultOperations`$FileNameMapping.class",
    "org\cryptomator\nativeui\NativeVaultOperations`$NativeCommandResult.class",
    "org\cryptomator\nativeui\NativeVaultOperations.class",
    "org\cryptomator\nativeui\VaultListSnapshotProvider.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.nativeui" -RemovedClassFiles $reviewedUnusedNativeFactoryClassFiles -RetainedClassFiles $retainedNativeEngineClassFiles

$reviewedUnusedEventFactoryClassFiles = @(
    "org\cryptomator\event\FileSystemEventAggregator_Factory`$InstanceHolder.class",
    "org\cryptomator\event\FileSystemEventAggregator_Factory.class",
    "org\cryptomator\event\NotificationManager_Factory`$InstanceHolder.class",
    "org\cryptomator\event\NotificationManager_Factory.class"
)
$retainedEventClassFiles = @(
    "org\cryptomator\event\Answer`$DoNothing.class",
    "org\cryptomator\event\Answer`$DoSomething.class",
    "org\cryptomator\event\Answer.class",
    "org\cryptomator\event\FileSystemEventAggregator.class",
    "org\cryptomator\event\FSEventBucket.class",
    "org\cryptomator\event\FSEventBucketContent.class",
    "org\cryptomator\event\NotificationHandler.class",
    "org\cryptomator\event\NotificationManager.class",
    "org\cryptomator\event\VaultEvent.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.event" -RemovedClassFiles $reviewedUnusedEventFactoryClassFiles -RetainedClassFiles $retainedEventClassFiles

$reviewedUnusedLoggingFactoryClassFiles = @(
    "org\cryptomator\logging\DebugMode_Factory.class"
)
$retainedLoggingClassFiles = @(
    "org\cryptomator\logging\DebugMode.class",
    "org\cryptomator\logging\LaunchAndSizeBasedTriggeringPolicy.class",
    "org\cryptomator\logging\LaunchBasedTriggeringPolicy.class",
    "org\cryptomator\logging\LogbackConfigurator.class",
    "org\cryptomator\logging\LogbackConfiguratorFactory`$1Holder.class",
    "org\cryptomator\logging\LogbackConfiguratorFactory.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.logging" -RemovedClassFiles $reviewedUnusedLoggingFactoryClassFiles -RetainedClassFiles $retainedLoggingClassFiles

$reviewedUnusedDirectCommonFactoryClassFiles = @(
    "org\cryptomator\common\CommonsModule_ProvideExecutorServiceFactory.class",
    "org\cryptomator\common\CommonsModule_ProvideLicensePublicKeyFactory`$InstanceHolder.class",
    "org\cryptomator\common\CommonsModule_ProvideLicensePublicKeyFactory.class",
    "org\cryptomator\common\CommonsModule_ProvideRevealPathServiceFactory`$InstanceHolder.class",
    "org\cryptomator\common\CommonsModule_ProvideRevealPathServiceFactory.class",
    "org\cryptomator\common\LicenseChecker_Factory.class",
    "org\cryptomator\common\LicenseHolder_Factory.class"
)
$retainedDirectCommonClassFiles = @(
    "org\cryptomator\common\CatchingExecutors`$CatchingScheduledThreadPoolExecutor.class",
    "org\cryptomator\common\CatchingExecutors`$CatchingThreadPoolExecutor.class",
    "org\cryptomator\common\CatchingExecutors.class",
    "org\cryptomator\common\CommonsModule.class",
    "org\cryptomator\common\CommonsModule_ProvideCSPRNGFactory`$InstanceHolder.class",
    "org\cryptomator\common\CommonsModule_ProvideCSPRNGFactory.class",
    "org\cryptomator\common\CommonsModule_ProvideEnvironmentFactory`$InstanceHolder.class",
    "org\cryptomator\common\CommonsModule_ProvideEnvironmentFactory.class",
    "org\cryptomator\common\CommonsModule_ProvideMasterkeyFileAccessFactory.class",
    "org\cryptomator\common\CommonsModule_ProvideScheduledExecutorServiceFactory.class",
    "org\cryptomator\common\CommonsModule_ProvideSettingsFactory.class",
    "org\cryptomator\common\Constants.class",
    "org\cryptomator\common\ConsumerThrowingException.class",
    "org\cryptomator\common\Environment`$1Holder.class",
    "org\cryptomator\common\Environment.class",
    "org\cryptomator\common\ErrorCode.class",
    "org\cryptomator\common\FilesystemOwnerSupplier.class",
    "org\cryptomator\common\LicenseChecker.class",
    "org\cryptomator\common\LicenseHolder.class",
    "org\cryptomator\common\Nullable.class",
    "org\cryptomator\common\ObservableUtil.class",
    "org\cryptomator\common\Passphrase.class",
    "org\cryptomator\common\PropertiesDecorator.class",
    "org\cryptomator\common\RunnableThrowingException.class",
    "org\cryptomator\common\ShutdownHook`$OrderedTask.class",
    "org\cryptomator\common\ShutdownHook.class",
    "org\cryptomator\common\ShutdownHook_Factory`$InstanceHolder.class",
    "org\cryptomator\common\ShutdownHook_Factory.class",
    "org\cryptomator\common\SubstitutingProperties`$Source.class",
    "org\cryptomator\common\SubstitutingProperties.class",
    "org\cryptomator\common\SupplierThrowingException.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.common" -RemovedClassFiles $reviewedUnusedDirectCommonFactoryClassFiles -RetainedClassFiles $retainedDirectCommonClassFiles

$reviewedUnusedSettingsFactoryClassFiles = @(
    "org\cryptomator\common\settings\DeviceKey_Factory.class",
    "org\cryptomator\common\settings\LegacySettingsAdapter_Factory.class",
    "org\cryptomator\common\settings\SettingsProvider_Factory.class"
)
$retainedSettingsClassFiles = @(
    "org\cryptomator\common\settings\DeviceKey`$DeviceKeyRetrievalException.class",
    "org\cryptomator\common\settings\DeviceKey.class",
    "org\cryptomator\common\settings\EngineSettings.class",
    "org\cryptomator\common\settings\LegacySettingsAdapter.class",
    "org\cryptomator\common\settings\LegacyVaultSettingsProperties`$1.class",
    "org\cryptomator\common\settings\LegacyVaultSettingsProperties.class",
    "org\cryptomator\common\settings\Settings.class",
    "org\cryptomator\common\settings\SettingsJson.class",
    "org\cryptomator\common\settings\SettingsProvider.class",
    "org\cryptomator\common\settings\UiTheme.class",
    "org\cryptomator\common\settings\VaultSettings.class",
    "org\cryptomator\common\settings\VaultSettingsData`$Field.class",
    "org\cryptomator\common\settings\VaultSettingsData`$Listener.class",
    "org\cryptomator\common\settings\VaultSettingsData.class",
    "org\cryptomator\common\settings\VaultSettingsJson.class",
    "org\cryptomator\common\settings\WhenUnlocked.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.common.settings" -RemovedClassFiles $reviewedUnusedSettingsFactoryClassFiles -RetainedClassFiles $retainedSettingsClassFiles

$reviewedUnusedMountFactoryClassFiles = @(
    "org\cryptomator\common\mount\Mounter_Factory.class",
    "org\cryptomator\common\mount\MountModule_ProvideDefaultMountServiceFactory.class",
    "org\cryptomator\common\mount\MountServiceSelector_Factory.class",
    "org\cryptomator\common\mount\WindowsDriveLetters_Factory`$InstanceHolder.class",
    "org\cryptomator\common\mount\WindowsDriveLetters_Factory.class"
)
$retainedMountClassFiles = @(
    "org\cryptomator\common\mount\ConflictingMountServiceException.class",
    "org\cryptomator\common\mount\HideawayNotDirectoryException.class",
    "org\cryptomator\common\mount\IllegalMountPointException.class",
    "org\cryptomator\common\mount\Mounter`$1.class",
    "org\cryptomator\common\mount\Mounter`$MountHandle.class",
    "org\cryptomator\common\mount\Mounter`$SettledMounter.class",
    "org\cryptomator\common\mount\Mounter.class",
    "org\cryptomator\common\mount\MountModule.class",
    "org\cryptomator\common\mount\MountModule_ProvideSetOfUsedMountServicesFactory`$InstanceHolder.class",
    "org\cryptomator\common\mount\MountModule_ProvideSetOfUsedMountServicesFactory.class",
    "org\cryptomator\common\mount\MountModule_ProvideSupportedMountServicesFactory`$InstanceHolder.class",
    "org\cryptomator\common\mount\MountModule_ProvideSupportedMountServicesFactory.class",
    "org\cryptomator\common\mount\MountPointCleanupFailedException.class",
    "org\cryptomator\common\mount\MountPointInUseException.class",
    "org\cryptomator\common\mount\MountPointNotEmptyDirectoryException.class",
    "org\cryptomator\common\mount\MountPointNotExistingException.class",
    "org\cryptomator\common\mount\MountPointNotSupportedException.class",
    "org\cryptomator\common\mount\MountServiceSelector.class",
    "org\cryptomator\common\mount\MountWithinParentUtil`$MountPointState.class",
    "org\cryptomator\common\mount\MountWithinParentUtil.class",
    "org\cryptomator\common\mount\WindowsDriveLetters.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.common.mount" -RemovedClassFiles $reviewedUnusedMountFactoryClassFiles -RetainedClassFiles $retainedMountClassFiles

$reviewedUnusedVaultFactoryClassFiles = @(
    "org\cryptomator\common\vaults\AutoLocker_Factory.class",
    "org\cryptomator\common\vaults\LegacyVaultStateObservable_Factory.class",
    "org\cryptomator\common\vaults\VaultListManager_Factory.class",
    "org\cryptomator\common\vaults\VaultListModule_ProvideVaultListFactory.class",
    "org\cryptomator\common\vaults\VaultListModule_ProvideVaultListPersistenceFactory.class",
    "org\cryptomator\common\vaults\VaultListModule_ProvideVaultListViewFactory.class",
    "org\cryptomator\common\vaults\VaultListSnapshotMapper_Factory`$InstanceHolder.class",
    "org\cryptomator\common\vaults\VaultListSnapshotMapper_Factory.class",
    "org\cryptomator\common\vaults\VaultState_Factory.class"
)
$retainedVaultClassFiles = @(
    "org\cryptomator\common\vaults\AutoLocker.class",
    "org\cryptomator\common\vaults\LegacyVaultExceptionProperty.class",
    "org\cryptomator\common\vaults\LegacyVaultExceptionProperty_Factory.class",
    "org\cryptomator\common\vaults\LegacyVaultObservables.class",
    "org\cryptomator\common\vaults\LegacyVaultStateObservable.class",
    "org\cryptomator\common\vaults\LegacyVaultStatsObservable.class",
    "org\cryptomator\common\vaults\LegacyVaultStatsObservable_Factory.class",
    "org\cryptomator\common\vaults\NotAVaultDirectoryException`$Reason.class",
    "org\cryptomator\common\vaults\NotAVaultDirectoryException.class",
    "org\cryptomator\common\vaults\PerVault.class",
    "org\cryptomator\common\vaults\Vault.class",
    "org\cryptomator\common\vaults\Vault_Factory.class",
    "org\cryptomator\common\vaults\VaultComponent`$Factory.class",
    "org\cryptomator\common\vaults\VaultComponent.class",
    "org\cryptomator\common\vaults\VaultConfigCache.class",
    "org\cryptomator\common\vaults\VaultExceptionState`$Listener.class",
    "org\cryptomator\common\vaults\VaultExceptionState.class",
    "org\cryptomator\common\vaults\VaultExceptionState_Factory.class",
    "org\cryptomator\common\vaults\VaultListChangeListener.class",
    "org\cryptomator\common\vaults\VaultListManager`$1.class",
    "org\cryptomator\common\vaults\VaultListManager.class",
    "org\cryptomator\common\vaults\VaultListModule`$1.class",
    "org\cryptomator\common\vaults\VaultListModule.class",
    "org\cryptomator\common\vaults\VaultListPersistence.class",
    "org\cryptomator\common\vaults\VaultListSnapshotMapper.class",
    "org\cryptomator\common\vaults\VaultModule.class",
    "org\cryptomator\common\vaults\VaultModule_ProvideCryptoFileSystemReferenceFactory.class",
    "org\cryptomator\common\vaults\VaultMutationDispatcher.class",
    "org\cryptomator\common\vaults\VaultRegistry.class",
    "org\cryptomator\common\vaults\VaultState`$Listener.class",
    "org\cryptomator\common\vaults\VaultState`$Value.class",
    "org\cryptomator\common\vaults\VaultState.class",
    "org\cryptomator\common\vaults\VaultStats`$Listener.class",
    "org\cryptomator\common\vaults\VaultStats`$NativeSnapshot.class",
    "org\cryptomator\common\vaults\VaultStats`$Snapshot.class",
    "org\cryptomator\common\vaults\VaultStats.class",
    "org\cryptomator\common\vaults\VaultStats_Factory.class",
    "org\cryptomator\common\vaults\VaultSummary.class"
)
Remove-ReviewedClassSlice -PackageName "org.cryptomator.common.vaults" -RemovedClassFiles $reviewedUnusedVaultFactoryClassFiles -RetainedClassFiles $retainedVaultClassFiles

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

# jlink includes Windows linker import libraries for native applications that
# embed or link against AWT/the JVM. VaultKind launches runtime\bin\java.exe
# as a separate process and does not compile or link native code in the staged
# layout. Keep this exact and fail closed if a future JDK changes the set.
$reviewedRuntimeImportLibraries = @("jawt.lib", "jvm.lib")
$runtimeLibraryDirectory = Join-Path $runtimeTarget "lib"
$actualRuntimeImportLibraries = @(Get-ChildItem -LiteralPath $runtimeLibraryDirectory -File -Filter "*.lib" | Select-Object -ExpandProperty Name)
$runtimeImportLibraryDifference = @(Compare-Object -ReferenceObject $reviewedRuntimeImportLibraries -DifferenceObject $actualRuntimeImportLibraries)
if ($runtimeImportLibraryDifference.Count -ne 0) {
    throw "The jlink runtime import-library set changed. Review it before updating the exact release exclusion."
}
foreach ($runtimeImportLibrary in $reviewedRuntimeImportLibraries) {
    Remove-Item -LiteralPath (Join-Path $runtimeLibraryDirectory $runtimeImportLibrary) -Force
}

# The native host launches javaw.exe, while the isolated engine probe uses
# java.exe. The remaining jlink launchers are standalone administration,
# diagnostics, and accessibility tools that VaultKind never invokes.
$reviewedRuntimeExecutables = @(
    "jabswitch.exe",
    "jaccessinspector.exe",
    "jaccesswalker.exe",
    "java.exe",
    "javaw.exe",
    "jfr.exe",
    "keytool.exe"
)
$removedRuntimeToolExecutables = @(
    "jabswitch.exe",
    "jaccessinspector.exe",
    "jaccesswalker.exe",
    "jfr.exe",
    "keytool.exe"
)
$runtimeBinaryDirectory = Join-Path $runtimeTarget "bin"
$actualRuntimeExecutables = @(Get-ChildItem -LiteralPath $runtimeBinaryDirectory -File -Filter "*.exe" | Select-Object -ExpandProperty Name)
$runtimeExecutableDifference = @(Compare-Object -ReferenceObject $reviewedRuntimeExecutables -DifferenceObject $actualRuntimeExecutables)
if ($runtimeExecutableDifference.Count -ne 0) {
    throw "The jlink runtime executable set changed. Review it before updating the exact release exclusion."
}
foreach ($runtimeToolExecutable in $removedRuntimeToolExecutables) {
    Remove-Item -LiteralPath (Join-Path $runtimeBinaryDirectory $runtimeToolExecutable) -Force
}

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
