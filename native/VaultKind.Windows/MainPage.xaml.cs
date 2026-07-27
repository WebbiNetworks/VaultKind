using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using System.Text.Json;
using VaultKind_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VaultKind_Windows;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly IVaultBackend backend = new LocalSocketVaultBackend();
    private readonly SignatureSoundService signatureSounds = new();
    private readonly List<Button> vaultButtons = [];
    private VaultSummary? activeVault;
    private VaultSummary? createdVault;
    private string? selectedCreateVaultParentPath;
    private string? selectedConnectVaultPath;
    private IReadOnlyList<VaultSummary> knownVaults = [];
    private readonly List<SessionActivity> activityHistory = [];
    private readonly HashSet<string> expandedActivityCategories = [];
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? navigationFocusTimer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? keyboardEntryFocusTimer;
    private bool initializingPreferences = true;
    private bool loadingMountServices;
    private readonly Dictionary<DependencyObject, double> baselineFontSizes = [];
    private bool useLargerText;
    private bool textScaleRefreshQueued;
    private static readonly string ActivityHistoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaultKind", "activity.json");
    private static readonly string LearningProgressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaultKind", "learning-progress.json");
    private readonly HashSet<string> viewedLearningTopics = [];
    private readonly Dictionary<string, double> learningTopicScrollOffsets = [];
    private readonly List<LearningSectionViewEntry> learningSectionViewEntries = [];
    private readonly Dictionary<string, string?> selectedLearningSections = [];
    private string? highlightedLearningAnswerTopic;
    private string? highlightedLearningAnswerSection;
    private TextBlock? learningSectionResultCount;
    private TextBlock? learningSectionNoResults;
    private Button? learningCopyGuidanceButton;
    private Button? learningSaveGuidanceButton;
    private TextBlock? learningCopyGuidanceStatus;
    private string selectedFaqCategory = "all";
    private bool restoreFaqCategoryFocusAfterRender;
    private string selectedLearningTopic = "how";
    private string selectedAssistantCategory = "all";
    private string? doctorAssistantCaseId;
    private string doctorAssistantEvidence = string.Empty;
    private IReadOnlyList<DoctorCheck> latestDoctorChecks = [];
    private DateTime? latestDoctorRunAt;
    private bool doctorRunInProgress;
    private string? recoveryTargetVaultId;
    private bool recoveryOpenedFromVaultManagement;
    private string? doctorFocusVaultId;
    private CancellationTokenSource? sensitiveClipboardClearCancellation;

    private static readonly IReadOnlyList<AssistantCase> AssistantCases =
    [
        new("VK-0001", "startup", "VaultKind settings cannot be loaded", "The local settings file may be locked, malformed, or left incomplete after an interrupted write.", "1. Confirm another VaultKind process is not running.\n2. Verify the settings location is writable.\n3. Preserve a copy of the current settings file before changing it.", "Close duplicate processes and restart VaultKind. If parsing still fails, restore a known-good settings backup or reset only the damaged preferences.", "settings corrupt invalid preferences startup settings cannot start"),
        new("VK-0002", "startup", "A required desktop component is unavailable", "The runtime, a native library, or another packaged dependency may be missing or blocked.", "1. Confirm the installation completed.\n2. Check whether security software quarantined a VaultKind file.\n3. Verify that Windows is supported.", "Restore a blocked file only when its VaultKind package source is trusted, then repair or reinstall the application.", "missing dll missing dependency runtime blocked startup dependency"),
        new("VK-1001", "vault", "The vault password was rejected", "The password may be incorrect, entered for a different vault, or affected by keyboard state.", "1. Confirm Caps Lock and keyboard layout.\n2. Confirm the selected vault name and location.\n3. Retry carefully without repeatedly guessing.", "Use the correct password. If it has been forgotten, use the saved recovery key; VaultKind cannot reset or reveal the old password.", "wrong password invalid password cannot unlock passphrase caps lock"),
        new("VK-1002", "vault", "The vault storage folder is unavailable", "The folder may have moved, an external device may be disconnected, or cloud synchronization may not have restored it locally.", "1. Verify the shown path exists.\n2. Reconnect external or network storage.\n3. Wait for the cloud folder to finish synchronizing.", "Use Add Vault, choose Connect an existing vault, and select the folder containing the vault configuration file.", "vault missing folder moved vault not found external drive missing relink"),
        new("VK-1003", "vault", "The vault configuration is missing or invalid", "The configuration file may be absent, incomplete, corrupted, or from an unsupported vault layout.", "1. Do not modify encrypted data files.\n2. Look for the expected vault configuration and its backups.\n3. Confirm the vault version is supported.", "Restore the configuration from a trusted backup or use the appropriate recovery workflow. Never replace it with a file from another vault.", "config invalid vault.cryptomator masterkey.cryptomator corrupt config missing master key"),
        new("VK-1004", "vault", "The vault is already connected", "VaultKind already has an entry pointing to the selected vault location.", "1. Return to Dashboard.\n2. Locate the existing vault entry.\n3. Confirm its path matches the folder you selected.", "Use the existing entry. Adding it again would create a duplicate and does not repair or resynchronize vault data.", "already connected duplicate vault connected to vaultkind"),
        new("VK-2001", "filesystem", "The vault location is not writable", "Folder permissions, read-only media, security controls, or a disconnected account may be blocking writes.", "1. Confirm the drive is not read-only.\n2. Test creating a harmless file beside the vault.\n3. Check the current account's folder permissions.", "Restore write access for the current user or move the encrypted vault to a supported writable location while it is locked.", "read only permission denied access denied not writable folder permissions"),
        new("VK-2002", "filesystem", "The storage location is full", "The encrypted storage, system drive, or temporary-file location may lack enough free space.", "1. Check free space on encrypted storage and the system drive.\n2. Check cloud quota.\n3. Allow room for synchronization overhead.", "Free sufficient space or increase the storage quota, then allow synchronization to finish before retrying.", "disk full no space storage full insufficient space quota"),
        new("VK-2003", "filesystem", "The vault is still in use", "An application or File Explorer window may still hold a file handle inside the unlocked virtual drive.", "1. Save open work.\n2. Close documents and windows using the vault.\n3. Pause tools scanning or indexing the unlocked drive.", "Close the process using the vault and try Lock again. Avoid forcing removal while writes are active.", "file locked cannot lock vault busy in use open files"),
        new("VK-2004", "filesystem", "Cloud synchronization is incomplete or conflicted", "The cloud client may be offline, paused, out of quota, or resolving conflicting encrypted files.", "1. Keep the vault locked.\n2. Confirm the cloud client is online.\n3. Resolve quota or conflict warnings.\n4. Wait for synchronization to finish.", "Retry only after the provider reports synchronization complete. Do not open the same vault on another device during conflict resolution.", "cloud unavailable sync conflict synchronization problem dropbox onedrive google drive"),
        new("VK-2005", "filesystem", "The virtual drive could not be mounted", "The selected mounting service may be unavailable, misconfigured, or unable to claim the requested drive location.", "1. Open Preferences and review Virtual Drive.\n2. Confirm an available mounting service is selected.\n3. Check whether the requested drive letter or path is already in use.", "Select an available mounting service or mount location, then lock and unlock the vault again.", "mount failed virtual drive missing mounting service bts9 kt9r nosuchelementexception no value present"),
        new("VK-3001", "recovery", "Access must be restored with a recovery key", "The password is no longer known, but a valid recovery key may still restore access.", "1. Locate the recovery key stored outside the vault.\n2. Confirm it belongs to this vault.\n3. Enter every word exactly.", "Complete the recovery workflow and choose a new strong password. Store the recovery key securely and never share it.", "forgot password recovery key reset password recover access"),
        new("VK-3002", "recovery", "Vault integrity needs verification", "Interrupted synchronization, storage failure, or manual changes may have left inconsistent encrypted data.", "1. Stop synchronization changes.\n2. Preserve a backup of the encrypted vault.\n3. Run Vault Doctor and review each reported item.", "Follow only the repair action associated with a verified result. Preserve backups until the vault has been opened and checked successfully.", "health check vault doctor verify integrity damaged vault corrupted vault")
    ];

    private static readonly IReadOnlyDictionary<string, LearningArticle> LearningArticles =
        new Dictionary<string, LearningArticle>(StringComparer.Ordinal)
        {
            ["keyboard"] = CreateKeyboardControlsLearningArticle(),
            ["first"] = new(
                "VaultKind creates an encrypted storage folder and a separate readable Windows drive. The four-step setup keeps those two locations clear from the beginning.",
                [
                    new("1. Name the vault", "Choose a short, recognizable name. This label appears only in VaultKind and can be changed later without renaming the encrypted storage folder."),
                    new("2. Choose encrypted storage", "Select a local folder, external drive, or a folder synchronized by your cloud provider. VaultKind creates a new vault folder inside that location."),
                    new("3. Review before creating", "Confirm the name and storage path. Advanced file-name compatibility should remain off unless a provider or file system specifically requires shorter encrypted names."),
                    new("4. Protect and recover", "Use a unique, memorable password. Create the recommended recovery key, save it somewhere separate, and confirm that copy before finishing setup."),
                    new("After setup", "Unlock the vault and use Open Drive. Add files to that readable drive—not directly to the encrypted storage folder.")
                ],
                "Choose storage that is backed up or synchronized, then keep the recovery key somewhere separate from both the vault and the computer."),
            ["recovery"] = new(
                "A recovery key is an emergency way to choose a new vault password. It does not reveal the old password, and VaultKind cannot recreate a lost key.",
                [
                    new("What the key protects", "The key belongs to one specific vault. Anyone who has it can restore access, so treat it with the same care as the vault password."),
                    new("Where to keep it", "Store at least one copy away from the encrypted vault: a printed copy in a secure place, an offline password manager, or a protected removable drive."),
                    new("How recovery works", "Open Manage Vault, choose Restore password access, enter every recovery-key word in order, and then choose a new password."),
                    new("After recovery", "Confirm that the vault unlocks with the new password. Preserve existing backups until you have verified the files you need.")
                ],
                "Never store the only recovery-key copy inside the vault it protects. You would need to unlock the vault to reach it."),
            ["cloud"] = new(
                "VaultKind works with the synchronization software you already use. VaultKind encrypts locally; OneDrive, Dropbox, Google Drive, and similar providers synchronize the encrypted storage folder.",
                [
                    new("What reaches the provider", "The provider receives encrypted file contents and scrambled file names. Your readable virtual drive and vault password are not uploaded by VaultKind."),
                    new("Let synchronization finish", "Lock the vault, then wait until the cloud application reports that synchronization is complete before shutting down or opening the vault on another device."),
                    new("Avoid simultaneous changes", "Do not edit the same vault from two devices at once. Conflicting encrypted files can be difficult to identify and repair."),
                    new("Offline files", "Configure the cloud client to keep the vault folder available on this device. Online-only placeholders may prevent VaultKind from reading required encrypted data."),
                    new("Backups still matter", "Synchronization mirrors changes and deletions; it is not a complete backup. Keep versioned or offline backups of the encrypted storage folder."),
                    new("Storage space and quota", "Keep free space available both on the Windows drive and with the cloud provider. A full local disk or exhausted cloud quota can interrupt writes and leave synchronization incomplete. Free space first, keep the vault locked, and let the provider finish before retrying.")
                ],
                "When a cloud client reports conflicts, keep the vault locked until synchronization is healthy and the conflict is understood."),
            ["drive"] = new(
                "Unlocking a vault opens a readable Windows drive. This is the normal workspace where familiar file and folder names appear.",
                [
                    new("The readable view", "Open, edit, organize, and save files in the virtual drive just as you would in another Windows drive. VaultKind encrypts those changes into the storage folder."),
                    new("The encrypted view", "The storage folder contains vault configuration, scrambled names, and encrypted data. Do not organize or edit those internal files by hand."),
                    new("Opening and closing", "Use Open Drive to reveal the readable view. Save your work and close programs using the drive before choosing Lock Vault."),
                    new("If locking fails", "A document, File Explorer window, search indexer, or security tool may still be using the drive. Close it and retry rather than forcing removal."),
                    new("Drive availability", "The readable drive exists only while the vault is unlocked. The encrypted storage remains safe and available for backup or synchronization while locked.")
                ],
                "If you see .c9r files and scrambled folders, you are looking at encrypted storage—not the readable virtual drive."),
            ["security"] = new(
                "VaultKind protects files through encryption, but the security of the Windows account, password, recovery key, and backups still matters.",
                [
                    new("Use a unique password", "Choose a password you do not use for Windows, email, or cloud accounts. Length and memorability are more useful than small predictable substitutions."),
                    new("Protect the recovery key", "Keep it private, offline when practical, and separate from the vault. Do not send it with a shared encrypted vault."),
                    new("Lock when finished", "Close documents and lock vaults that are no longer in use. VaultKind also attempts to lock open vaults during a normal application shutdown."),
                    new("Maintain Windows", "Install trusted Windows security updates, use device encryption where appropriate, and protect the Windows account with a strong sign-in method."),
                    new("Verify backups", "Back up the complete encrypted storage folder and periodically confirm that the backup can be read and contains vault.cryptomator plus the d folder. Keep an earlier known-good copy until a restored vault has opened successfully."),
                    new("Folder permissions and write access", "VaultKind must be able to read and write the encrypted storage location. If a vault cannot be created or updated, check that the drive is connected, the media is not read-only, and the current Windows account can create a harmless test file beside the vault."),
                    new("Blocked or missing app components", "If VaultKind cannot start or mount a drive, confirm the installation completed and review Windows Security or other trusted protection software for a quarantined VaultKind component. Restore files only when their package source is trusted; otherwise repair or reinstall VaultKind."),
                    new("Vault integrity and safe verification", "If synchronization was interrupted, storage failed, or encrypted files were changed manually, stop further synchronization and preserve a copy of the complete encrypted vault first. Run Vault Doctor for read-only checks, review each finding, and use only the guided action associated with a verified result.")
                ],
                "Encryption cannot protect readable files while the vault is open to someone already controlling your Windows session."),
            ["faq"] = new(
                "Detailed answers to common VaultKind questions, including what happens behind the interface and what to check when a workflow does not behave as expected.",
                [
                    new("Where are my files actually stored?", "A vault has two views. The encrypted storage folder is the permanent location containing vault.cryptomator, a d folder, scrambled names, and encrypted contents. The readable Windows drive exists only while the vault is unlocked. Work in the readable drive; back up or synchronize the encrypted storage folder."),
                    new("Does VaultKind upload files or require an account?", "No. VaultKind performs vault operations through its local engine and does not require an online account. If encrypted storage is inside OneDrive, Dropbox, Google Drive, or another synchronized folder, that provider's own Windows client uploads the already-encrypted data."),
                    new("Why was the correct password rejected?", "First confirm the selected vault name and storage path, then check Caps Lock, Num Lock, and the active Windows keyboard layout. A password belongs to one vault. If it is genuinely forgotten, use that vault's saved recovery key rather than repeatedly guessing."),
                    new("Can VaultKind reset or reveal a forgotten password?", "VaultKind cannot reveal the old password. A valid recovery key can authorize a new password without decrypting and re-encrypting every file. The key must belong to that specific vault and every word must remain in its original order."),
                    new("What are vault.cryptomator and the d folder?", "They are internal parts of encrypted storage. vault.cryptomator describes how the vault is protected; the d folder contains encrypted directory and file entries. Do not rename, edit, reorganize, or replace either by hand. Preserve both when copying, synchronizing, or backing up a vault."),
                    new("Can I rename a vault?", "Yes. Renaming changes the friendly label shown in VaultKind. It deliberately does not rename the encrypted storage folder, the readable drive, or internal encrypted files. This prevents a cosmetic change from becoming a risky storage operation."),
                    new("How do I move a vault safely?", "Lock the vault and let all applications release its readable drive. Pause synchronization if applicable, copy or move the complete encrypted storage folder, verify the destination contains vault.cryptomator and the d folder, then reconnect the destination through Add Vault. Keep the old copy until the moved vault unlocks and its expected files are verified."),
                    new("Can I use a vault on another Windows PC?", "Yes. Make the complete encrypted storage folder available on the second PC, wait for any cloud synchronization to finish, then choose Add Vault > Connect an existing vault. Avoid editing the same vault from two computers at once; finish work, lock, and synchronize before switching devices."),
                    new("How should I share a vault?", "Share only the encrypted storage folder. The other person connects it as an existing vault. Send the password through a separate trusted channel, never in the same message or shared folder. Anyone who possesses both the encrypted vault and its password or recovery key can access its readable contents."),
                    new("Why will a vault not lock?", "A program may still be using the readable drive. Save open documents, close File Explorer windows inside the vault, and stop applications that are previewing, indexing, scanning, or synchronizing readable files. Retry Lock Vault after those handles are released rather than forcing the drive closed during a write."),
                    new("Why is the readable drive missing?", "The readable drive exists only while the vault is unlocked and successfully mounted. Confirm the vault reports Unlocked, then use Open Drive. If mounting fails, check that the configured Windows mounting service is available and that the requested drive location is not already occupied."),
                    new("What does Vault Doctor change?", "Nothing. Vault Doctor is read-only: it reports locally observable health information such as engine availability, storage presence, configuration availability, free space, and vault state. A finding may direct you to reviewed guidance, but repairs remain a separate, deliberate action."),
                    new("What are Locate Encrypted File and Decrypt File Name for?", "They are troubleshooting and backup tools. Locate Encrypted File starts with a familiar file in the open readable drive and identifies its scrambled .c9r storage entry. Decrypt File Name starts with a .c9r entry and identifies its readable name. Neither tool modifies the selected file."),
                    new("What does Activity record?", "Activity is a private local history of VaultKind actions such as creating, connecting, unlocking, locking, recovery, password changes, and Doctor checks. It does not record passwords, recovery-key words, file contents, or readable file names, and it can be cleared from the Activity page."),
                    new("What happens when VaultKind closes?", "During a normal shutdown, VaultKind asks its local engine to lock every open vault before exiting. If Windows still has a vault in use, closing may require you to release the open file or application first. The encrypted storage folders remain in place and are not deleted."),
                    new("What if VaultKind settings cannot be loaded?", "Close any duplicate VaultKind process and restart once. If settings still cannot be read, preserve the existing settings file before changing anything, confirm the VaultKind settings folder is writable, and restore a known-good settings backup when available. Reset only damaged preferences; this does not require changing encrypted vault files."),
                    new("What if a vault configuration is missing or invalid?", "Keep the vault locked and do not edit the d folder or encrypted .c9r entries. Confirm vault.cryptomator exists and look for trusted configuration backups from the same vault. Never substitute a configuration file from another vault. Preserve a complete copy before attempting guided recovery.")
                ],
                "When an answer depends on a specific error or vault state, use VaultKind Assistant for a reviewed diagnostic case or run the read-only Vault Doctor for local evidence."),
        };

    public MainPage()
    {
        InitializeComponent();
        InitializeLiveRegions();
        foreach ((Button button, string _, string _, FontIcon _) in LearningTopicButtons())
        {
            button.KeyDown += LearningTopicKeyDown;
        }
        LoadActivityHistory();
        LoadLearningProgress();
        UpdateLearningProgressDisplay();
        UpdateLearningTopicAutomationNames();
        AppPreferences preferences = AppPreferencesStore.Load();
        DoctorRunSummary? doctorSummary = DoctorSummaryStore.Load();
        if (doctorSummary is not null)
        {
            UpdateDashboardDoctorStatus(
                doctorSummary.Healthy,
                doctorSummary.Attention,
                doctorSummary.Information,
                doctorSummary.CompletedAt);
        }
        DarkAppearanceToggle.IsOn = !string.Equals(preferences.AppearanceMode, "light", StringComparison.OrdinalIgnoreCase);
        ApplyAppearanceMode(DarkAppearanceToggle.IsOn, persist: false);
        LargerTextToggle.IsOn = preferences.UseLargerText;
        ApplyLargerText(preferences.UseLargerText);
        LaunchWithWindowsToggle.IsOn = WindowsStartupService.IsEnabled();
        RememberWindowPlacementToggle.IsOn = preferences.RememberWindowPlacement;
        RecordActivityHistoryToggle.IsOn = preferences.RecordActivityHistory;
        SignatureSoundsToggle.IsOn = preferences.SignatureSoundsEnabled;
        signatureSounds.IsEnabled = preferences.SignatureSoundsEnabled;
        initializingPreferences = false;
        Loaded += LoadBackendSnapshot;
        Loaded += EnsureInitialKeyboardTarget;
    }

    private void InitializeLiveRegions()
    {
        TextBlock[] regions =
        [
            DoctorSaveReportStatus,
            EngineStatusFooter,
            LaunchWithWindowsStatus,
            DiagnosticsFolderStatus,
            MountServiceStatus,
            AboutWebsiteStatus,
            RecoveryPasswordStatus,
            RecoveryStatus,
            ConnectVaultStatus,
            CreateVaultNameStatus,
            CreateVaultStorageStatus,
            CreateVaultPasswordMatchStatus,
            CreateVaultPasswordStrength,
            CreateVaultCreationStatus,
            CreatedRecoveryKeyCopyStatus,
            VaultActionStatus,
            ManagedVaultRenameStatus,
            ShareVaultStatus,
            ChangePasswordMatchStatus,
            ChangePasswordStatus,
            RecoveryKeyDisplayStatus,
            ManagedRecoveryKeyCopyStatus,
            VaultStatisticsStatus,
            LocateEncryptedFileStatus,
            DecryptFileNameStatus,
            UnlockStatus
        ];

        foreach (TextBlock region in regions)
        {
            RegisterLiveRegion(region);
        }
    }

    private static void RegisterLiveRegion(TextBlock region)
    {
        region.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) => AnnounceLiveRegion(region));
        region.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => AnnounceLiveRegion(region));
    }

    private static void AnnounceLiveRegion(TextBlock region)
    {
        if (region.Visibility != Visibility.Visible
            || string.IsNullOrWhiteSpace(region.Text)
            || !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            return;
        }

        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(region)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(region);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void EnsureInitialKeyboardTarget(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(EnsureKeyboardEntryPoint);
    }

    public void EnsureKeyboardEntryPoint()
    {
        if (HasKeyboardFocusWithinPage())
        {
            return;
        }

        TryFocusDashboardEntryPoint();
        keyboardEntryFocusTimer?.Stop();
        keyboardEntryFocusTimer = DispatcherQueue.CreateTimer();
        keyboardEntryFocusTimer.Interval = TimeSpan.FromMilliseconds(150);
        keyboardEntryFocusTimer.IsRepeating = false;
        keyboardEntryFocusTimer.Tick += (_, _) =>
        {
            keyboardEntryFocusTimer?.Stop();
            if (!HasKeyboardFocusWithinPage())
            {
                TryFocusDashboardEntryPoint();
            }
            keyboardEntryFocusTimer = null;
        };
        keyboardEntryFocusTimer.Start();
    }

    private bool HasKeyboardFocusWithinPage()
    {
        if (XamlRoot is null)
        {
            return false;
        }

        DependencyObject? current = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void TryFocusDashboardEntryPoint()
    {
        if (XamlRoot is null || !DashboardButton.IsLoaded)
        {
            return;
        }

        // Focus can initially remain in the native title bar. Give keyboard
        // input a stable route into the page without changing the visible view.
        DashboardButton.UpdateLayout();
        DashboardButton.StartBringIntoView();
        DashboardButton.Focus(FocusState.Keyboard);
    }

    private void MainPageCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        object? focused = XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot);
        if (focused is TextBox or PasswordBox or RichEditBox)
        {
            return;
        }

        if (e.Character != '/')
        {
            return;
        }

        e.Handled = true;
        ShowLearningCenter(this, new RoutedEventArgs());
    }

    private void TogglePasswordVisibility(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string target, Content: FontIcon icon })
        {
            return;
        }

        PasswordBox? passwordBox = target switch
        {
            nameof(UnlockPassword) => UnlockPassword,
            nameof(CreateVaultPasswordInput) => CreateVaultPasswordInput,
            nameof(CreateVaultPasswordConfirmInput) => CreateVaultPasswordConfirmInput,
            nameof(RecoveryNewPassword) => RecoveryNewPassword,
            nameof(RecoveryConfirmPassword) => RecoveryConfirmPassword,
            nameof(ChangeCurrentPassword) => ChangeCurrentPassword,
            nameof(ChangeNewPassword) => ChangeNewPassword,
            nameof(ChangeConfirmPassword) => ChangeConfirmPassword,
            nameof(RecoveryKeyPassword) => RecoveryKeyPassword,
            _ => null
        };
        if (passwordBox is null)
        {
            return;
        }

        bool reveal = passwordBox.PasswordRevealMode != PasswordRevealMode.Visible;
        passwordBox.PasswordRevealMode = reveal ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        icon.Glyph = reveal ? "\uE8A7" : "\uE890";
        AutomationProperties.SetName((Button)sender, reveal ? "Hide password" : "Show password");
        passwordBox.Focus(FocusState.Programmatic);
    }

    private async void LoadBackendSnapshot(object sender, RoutedEventArgs e)
    {
        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        for (int attempt = 0; attempt < 12 && snapshot.ConnectionState != BackendConnectionState.Ready; attempt++)
        {
            DashboardHealthDescription.Text = "Starting the VaultKind vault engine…";
            EngineStatusFooter.Text = "Connecting securely to the local vault engine.";
            await Task.Delay(500);
            snapshot = await backend.GetSnapshotAsync();
        }

        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(VaultBackendSnapshot snapshot)
    {
        knownVaults = snapshot.Vaults;
        TotalVaultsCount.Text = snapshot.Vaults.Count.ToString();
        UnlockedVaultsCount.Text = snapshot.UnlockedCount.ToString();
        LockedVaultsCount.Text = snapshot.LockedCount.ToString();
        int attentionCount = snapshot.Vaults.Count(VaultNeedsAttention);
        AttentionVaultsCount.Text = attentionCount.ToString();
        TotalVaultsCard.IsEnabled = snapshot.Vaults.Count > 0;
        UnlockedVaultsCard.IsEnabled = snapshot.UnlockedCount > 0;
        LockedVaultsCard.IsEnabled = snapshot.LockedCount > 0;
        AttentionVaultsCard.IsEnabled = attentionCount > 0;
        UpdateDashboardHealth(snapshot, attentionCount);
        EngineStatusFooter.Text = snapshot.ConnectionState == BackendConnectionState.Ready
            ? "Connected securely to the local VaultKind engine."
            : "The local VaultKind engine is unavailable.";
        RenderVaultSidebar(snapshot.Vaults);
    }

    private void UpdateDashboardHealth(VaultBackendSnapshot snapshot, int attentionCount)
    {
        if (snapshot.ConnectionState != BackendConnectionState.Ready)
        {
            DashboardHealthTitle.Text = "VaultKind needs your attention";
            DashboardHealthDescription.Text = "The local vault engine is unavailable. Vault states may be out of date.";
            DashboardHealthIcon.Glyph = "\uE7BA";
            DashboardHealthIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 46));
            DashboardHealthCard.Background = new SolidColorBrush(Color.FromArgb(255, 62, 55, 31));
            DashboardHealthCard.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 177, 139, 32));
            return;
        }

        if (attentionCount > 0)
        {
            DashboardHealthTitle.Text = attentionCount == 1
                ? "One vault is worth reviewing"
                : $"{attentionCount} vaults are worth reviewing";
            DashboardHealthDescription.Text = "Open the Attention card above to review the affected vault.";
            DashboardHealthIcon.Glyph = "\uE7BA";
            DashboardHealthIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 46));
            DashboardHealthCard.Background = new SolidColorBrush(Color.FromArgb(255, 62, 55, 31));
            DashboardHealthCard.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 177, 139, 32));
            return;
        }

        DashboardHealthTitle.Text = "Everything looks good";
        DashboardHealthDescription.Text = $"Connected securely to the local vault engine. {snapshot.Vaults.Count} configured vault(s) reported.";
        DashboardHealthIcon.Glyph = "\uE73E";
        DashboardHealthIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112));
        DashboardHealthCard.Background = new SolidColorBrush(Color.FromArgb(255, 41, 63, 52));
        DashboardHealthCard.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 62, 150, 91));
    }

    private static bool VaultNeedsAttention(VaultSummary vault) =>
        vault.State.Equals("missing", StringComparison.OrdinalIgnoreCase)
        || vault.State.Equals("error", StringComparison.OrdinalIgnoreCase);

    private void OpenDashboardMetric(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string category })
        {
            return;
        }

        VaultSummary? vault = category switch
        {
            "unlocked" => knownVaults.FirstOrDefault(candidate => candidate.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase)),
            "locked" => knownVaults.FirstOrDefault(candidate => candidate.State.Equals("locked", StringComparison.OrdinalIgnoreCase)),
            "attention" => knownVaults.FirstOrDefault(VaultNeedsAttention),
            _ => knownVaults.FirstOrDefault()
        };

        if (vault is not null && FindVaultButton(vault.Id) is Button vaultButton)
        {
            ShowVault(vault, vaultButton);
        }
    }

    private void RenderVaultSidebar(IReadOnlyList<VaultSummary> vaults)
    {
        VaultListPanel.Children.Clear();
        vaultButtons.Clear();
        EmptyVaultState.Visibility = vaults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var vault in vaults)
        {
            var unlocked = vault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase);
            var icon = new FontIcon
            {
                Glyph = unlocked ? "\uE785" : "\uE72E",
                FontSize = 17,
                Foreground = new SolidColorBrush(unlocked
                    ? Color.FromArgb(255, 73, 205, 112)
                    : Color.FromArgb(255, 78, 161, 255))
            };
            var labels = new StackPanel { Spacing = 1, Width = 214 };
            labels.Children.Add(new TextBlock
            {
                Text = vault.Name,
                FontSize = 16,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var pathLabel = new TextBlock
            {
                Text = vault.Path,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 146, 156, 163)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ToolTipService.SetToolTip(pathLabel, vault.Path);
            labels.Children.Add(pathLabel);
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            content.Children.Add(icon);
            content.Children.Add(labels);

            var vaultButton = new Button
            {
                Style = (Style)Resources["VaultSidebarButtonStyle"],
                Content = content,
                Tag = vault.Id,
                ContextFlyout = BuildVaultContextMenu(vault)
            };
            AutomationProperties.SetName(vaultButton, $"{vault.Name}, {FriendlyVaultState(vault.State)}, {vault.Path}");
            vaultButton.Click += (_, _) => ShowVault(vault, vaultButton);
            vaultButton.KeyDown += SidebarNavigationKeyDown;
            vaultButtons.Add(vaultButton);
            VaultListPanel.Children.Add(vaultButton);
        }

        QueueTextScaleRefresh();
    }

    private MenuFlyout BuildVaultContextMenu(VaultSummary vault)
    {
        var menu = new MenuFlyout();
        bool unlocked = vault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase);

        if (unlocked)
        {
            menu.Items.Add(CreateVaultMenuItem("Open Drive", "\uE838", (_, e) =>
            {
                SelectVaultForContextAction(vault);
                OpenDrive(menu, e);
            }));
            menu.Items.Add(CreateVaultMenuItem("Lock", "\uE72E", (_, e) =>
            {
                SelectVaultForContextAction(vault);
                LockVault(menu, e);
            }));
        }
        else
        {
            menu.Items.Add(CreateVaultMenuItem("Unlock\u2026", "\uE785", (_, e) =>
            {
                SelectVaultForContextAction(vault);
                ShowUnlock(menu, e);
            }));
            menu.Items.Add(CreateVaultMenuItem("Share\u2026", "\uE72D", (_, e) =>
            {
                SelectVaultForContextAction(vault);
                ShowVaultManagement(menu, e);
                ShowVaultShareGuide(menu, e);
            }));
        }

        if (unlocked)
        {
            menu.Items.Add(CreateVaultMenuItem("Share\u2026", "\uE72D", (_, e) =>
            {
                SelectVaultForContextAction(vault);
                ShowVaultManagement(menu, e);
                ShowVaultShareGuide(menu, e);
            }));
        }

        menu.Items.Add(CreateVaultMenuItem("Manage Vault", "\uE713", (_, e) =>
        {
            SelectVaultForContextAction(vault);
            ShowVaultManagement(menu, e);
        }));

        if (!unlocked)
        {
            menu.Items.Add(CreateVaultMenuItem("Remove from VaultKind\u2026", "\uE74D", (_, e) =>
            {
                SelectVaultForContextAction(vault);
                ShowRemoveVaultConfirmation(menu, e);
            }));
        }

        return menu;
    }

    private static MenuFlyoutItem CreateVaultMenuItem(string text, string glyph, RoutedEventHandler action)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Icon = new FontIcon { Glyph = glyph }
        };
        item.Click += action;
        return item;
    }

    private void SelectVaultForContextAction(VaultSummary vault)
    {
        if (FindVaultButton(vault.Id) is Button button)
        {
            ShowVault(vault, button);
        }
    }

    private static string FriendlyVaultState(string state) => state.ToLowerInvariant() switch
    {
        "unlocked" => "Unlocked",
        "locked" => "Locked",
        "processing" => "Working…",
        "needs_migration" => "Update required",
        "missing" or "vault_config_missing" or "all_missing" => "Location unavailable",
        "error" => "Needs attention",
        _ => "Unavailable"
    };

    private void SidebarNavigationKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not Button current)
        {
            return;
        }

        List<Button> destinations =
        [
            DashboardButton,
            DoctorButton,
            AddVaultButton,
            .. vaultButtons,
            ActivityButton,
            SettingsButton,
            LearningButton
        ];
        destinations = destinations
            .Where(button => button.Visibility == Visibility.Visible && button.IsEnabled)
            .ToList();

        KeyboardNavigationCommand command = e.Key switch
        {
            VirtualKey.Up => KeyboardNavigationCommand.Previous,
            VirtualKey.Down => KeyboardNavigationCommand.Next,
            VirtualKey.Home => KeyboardNavigationCommand.First,
            VirtualKey.End => KeyboardNavigationCommand.Last,
            _ => KeyboardNavigationCommand.None
        };
        int nextIndex = KeyboardNavigationPolicy.ResolveNextIndex(
            destinations.IndexOf(current),
            destinations.Count,
            command);
        if (nextIndex < 0)
        {
            return;
        }

        Button target = destinations[nextIndex];
        target.StartBringIntoView();
        target.Focus(FocusState.Keyboard);
        e.Handled = true;
    }

    private void ShowDashboard(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Visible;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Dashboard";
        ContextSubtitle.Text = "Your secure workspace at a glance.";
        SetSelectedDestination(DashboardButton, DoctorButton, "Dashboard");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
        FocusAfterNavigation(DashboardButton);
    }

    private void ShowDoctor(object sender, RoutedEventArgs e)
    {
        doctorFocusVaultId = null;
        OpenDoctorView();
    }

    private void ShowManagedDoctor(object sender, RoutedEventArgs e)
    {
        doctorFocusVaultId = activeVault?.Id;
        OpenDoctorView();
    }

    private void OpenDoctorView()
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Visible;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Vault Doctor";
        ContextSubtitle.Text = "Automatic, private checks across VaultKind and your configured vaults.";
        SetSelectedDestination(DoctorButton, DashboardButton, "Vault Doctor");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
        if (!string.IsNullOrWhiteSpace(doctorFocusVaultId) || latestDoctorChecks.Count == 0)
        {
            _ = RunDoctorChecksAsync();
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => DoctorView.ChangeView(null, 0, null, true));
            FocusAfterNavigation(DoctorRunAgainButton);
        }
    }

    private void ShowAddVault(object sender, RoutedEventArgs e)
    {
        recoveryOpenedFromVaultManagement = false;
        activeVault = null;
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Visible;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Add Vault";
        ContextSubtitle.Text = "Create a new encrypted vault, connect an existing one, or recover access.";
        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        AddVaultButton.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
        AddVaultButton.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        AddVaultButton.BorderThickness = new Thickness(1);
        AutomationProperties.SetName(AddVaultButton, "Add Vault, selected");
        DispatcherQueue.TryEnqueue(() => AddVaultView.ChangeView(null, 0, null, true));
        FocusAfterNavigation(CreateNewVaultButton);
    }

    private void ShowConnectVault(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Visible;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Connect a Vault";
        ContextSubtitle.Text = "Add an existing encrypted vault without moving or changing its files.";
        selectedConnectVaultPath = null;
        ConnectVaultReviewCard.Visibility = Visibility.Collapsed;
        ConnectVaultStatus.Visibility = Visibility.Collapsed;
        ConnectVaultProgress.IsActive = false;
        ConnectVaultProgress.Visibility = Visibility.Collapsed;
        ConnectVaultStepText.Text = "STEP 1 OF 2";
        ConnectVaultSecondStepBar.Background = new SolidColorBrush(Color.FromArgb(255, 88, 97, 104));
        ConnectVaultButton.IsEnabled = false;
        FocusAfterNavigation(ConnectVaultFolderButton);
    }

    private void ShowActivity(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Visible;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Activity";
        ContextSubtitle.Text = "A private record of vault actions from this VaultKind session.";
        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");
        SelectSidebarDestination(ActivityButton, "Activity");
        ClearVaultSelection();
        SetAddVaultUnselected();
        ActivityClearConfirmation.Visibility = Visibility.Collapsed;
        RenderActivity();
        FocusAfterNavigation(ActivitySearchInput);
    }

    private void ShowRecentActivity(object sender, RoutedEventArgs e)
    {
        SessionActivity? latest = activityHistory.LastOrDefault();
        ShowActivity(sender, e);
        ActivitySearchInput.Text = latest is null
            ? string.Empty
            : ActivityCategoryLabel(ActivitySectionFor(latest.Category));
    }

    private void ShowClearActivityConfirmation(object sender, RoutedEventArgs e)
    {
        if (activityHistory.Count == 0)
        {
            return;
        }

        ActivityClearConfirmation.Visibility = Visibility.Visible;
        ActivityClearCancelButton.Focus(FocusState.Programmatic);
    }

    private void HideClearActivityConfirmation(object sender, RoutedEventArgs e)
    {
        ActivityClearConfirmation.Visibility = Visibility.Collapsed;
        ClearActivityButton.Focus(FocusState.Programmatic);
    }

    private void ClearActivity(object sender, RoutedEventArgs e)
    {
        activityHistory.Clear();
        SaveActivityHistory();
        ActivityClearConfirmation.Visibility = Visibility.Collapsed;
        ActivitySearchInput.Text = string.Empty;
        RenderActivity();
        UpdateDashboardRecentActivity();
        FocusAfterNavigation(ActivitySearchInput);
    }

    private void ShowSettings(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        LearningView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Preferences";
        ContextSubtitle.Text = "Review VaultKind's appearance, Windows behavior, and privacy defaults.";
        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(LearningButton, "Learning Center");
        SelectSidebarDestination(SettingsButton, "Preferences");
        ClearVaultSelection();
        SetAddVaultUnselected();
        SelectSettingsSection("general");
        FocusAfterNavigation(SettingsGeneralButton);
    }

    private void SelectSettingsSection(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string section)
        {
            SelectSettingsSection(section);
        }
    }

    private void SettingsSectionKeyDown(object sender, KeyRoutedEventArgs e)
    {
        Button[] sections =
        [
            SettingsGeneralButton,
            SettingsAppearanceButton,
            SettingsVirtualDriveButton,
            SettingsPrivacyButton,
            SettingsAboutButton
        ];

        int currentIndex = sender is Button current ? Array.IndexOf(sections, current) : -1;
        if (currentIndex < 0)
        {
            return;
        }

        int nextIndex = e.Key switch
        {
            VirtualKey.Left => (currentIndex + sections.Length - 1) % sections.Length,
            VirtualKey.Right => (currentIndex + 1) % sections.Length,
            VirtualKey.Home => 0,
            VirtualKey.End => sections.Length - 1,
            _ => -1
        };

        if (nextIndex < 0)
        {
            return;
        }

        Button next = sections[nextIndex];
        if (next.Tag is string section)
        {
            SelectSettingsSection(section);
            next.Focus(FocusState.Keyboard);
            e.Handled = true;
        }
    }

    private void SelectSettingsSection(string section)
    {
        SettingsGeneralPanel.Visibility = section == "general" ? Visibility.Visible : Visibility.Collapsed;
        SettingsAppearancePanel.Visibility = section == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        SettingsVirtualDrivePanel.Visibility = section == "virtual-drive" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPrivacyPanel.Visibility = section == "privacy" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPrivacyPromisePanel.Visibility = section == "privacy" ? Visibility.Visible : Visibility.Collapsed;
        SettingsAboutPanel.Visibility = section == "about" ? Visibility.Visible : Visibility.Collapsed;

        ApplySettingsButtonPalette(section);

        if (section == "virtual-drive")
        {
            _ = LoadMountSettingsAsync();
        }

        if (section == "privacy")
        {
            UpdatePrivacyHistorySummary();
        }

        if (section == "about")
        {
            _ = LoadAboutInformationAsync();
        }
    }

    private void UpdatePrivacyHistorySummary()
    {
        PrivacyHistorySummary.Text = activityHistory.Count switch
        {
            0 => "No Activity entries are currently stored on this computer.",
            1 => "1 private Activity entry is currently stored on this computer.",
            _ => $"{activityHistory.Count} private Activity entries are currently stored on this computer."
        };
    }

    private async Task LoadAboutInformationAsync()
    {
        Version? version = typeof(MainPage).Assembly.GetName().Version;
        AboutVersionText.Text = version is null ? "Version unavailable" : $"{version.Major}.{version.Minor}.{version.Build}";
        WindowsVersionInfo windows = ReadWindowsVersionInfo();
        AboutWindowsEditionText.Text = windows.Edition;
        AboutWindowsVersionText.Text = windows.Version;

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        bool connected = snapshot.ConnectionState == BackendConnectionState.Ready;
        AboutEngineText.Text = connected ? "Connected locally" : "Currently unavailable";
        AboutEngineText.Foreground = new SolidColorBrush(connected
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 196, 0));
        AboutVaultCountText.Text = snapshot.Vaults.Count == 1 ? "1 vault" : $"{snapshot.Vaults.Count} vaults";
    }

    private static WindowsVersionInfo ReadWindowsVersionInfo()
    {
        const string currentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(currentVersionKey);
            string edition = key?.GetValue("ProductName") as string ?? "Windows";
            string version = key?.GetValue("DisplayVersion") as string
                ?? key?.GetValue("ReleaseId") as string
                ?? "Unavailable";
            string? buildValue = key?.GetValue("CurrentBuildNumber") as string;

            // Some Windows 11 installations retain "Windows 10" in ProductName for
            // application compatibility. Builds 22000 and newer are Windows 11.
            if (int.TryParse(buildValue, NumberStyles.None, CultureInfo.InvariantCulture, out int build)
                && build >= 22000
                && edition.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
            {
                edition = $"Windows 11{edition["Windows 10".Length..]}";
            }

            return new WindowsVersionInfo(edition, version);
        }
        catch (Exception)
        {
            return new WindowsVersionInfo("Windows", "Unavailable");
        }
    }

    private sealed record WindowsVersionInfo(string Edition, string Version);

    private async void OpenVaultKindWebsite(object sender, RoutedEventArgs e)
    {
        bool opened = await Launcher.LaunchUriAsync(new Uri("https://vaultkind.dev"));
        AboutWebsiteStatus.Text = opened
            ? "Opened vaultkind.dev in your default browser."
            : "Windows could not open vaultkind.dev in the default browser.";
        AboutWebsiteStatus.Foreground = new SolidColorBrush(opened
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 92, 87));
        AboutWebsiteStatus.Visibility = Visibility.Visible;
    }

    private async Task LoadMountSettingsAsync()
    {
        loadingMountServices = true;
        MountServiceSelector.IsEnabled = false;
        MountServiceStatus.Text = "Asking the local vault engine which Windows drive providers are installed...";
        MountServiceStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 175, 185, 192));

        MountSettingsResult result = await backend.GetMountSettingsAsync();
        if (!result.Succeeded || result.MountServices.Count == 0)
        {
            MountServiceSelector.ItemsSource = null;
            MountServiceCapabilities.Text = "Provider information is unavailable until the local VaultKind engine is connected.";
            MountServiceStatus.Text = "VaultKind could not read the installed drive providers. No setting was changed.";
            MountServiceStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 92, 87));
            loadingMountServices = false;
            return;
        }

        MountServiceSelector.ItemsSource = result.MountServices;
        MountServiceSelector.SelectedItem = result.MountServices.FirstOrDefault(service => string.Equals(service.Id, result.SelectedMountService, StringComparison.Ordinal)) ?? result.MountServices[0];
        MountServiceSelector.IsEnabled = true;
        UpdateMountServiceCapabilities(MountServiceSelector.SelectedItem as MountServiceOption);
        MountServiceStatus.Text = $"VaultKind found {result.MountServices.Count - 1} available drive providers. Automatic is recommended.";
        MountServiceStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112));
        loadingMountServices = false;
    }

    private void RefreshMountServices(object sender, RoutedEventArgs e)
    {
        _ = LoadMountSettingsAsync();
    }

    private async void MountServiceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingMountServices || MountServiceSelector.SelectedItem is not MountServiceOption selected)
        {
            return;
        }

        MountServiceSelector.IsEnabled = false;
        MountServiceStatus.Text = "Saving the default drive provider locally...";
        MountSettingsResult result = await backend.SetMountServiceAsync(selected.Id);
        MountServiceSelector.IsEnabled = true;
        if (!result.Succeeded)
        {
            MountServiceStatus.Text = "VaultKind could not save that provider. The previous setting remains in use.";
            MountServiceStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 92, 87));
            await LoadMountSettingsAsync();
            return;
        }

        UpdateMountServiceCapabilities(selected);
        MountServiceStatus.Text = $"{selected.Name} is now the default for future vault unlocks.";
        MountServiceStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112));
    }

    private void UpdateMountServiceCapabilities(MountServiceOption? service)
    {
        if (service is null)
        {
            MountServiceCapabilities.Text = "No provider selected.";
            return;
        }

        if (service.Id == "automatic")
        {
            MountServiceCapabilities.Text = "VaultKind chooses the best available provider. Exact capabilities follow the provider selected at unlock time.";
            return;
        }

        var capabilities = new List<string>();
        if (service.DriveLetter) capabilities.Add("Windows drive letter");
        if (service.MountPoint) capabilities.Add("Folder mount point");
        if (service.ReadOnly) capabilities.Add("Read-only mode");
        if (service.MountFlags) capabilities.Add("Custom mount flags");
        if (service.LoopbackPort) capabilities.Add("Configurable loopback port");
        MountServiceCapabilities.Text = capabilities.Count > 0 ? "✓ " + string.Join("   ✓ ", capabilities) : "This provider reports no optional mounting capabilities.";
    }

    private void RememberWindowPlacementChanged(object sender, RoutedEventArgs e)
    {
        SavePreferences();
    }

    private void LaunchWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (initializingPreferences)
        {
            return;
        }

        bool requestedState = LaunchWithWindowsToggle.IsOn;
        if (WindowsStartupService.TrySetEnabled(requestedState))
        {
            LaunchWithWindowsStatus.Visibility = Visibility.Collapsed;
            return;
        }

        initializingPreferences = true;
        LaunchWithWindowsToggle.IsOn = WindowsStartupService.IsEnabled();
        initializingPreferences = false;
        LaunchWithWindowsStatus.Text = "Windows could not update the startup registration. No other setting was changed.";
        LaunchWithWindowsStatus.Visibility = Visibility.Visible;
    }

    private async void OpenDiagnosticsFolder(object sender, RoutedEventArgs e)
    {
        string? repositoryRoot = FindRepositoryRoot();
        string logDirectory = repositoryRoot is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaultKind", "logs")
            : Path.Combine(repositoryRoot, "target", "ui-dev-profile", "logs");

        try
        {
            Directory.CreateDirectory(logDirectory);
            Windows.Storage.StorageFolder folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(logDirectory);
            bool opened = await Launcher.LaunchFolderAsync(folder);
            DiagnosticsFolderStatus.Text = opened
                ? "Opened the local diagnostics folder."
                : $"Windows could not open the folder. Its location is {logDirectory}";
            DiagnosticsFolderStatus.Foreground = new SolidColorBrush(opened
                ? Color.FromArgb(255, 73, 205, 112)
                : Color.FromArgb(255, 255, 92, 87));
            DiagnosticsFolderStatus.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            DiagnosticsFolderStatus.Text = $"Windows could not open the folder. Its location is {logDirectory}";
            DiagnosticsFolderStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 92, 87));
            DiagnosticsFolderStatus.Visibility = Visibility.Visible;
        }
    }

    private static string? FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "pom.xml")) &&
                Directory.Exists(Path.Combine(current.FullName, "native", "VaultKind.Windows")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private void RecordActivityHistoryChanged(object sender, RoutedEventArgs e)
    {
        SavePreferences();
    }

    private void SavePreferences()
    {
        if (initializingPreferences)
        {
            return;
        }

        AppPreferencesStore.Save(new AppPreferences(
            RememberWindowPlacementToggle.IsOn,
            RecordActivityHistoryToggle.IsOn,
            DarkAppearanceToggle.IsOn ? "dark" : "light",
            LargerTextToggle.IsOn,
            SignatureSoundsToggle.IsOn));
    }

    private void SignatureSoundsChanged(object sender, RoutedEventArgs e)
    {
        signatureSounds.IsEnabled = SignatureSoundsToggle.IsOn;
        SavePreferences();
    }

    private void PreviewSignatureSound(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sound })
        {
            return;
        }

        signatureSounds.Play(sound switch
        {
            "open" => SignatureSound.VaultOpen,
            "locked" => SignatureSound.VaultLocked,
            _ => SignatureSound.Warning
        }, sound == "warning" ? SoundEmphasis.Strong : SoundEmphasis.Standard);
    }

    private void LargerTextChanged(object sender, RoutedEventArgs e)
    {
        if (initializingPreferences)
        {
            return;
        }

        ApplyLargerText(LargerTextToggle.IsOn);
        SavePreferences();
    }

    private void ApplyLargerText(bool enabled)
    {
        useLargerText = enabled;
        ApplyTextScale(this);
    }

    private void QueueTextScaleRefresh()
    {
        if (!useLargerText || textScaleRefreshQueued)
        {
            return;
        }

        textScaleRefreshQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            textScaleRefreshQueued = false;
            ApplyTextScale(this);
        });
    }

    private void ApplyTextScale(DependencyObject element)
    {
        const double scale = 1.16;

        if (element is TextBlock textBlock)
        {
            if (!baselineFontSizes.TryGetValue(textBlock, out double baseline))
            {
                baseline = textBlock.FontSize;
                baselineFontSizes[textBlock] = baseline;
            }

            textBlock.FontSize = useLargerText ? baseline * scale : baseline;
        }
        else if (element is Control control && element is not FontIcon)
        {
            if (!baselineFontSizes.TryGetValue(control, out double baseline))
            {
                baseline = control.FontSize;
                baselineFontSizes[control] = baseline;
            }

            control.FontSize = useLargerText ? baseline * scale : baseline;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(element);
        for (int index = 0; index < childCount; index++)
        {
            ApplyTextScale(VisualTreeHelper.GetChild(element, index));
        }
    }

    private void AppearanceModeChanged(object sender, RoutedEventArgs e)
    {
        if (initializingPreferences)
        {
            return;
        }

        ApplyAppearanceMode(DarkAppearanceToggle.IsOn, persist: true);
    }

    private void ApplyAppearanceMode(bool useDarkMode, bool persist)
    {
        bool useLightMode = !useDarkMode;
        RequestedTheme = useLightMode ? ElementTheme.Light : ElementTheme.Dark;

        SetPaletteColor("AppCanvasBrush", useLightMode ? "#E3E8EC" : "#292D30");
        SetPaletteColor("SidebarBrush", useLightMode ? "#D4DCE2" : "#1D2022");
        SetPaletteColor("CardBrush", useLightMode ? "#F8FAFB" : "#3B4145");
        SetPaletteColor("CardBorderBrush", useLightMode ? "#8998A3" : "#535B60");
        SetPaletteColor("BrandBlueBrush", useLightMode ? "#006FC9" : "#4EA1FF");
        SetPaletteColor("PrimaryTextBrush", useLightMode ? "#18232B" : "#F4F6F8");
        SetPaletteColor("SelectedSurfaceBrush", useLightMode ? "#C3D1DB" : "#3A4248");
        SetPaletteColor("MutedTextBrush", useLightMode ? "#455662" : "#AEB7BE");
        SetPaletteColor("DividerBrush", useLightMode ? "#9AA8B2" : "#485056");
        SetPaletteColor("InfoSurfaceBrush", useLightMode ? "#D5E6F2" : "#293A4A");
        SetPaletteColor("InfoTextBrush", useLightMode ? "#234A68" : "#AFC8E2");
        SetPaletteColor("IconSurfaceBrush", useLightMode ? "#C7DCEB" : "#35444D");
        SetPaletteColor("DeepSurfaceBrush", useLightMode ? "#EDF1F4" : "#202426");
        SetPaletteColor("ListSurfaceBrush", useLightMode ? "#F4F7F9" : "#2D3438");
        SetPaletteColor("ListSurfaceStrongBrush", useLightMode ? "#E8EEF2" : "#252A2D");
        SetPaletteColor("SubtleBadgeBrush", useLightMode ? "#E1E8ED" : "#2D3438");
        SetPaletteColor("TrackBrush", useLightMode ? "#8F9DA7" : "#586168");
        SetPaletteColor("StrongDividerBrush", useLightMode ? "#7E8E99" : "#4E575D");
        SetPaletteColor("InfoBorderBrush", useLightMode ? "#347FB8" : "#367DC2");
        SetPaletteColor("ButtonBackgroundPointerOver", useLightMode ? "#B7CAD8" : "#40596C");
        SetPaletteColor("ButtonBorderBrushPointerOver", useLightMode ? "#2379B9" : "#69B1FF");
        SetPaletteColor("ButtonForegroundPointerOver", useLightMode ? "#10202B" : "#FFFFFF");
        SetPaletteColor("ButtonBackgroundPressed", useLightMode ? "#A8BFCE" : "#29435B");
        SetPaletteColor("ButtonForegroundPressed", useLightMode ? "#10202B" : "#FFFFFF");

        // Page-scoped resources do not exist until the XAML root has finished
        // loading, so assign the canvas brush only after construction.
        if (Resources["AppCanvasBrush"] is SolidColorBrush canvasBrush)
        {
            Background = canvasBrush;
        }

        RefreshThemeDependentNavigation();

        AppearanceModeName.Text = useLightMode ? "Light — Clear" : "Dark — Low Glare";
        AppearanceModeDescription.Text = useLightMode
            ? "Bright surfaces, clear dark text, and VaultKind blue accents."
            : "Charcoal surfaces, restrained contrast, and VaultKind blue accents.";

        if (Application.Current is App app && app.MainWindow is MainWindow window)
        {
            window.ApplyAppearance(useLightMode);
        }

        if (persist)
        {
            SavePreferences();
        }
    }

    private void SetPaletteColor(string resourceKey, string color)
    {
        if (Resources[resourceKey] is SolidColorBrush brush)
        {
            brush.Color = Color.FromArgb(
                255,
                Convert.ToByte(color.Substring(1, 2), 16),
                Convert.ToByte(color.Substring(3, 2), 16),
                Convert.ToByte(color.Substring(5, 2), 16));
        }
    }

    private SolidColorBrush PaletteBrush(string resourceKey)
    {
        return Resources[resourceKey] as SolidColorBrush ?? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    }

    private void ApplySettingsButtonPalette(string section)
    {
        (Button Button, string Name)[] settingsSections =
        [
            (SettingsGeneralButton, "General preferences"),
            (SettingsAppearanceButton, "Appearance preferences"),
            (SettingsVirtualDriveButton, "Virtual Drive preferences"),
            (SettingsPrivacyButton, "Privacy preferences"),
            (SettingsAboutButton, "About VaultKind")
        ];

        foreach ((Button button, string name) in settingsSections)
        {
            bool selected = string.Equals(button.Tag as string, section, StringComparison.Ordinal);
            button.Background = selected ? PaletteBrush("SelectedSurfaceBrush") : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderBrush = selected ? PaletteBrush("BrandBlueBrush") : PaletteBrush("CardBorderBrush");
            button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            button.Foreground = selected ? PaletteBrush("BrandBlueBrush") : PaletteBrush("PrimaryTextBrush");
            AutomationProperties.SetName(button, selected ? $"{name}, selected" : name);
        }
    }

    private void RefreshThemeDependentNavigation()
    {
        foreach (Button button in new[] { DashboardButton, DoctorButton, ActivityButton, SettingsButton, LearningButton })
        {
            bool selected = AutomationProperties.GetName(button).EndsWith(", selected", StringComparison.Ordinal);
            if (selected)
            {
                button.Background = PaletteBrush("SelectedSurfaceBrush");
                button.BorderBrush = PaletteBrush("BrandBlueBrush");
                SetContentForeground(button, PaletteBrush("BrandBlueBrush"));
            }
            else
            {
                SetContentForeground(button, PaletteBrush("PrimaryTextBrush"));
            }
        }

        string section = SettingsAppearancePanel.Visibility == Visibility.Visible ? "appearance"
            : SettingsVirtualDrivePanel.Visibility == Visibility.Visible ? "virtual-drive"
            : SettingsPrivacyPanel.Visibility == Visibility.Visible ? "privacy"
            : SettingsAboutPanel.Visibility == Visibility.Visible ? "about"
            : "general";
        ApplySettingsButtonPalette(section);
    }

    private void ShowLearningCenter(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Visible;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Getting Started with VaultKind";
        ContextSubtitle.Text = "Plain-language guidance for creating, using, and protecting your vaults.";
        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SelectSidebarDestination(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
        LearningNavigationPanel.Visibility = Visibility.Visible;
        AssistantNavigationPanel.Visibility = Visibility.Collapsed;
        LearningContentScroll.Visibility = Visibility.Visible;
        AssistantContentScroll.Visibility = Visibility.Collapsed;
        ShowLearningTopic(selectedLearningTopic);
        FocusAfterNavigation(LearningSearch);
    }

    private void ContinueLearning(object sender, RoutedEventArgs e)
    {
        string? nextTopic = LearningTopicButtons()
            .Select(topic => topic.Id)
            .FirstOrDefault(topic => !viewedLearningTopics.Contains(topic));
        if (!string.IsNullOrWhiteSpace(nextTopic))
        {
            selectedLearningTopic = nextTopic;
        }

        ShowLearningCenter(sender, e);
    }

    private void SelectLearningTopic(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string topic })
        {
            ShowLearningTopic(topic);
        }
    }

    private void LearningSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Down or VirtualKey.Up)
        {
            List<Button> visibleTopics = LearningTopicButtons()
                .Where(topic => topic.Button.Visibility == Visibility.Visible)
                .Select(topic => topic.Button)
                .ToList();
            if (visibleTopics.Count > 0)
            {
                e.Handled = true;
                Button target = e.Key == VirtualKey.Down ? visibleTopics[0] : visibleTopics[^1];
                target.Focus(FocusState.Keyboard);
            }

            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            foreach ((Button button, string id, string _, FontIcon _) in LearningTopicButtons())
            {
                if (button.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    ShowLearningTopic(id);
                    return;
                }
            }

            return;
        }

        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;
        if (!string.IsNullOrEmpty(LearningSearch.Text))
        {
            LearningSearch.Text = string.Empty;
            return;
        }

        ShowDashboard(this, new RoutedEventArgs());
    }

    private void LearningTopicKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not Button current || e.Key is not (VirtualKey.Down or VirtualKey.Up))
        {
            return;
        }

        List<Button> visibleTopics = LearningTopicButtons()
            .Where(topic => topic.Button.Visibility == Visibility.Visible)
            .Select(topic => topic.Button)
            .ToList();
        int currentIndex = visibleTopics.IndexOf(current);
        if (currentIndex < 0)
        {
            return;
        }

        e.Handled = true;
        if (e.Key == VirtualKey.Up && currentIndex == 0)
        {
            LearningSearch.Focus(FocusState.Keyboard);
            return;
        }

        int targetIndex = e.Key == VirtualKey.Down
            ? Math.Min(currentIndex + 1, visibleTopics.Count - 1)
            : Math.Max(currentIndex - 1, 0);
        visibleTopics[targetIndex].Focus(FocusState.Keyboard);
    }

    private void ShowLearningTopic(string topic, string? sectionTitle = null, bool highlightAnswer = false)
    {
        if (!string.IsNullOrWhiteSpace(selectedLearningTopic))
        {
            learningTopicScrollOffsets[selectedLearningTopic] = LearningContentScroll.VerticalOffset;
        }

        selectedLearningTopic = topic;
        if (highlightAnswer && !string.IsNullOrWhiteSpace(sectionTitle))
        {
            highlightedLearningAnswerTopic = topic;
            highlightedLearningAnswerSection = sectionTitle;
        }
        else if (!highlightAnswer)
        {
            highlightedLearningAnswerTopic = null;
            highlightedLearningAnswerSection = null;
        }
        if (!string.IsNullOrWhiteSpace(sectionTitle))
        {
            selectedLearningSections[topic] = sectionTitle;
        }
        if (viewedLearningTopics.Add(topic))
        {
            SaveLearningProgress();
        }

        (string title, string summary, string body, string tip, string glyph) = topic switch
        {
            "first" => ("Your First Vault", "Create a protected space in four clear steps.", "VaultKind guides you through naming the vault, choosing its encrypted storage location, reviewing the details, and protecting it with a password.", "Choose a location that is backed up or synchronized, then save the recovery key somewhere separate from the vault.", "\uE710"),
            "recovery" => ("Recovery Keys", "Restore access if you ever forget a vault password.", "A recovery key can restore access when you forget your password. It cannot be recreated after access is lost.", "Keep a printed or offline copy in a secure place. Never store the only copy inside the vault it protects.", "\uE8D7"),
            "cloud" => ("Cloud Storage", "Keep encrypted data synchronized with the provider you already use.", "Place the encrypted storage folder inside OneDrive, Dropbox, Google Drive, or another synchronized folder. Your provider receives encrypted data—not your readable files.", "Allow synchronization to finish before shutting down or making changes from another device.", "\uE753"),
            "drive" => ("Virtual Drives", "Work with readable files through a familiar Windows drive.", "After unlocking, VaultKind opens a familiar virtual drive. This is where you read, edit, add, and organize your files.", "Work in the virtual drive, not directly inside the encrypted storage folder. Lock the vault when finished.", "\uE7C3"),
            "security" => ("Security Tips", "Simple habits that strengthen the protection around your vaults.", "Use a unique password, protect your recovery key, keep reliable backups, install trusted Windows updates, and lock vaults you are no longer using.", "VaultKind protects file contents, but device security and safe backups remain important parts of your protection.", "\uE83D"),
            "keyboard" => ("Keyboard Shortcuts", "Navigate VaultKind efficiently without leaving the keyboard.", string.Empty, string.Empty, "\uE765"),
            "faq" => ("FAQ", "Straight answers to common VaultKind questions.", "Does VaultKind upload my files?\nNo. VaultKind encrypts locally. Your existing cloud application handles synchronization if you choose a cloud folder.\n\nCan VaultKind reset my password?\nNo. Use your recovery key to restore access if you forget the password.\n\nCan I use the same vault on another Windows device?\nYes. Let the encrypted folder synchronize, then connect that existing vault on the other device.", "VaultKind is desktop first, Windows focused, and private by default.", "\uE897"),
            _ => ("How VaultKind Works", "Understand what is encrypted, where it is stored, and how you safely access it.", string.Empty, string.Empty, "\uE72E")
        };

        LearningTopicTitle.Text = title;
        LearningTopicSummary.Text = summary;
        LearningTopicIcon.Glyph = glyph;
        LearningHowContent.Visibility = topic == "how" ? Visibility.Visible : Visibility.Collapsed;
        LearningSimpleContent.Visibility = topic == "how" ? Visibility.Collapsed : Visibility.Visible;
        if (LearningArticles.TryGetValue(topic, out LearningArticle? article))
        {
            LearningBodyText.Text = article.Introduction;
            LearningTipText.Text = article.Tip;
            RenderLearningArticle(article);
        }
        else
        {
            LearningBodyText.Text = body;
            LearningTipText.Text = tip;
            LearningArticleSectionsPanel.Children.Clear();
        }

        foreach ((Button button, string id, string _, FontIcon check) in LearningTopicButtons())
        {
            bool selected = id == topic;
            button.Background = selected ? PaletteBrush("SelectedSurfaceBrush") : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderBrush = selected ? PaletteBrush("BrandBlueBrush") : PaletteBrush("CardBorderBrush");
            button.BorderThickness = selected ? new Thickness(3, 0, 0, 0) : new Thickness(1);
            check.Visibility = viewedLearningTopics.Contains(id) ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateLearningTopicAutomationNames();
        UpdateLearningProgressDisplay();
        if (string.IsNullOrWhiteSpace(sectionTitle))
        {
            double targetOffset = learningTopicScrollOffsets.GetValueOrDefault(topic, 0);
            DispatcherQueue.TryEnqueue(() => LearningContentScroll.ChangeView(null, targetOffset, null, true));
        }
    }

    private void RenderLearningArticle(LearningArticle article)
    {
        LearningArticleSectionsPanel.Children.Clear();
        RenderSmartLearningArticle(article);
    }

    private void RenderSmartLearningArticle(LearningArticle article)
    {
        learningSectionViewEntries.Clear();
        bool isFaq = selectedLearningTopic == "faq";
        Button? faqCategoryFocusTarget = null;

        var search = new TextBox
        {
            PlaceholderText = isFaq ? "Ask a question or search the FAQ" : $"Search {LearningTopicName(selectedLearningTopic)}",
            FontSize = 16,
            Padding = new Thickness(13, 10, 13, 10)
        };
        search.TextChanged += FilterLearningSections;
        AutomationProperties.SetName(search, isFaq ? "Search frequently asked questions" : $"Search {LearningTopicName(selectedLearningTopic)} guidance");
        LearningArticleSectionsPanel.Children.Add(search);

        if (isFaq)
        {
            var categories = new ItemsControl
            {
                ItemsPanel = (ItemsPanelTemplate)Resources["LearningCategoryWrapPanel"],
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            foreach ((string id, string label) in new[]
            {
                ("all", "All"),
                ("access", "Access"),
                ("storage", "Storage"),
                ("privacy", "Privacy"),
                ("troubleshooting", "Troubleshooting")
            })
            {
                var button = new Button
                {
                    Tag = id,
                    Content = label,
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 8, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    BorderBrush = new SolidColorBrush(id == selectedFaqCategory
                        ? Color.FromArgb(255, 78, 161, 255)
                        : Color.FromArgb(255, 82, 93, 101)),
                    BorderThickness = new Thickness(id == selectedFaqCategory ? 2 : 1)
                };
                AutomationProperties.SetName(button, id == selectedFaqCategory ? $"{label}, selected" : label);
                if (restoreFaqCategoryFocusAfterRender && id == selectedFaqCategory)
                {
                    faqCategoryFocusTarget = button;
                }
                button.Click += SelectFaqCategory;
                categories.Items.Add(button);
            }
            LearningArticleSectionsPanel.Children.Add(categories);
        }

        string? selectedSection = selectedLearningSections.GetValueOrDefault(selectedLearningTopic);
        learningSectionResultCount = new TextBlock
        {
            Foreground = PaletteBrush("MutedTextBrush"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        learningCopyGuidanceStatus = new TextBlock
        {
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        AutomationProperties.SetLiveSetting(learningCopyGuidanceStatus, AutomationLiveSetting.Polite);
        RegisterLiveRegion(learningCopyGuidanceStatus);
        learningCopyGuidanceButton = new Button
        {
            Content = "Copy Guidance",
            Padding = new Thickness(14, 7, 14, 7),
            Visibility = string.IsNullOrWhiteSpace(selectedSection) ? Visibility.Collapsed : Visibility.Visible
        };
        learningCopyGuidanceButton.Click += CopySelectedLearningGuidance;
        learningSaveGuidanceButton = new Button
        {
            Content = "Save Guidance\u2026",
            Padding = new Thickness(14, 7, 14, 7),
            Visibility = string.IsNullOrWhiteSpace(selectedSection) ? Visibility.Collapsed : Visibility.Visible
        };
        learningSaveGuidanceButton.Click += SaveSelectedLearningGuidance;

        var learningResultRow = new Grid { RowSpacing = 10 };
        learningResultRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        learningResultRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        learningResultRow.Children.Add(learningSectionResultCount);
        var learningCopyActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                learningCopyGuidanceStatus,
                learningCopyGuidanceButton,
                learningSaveGuidanceButton
            }
        };
        learningCopyActions.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(learningCopyActions, 1);
        learningResultRow.Children.Add(learningCopyActions);
        LearningArticleSectionsPanel.Children.Add(learningResultRow);

        foreach (LearningSection section in article.Sections)
        {
            string category = isFaq ? FaqCategory(section.Title) : "guide";
            bool isExpanded = section.Title == selectedSection;
            bool isHighlightedAnswer = selectedLearningTopic == highlightedLearningAnswerTopic
                && section.Title == highlightedLearningAnswerSection;
            var answer = new TextBlock
            {
                Text = section.Body,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                LineHeight = 22,
                Foreground = PaletteBrush("MutedTextBrush"),
                Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 8, 26, 2)
            };
            var chevron = new FontIcon
            {
                Glyph = isExpanded ? "\uE70E" : "\uE70D",
                FontSize = 13,
                Foreground = PaletteBrush("BrandBlueBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            var header = new Grid { ColumnSpacing = 12 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = isHighlightedAnswer
                            ? "✓  ANSWER FOR THIS ISSUE"
                            : (isFaq ? FaqCategoryLabel(category) : "GUIDE").ToUpperInvariant(),
                        Foreground = isHighlightedAnswer ? PaletteBrush("PrimaryTextBrush") : PaletteBrush("BrandBlueBrush"),
                        FontSize = isHighlightedAnswer ? 12 : 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold
                    },
                    new TextBlock
                    {
                        Text = section.Title,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 16,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    }
                }
            });
            Grid.SetColumn(chevron, 1);
            header.Children.Add(chevron);

            var content = new StackPanel { Spacing = 3 };
            content.Children.Add(header);
            content.Children.Add(answer);
            var question = new Button
            {
                Tag = section.Title,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(16, 13, 16, 13),
                Background = isExpanded ? PaletteBrush("InfoSurfaceBrush") : PaletteBrush("ListSurfaceBrush"),
                BorderBrush = isHighlightedAnswer || isExpanded ? PaletteBrush("BrandBlueBrush") : PaletteBrush("CardBorderBrush"),
                BorderThickness = new Thickness(isHighlightedAnswer ? 3 : 1),
                CornerRadius = new CornerRadius(9),
                Content = content
            };
            AutomationProperties.SetName(question, LearningSectionAutomationName(section.Title, isExpanded, isHighlightedAnswer));
            question.Click += ToggleLearningSection;
            learningSectionViewEntries.Add(new LearningSectionViewEntry(section, category, question, answer, chevron, isHighlightedAnswer));
            LearningArticleSectionsPanel.Children.Add(question);
        }

        learningSectionNoResults = new TextBlock
        {
            Text = isFaq
                ? "No FAQ answer matches that search. Try fewer words, or open VaultKind Assistant for diagnostic guidance."
                : "No guidance in this chapter matches that search. Try fewer words or search another Learning Center chapter.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = PaletteBrush("MutedTextBrush"),
            FontSize = 14,
            Visibility = Visibility.Collapsed
        };
        LearningArticleSectionsPanel.Children.Add(learningSectionNoResults);
        ApplyLearningSectionFilter(search.Text);
        QueueTextScaleRefresh();

        if (faqCategoryFocusTarget is not null)
        {
            restoreFaqCategoryFocusAfterRender = false;
            FocusAfterNavigation(faqCategoryFocusTarget);
        }
        else if (!string.IsNullOrWhiteSpace(selectedSection))
        {
            LearningSectionViewEntry? selectedEntry = learningSectionViewEntries.FirstOrDefault(entry => entry.Section.Title == selectedSection);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (selectedEntry is null)
                {
                    return;
                }

                selectedEntry.Button.Focus(FocusState.Programmatic);
                selectedEntry.Button.StartBringIntoView(new BringIntoViewOptions
                {
                    AnimationDesired = true,
                    // Deep-linked answers can be taller than the viewport's remaining
                    // space. Anchor them near the top so the complete guidance receives
                    // the largest possible reading area without an extra manual scroll.
                    VerticalAlignmentRatio = 0.03
                });
            });
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => search.Focus(FocusState.Programmatic));
        }
    }

    private void SelectFaqCategory(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string category })
        {
            return;
        }

        selectedFaqCategory = category;
        restoreFaqCategoryFocusAfterRender = true;
        if (LearningArticles.TryGetValue("faq", out LearningArticle? article))
        {
            RenderLearningArticle(article);
        }
    }

    private void FilterLearningSections(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox search)
        {
            ApplyLearningSectionFilter(search.Text);
        }
    }

    private void ApplyLearningSectionFilter(string query)
    {
        string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int visible = 0;
        foreach (LearningSectionViewEntry entry in learningSectionViewEntries)
        {
            bool categoryMatches = selectedLearningTopic != "faq" || selectedFaqCategory == "all" || entry.Category == selectedFaqCategory;
            string searchable = $"{entry.Section.Title} {entry.Section.Body}";
            bool textMatches = terms.All(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase));
            bool matches = categoryMatches && textMatches;
            entry.Button.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            visible += matches ? 1 : 0;
        }

        if (learningSectionResultCount is not null)
        {
            string noun = selectedLearningTopic == "faq" ? "answer" : "section";
            learningSectionResultCount.Text = $"{visible} {noun}{(visible == 1 ? string.Empty : "s")}";
        }
        if (learningSectionNoResults is not null)
        {
            learningSectionNoResults.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ToggleLearningSection(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string question })
        {
            return;
        }

        string? selected = selectedLearningSections.GetValueOrDefault(selectedLearningTopic);
        selectedLearningSections[selectedLearningTopic] = selected == question ? null : question;
        foreach (LearningSectionViewEntry entry in learningSectionViewEntries)
        {
            bool expanded = entry.Section.Title == selectedLearningSections[selectedLearningTopic];
            entry.Answer.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            entry.Chevron.Glyph = expanded ? "\uE70E" : "\uE70D";
            entry.Button.Background = expanded ? PaletteBrush("InfoSurfaceBrush") : PaletteBrush("ListSurfaceBrush");
            entry.Button.BorderBrush = entry.IsHighlightedAnswer || expanded ? PaletteBrush("BrandBlueBrush") : PaletteBrush("CardBorderBrush");
            entry.Button.BorderThickness = new Thickness(entry.IsHighlightedAnswer ? 3 : 1);
            AutomationProperties.SetName(entry.Button, LearningSectionAutomationName(entry.Section.Title, expanded, entry.IsHighlightedAnswer));
        }

        bool hasSelectedGuidance = !string.IsNullOrWhiteSpace(selectedLearningSections[selectedLearningTopic]);
        if (learningCopyGuidanceButton is not null)
        {
            learningCopyGuidanceButton.Visibility = hasSelectedGuidance ? Visibility.Visible : Visibility.Collapsed;
        }
        if (learningSaveGuidanceButton is not null)
        {
            learningSaveGuidanceButton.Visibility = hasSelectedGuidance ? Visibility.Visible : Visibility.Collapsed;
        }
        if (learningCopyGuidanceStatus is not null)
        {
            learningCopyGuidanceStatus.Text = string.Empty;
            learningCopyGuidanceStatus.Visibility = Visibility.Collapsed;
        }
    }

    private void CopySelectedLearningGuidance(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedLearningGuidance(out string guidance, out _))
        {
            return;
        }

        try
        {
            DataPackage package = new();
            package.SetText(guidance);
            Clipboard.SetContent(package);
            ShowLearningCopyStatus("Guidance copied.", true);
        }
        catch (Exception)
        {
            ShowLearningCopyStatus("Clipboard unavailable.", false);
        }
    }

    private async void SaveSelectedLearningGuidance(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedLearningGuidance(out string guidance, out string suggestedFileName)
            || ((App)Application.Current).MainWindow is not Window window)
        {
            return;
        }

        try
        {
            FileSavePicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName
            };
            picker.FileTypeChoices.Add("Text document", new List<string> { ".txt" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, guidance);
            ShowLearningCopyStatus("Guidance saved.", true);
        }
        catch (Exception)
        {
            ShowLearningCopyStatus("Could not save guidance.", false);
        }
    }

    private bool TryGetSelectedLearningGuidance(out string guidance, out string suggestedFileName)
    {
        guidance = string.Empty;
        suggestedFileName = "VaultKind Guidance";
        string? selectedSection = selectedLearningSections.GetValueOrDefault(selectedLearningTopic);
        if (string.IsNullOrWhiteSpace(selectedSection)
            || !LearningArticles.TryGetValue(selectedLearningTopic, out LearningArticle? article))
        {
            return false;
        }

        LearningSection? section = article.Sections.FirstOrDefault(candidate => candidate.Title == selectedSection);
        if (section is null)
        {
            return false;
        }

        string topicName = LearningTopicName(selectedLearningTopic);
        guidance = $"VaultKind Learning Center\r\n{topicName}\r\n\r\n{section.Title}\r\n\r\n{section.Body}";
        string rawFileName = $"VaultKind - {topicName} - {section.Title}";
        suggestedFileName = string.Concat(rawFileName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return true;
    }

    private void ShowLearningCopyStatus(string message, bool success)
    {
        if (learningCopyGuidanceStatus is null)
        {
            return;
        }

        learningCopyGuidanceStatus.Text = message;
        learningCopyGuidanceStatus.Foreground = new SolidColorBrush(success
            ? Color.FromArgb(255, 58, 211, 111)
            : Color.FromArgb(255, 255, 96, 86));
        learningCopyGuidanceStatus.Visibility = Visibility.Visible;
    }

    private static string FaqCategory(string title)
    {
        if (title.Contains("password", StringComparison.OrdinalIgnoreCase)
            || title.Contains("recovery", StringComparison.OrdinalIgnoreCase)
            || title.Contains("another Windows", StringComparison.OrdinalIgnoreCase))
        {
            return "access";
        }
        if (title.Contains("stored", StringComparison.OrdinalIgnoreCase)
            || title.Contains("vault.cryptomator", StringComparison.OrdinalIgnoreCase)
            || title.Contains("rename", StringComparison.OrdinalIgnoreCase)
            || title.Contains("move", StringComparison.OrdinalIgnoreCase)
            || title.Contains("share", StringComparison.OrdinalIgnoreCase))
        {
            return "storage";
        }
        if (title.Contains("upload", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Activity", StringComparison.OrdinalIgnoreCase)
            || title.Contains("closes", StringComparison.OrdinalIgnoreCase))
        {
            return "privacy";
        }
        return "troubleshooting";
    }

    private static string FaqCategoryLabel(string category) => category switch
    {
        "access" => "Access",
        "storage" => "Storage",
        "privacy" => "Privacy",
        _ => "Troubleshooting"
    };

    private void FilterLearningTopics(object sender, TextChangedEventArgs e)
    {
        string query = LearningSearch.Text.Trim();
        int visible = 0;
        foreach ((Button button, string id, string title, FontIcon _) in LearningTopicButtons())
        {
            bool matches = query.Length == 0 || LearningTopicMatches(id, title, query);
            button.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            visible += matches ? 1 : 0;
        }
        LearningNoResults.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool LearningTopicMatches(string id, string title, string query)
    {
        string searchableText;
        if (LearningArticles.TryGetValue(id, out LearningArticle? article))
        {
            searchableText = string.Join(' ',
                new[] { title, article.Introduction, article.Tip }
                    .Concat(article.Sections.SelectMany(section => new[] { section.Title, section.Body })));
        }
        else
        {
            searchableText = $"{title} encryption encrypted files cloud storage password unlock virtual drive readable view encrypted storage folder scrambled file names lock vault";
        }

        string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.All(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private void ResetLearningProgress(object sender, RoutedEventArgs e)
    {
        viewedLearningTopics.Clear();
        SaveLearningProgress();
        foreach ((Button _, string _, string _, FontIcon check) in LearningTopicButtons())
        {
            check.Visibility = Visibility.Collapsed;
        }
        UpdateLearningProgressDisplay();
        UpdateLearningTopicAutomationNames();
    }

    private void UpdateLearningProgressDisplay()
    {
        int viewed = viewedLearningTopics.Count;
        int topicCount = LearningTopicButtons().Length;
        bool complete = viewed == topicCount;
        LearningProgressBar.Value = viewed;
        LearningProgressBar.Foreground = complete
            ? new SolidColorBrush(Color.FromArgb(255, 73, 205, 112))
            : PaletteBrush("BrandBlueBrush");
        LearningProgressText.Text = complete
            ? $"All {topicCount} topics viewed — nice work"
            : $"{viewed} of {topicCount} topics viewed";
        LearningProgressText.Foreground = complete
            ? new SolidColorBrush(Color.FromArgb(255, 73, 205, 112))
            : PaletteBrush("MutedTextBrush");
        DashboardLearningProgressBar.Value = viewed;
        DashboardLearningProgressBar.Foreground = LearningProgressBar.Foreground;
        DashboardLearningTitle.Text = complete ? "Learning Center complete" : "Continue learning";
        DashboardLearningDescription.Text = complete
            ? "You have viewed every practical VaultKind topic. Revisit them whenever you need a refresher."
            : $"{viewed} of {topicCount} topics viewed. Continue with the next short, practical guide.";
        DashboardLearningButton.Content = complete ? "Review Topics" : "Continue";
        DashboardLearningIcon.Glyph = complete ? "\uE73E" : "\uE82D";
        DashboardLearningIcon.Foreground = complete
            ? new SolidColorBrush(Color.FromArgb(255, 73, 205, 112))
            : PaletteBrush("BrandBlueBrush");
        AutomationProperties.SetName(DashboardLearningProgressBar, complete
            ? $"Learning Center progress complete, all {topicCount} topics viewed"
            : $"Learning Center progress, {viewed} of {topicCount} topics viewed");
        AutomationProperties.SetName(DashboardLearningButton, complete
            ? "Review Learning Center topics"
            : "Continue to the next Learning Center topic");
        AutomationProperties.SetName(LearningProgressBar, complete
            ? $"Learning Center progress complete, all {topicCount} topics viewed"
            : $"Learning Center progress, {viewed} of {topicCount} topics viewed");
    }

    private void LoadLearningProgress()
    {
        try
        {
            if (!File.Exists(LearningProgressPath))
            {
                return;
            }

            string json = File.ReadAllText(LearningProgressPath);
            List<string>? stored = JsonSerializer.Deserialize<List<string>>(json);
            if (stored is not null)
            {
                string[] validTopics = LearningTopicButtons().Select(topic => topic.Id).ToArray();
                foreach (string topic in stored.Where(topic => validTopics.Contains(topic, StringComparer.Ordinal)))
                {
                    viewedLearningTopics.Add(topic);
                }
            }
        }
        catch (JsonException)
        {
            // Learning progress is optional and must never prevent VaultKind from starting.
        }
        catch (IOException)
        {
            // Learning progress is optional and must never prevent VaultKind from starting.
        }
        catch (UnauthorizedAccessException)
        {
            // Learning progress is optional and must never prevent VaultKind from starting.
        }
    }

    private void SaveLearningProgress()
    {
        try
        {
            string? directory = Path.GetDirectoryName(LearningProgressPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = LearningProgressPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(viewedLearningTopics.OrderBy(topic => topic)));
            File.Move(temporaryPath, LearningProgressPath, true);
        }
        catch (IOException)
        {
            // A progress write failure must not interrupt Learning Center navigation.
        }
        catch (UnauthorizedAccessException)
        {
            // A progress write failure must not interrupt Learning Center navigation.
        }
    }

    private (Button Button, string Id, string Title, FontIcon Check)[] LearningTopicButtons() =>
    [
        (LearningHowButton, "how", "How VaultKind Works", LearningHowCheck),
        (LearningFirstButton, "first", "Your First Vault", LearningFirstCheck),
        (LearningRecoveryButton, "recovery", "Recovery Keys", LearningRecoveryCheck),
        (LearningCloudButton, "cloud", "Cloud Storage", LearningCloudCheck),
        (LearningDriveButton, "drive", "Virtual Drives", LearningDriveCheck),
        (LearningSecurityButton, "security", "Security Tips", LearningSecurityCheck),
        (LearningKeyboardButton, "keyboard", "Keyboard Shortcuts", LearningKeyboardCheck),
        (LearningFaqButton, "faq", "FAQ", LearningFaqCheck)
    ];

    private void UpdateLearningTopicAutomationNames()
    {
        foreach ((Button button, string id, string title, FontIcon _) in LearningTopicButtons())
        {
            bool selected = string.Equals(id, selectedLearningTopic, StringComparison.Ordinal);
            bool viewed = viewedLearningTopics.Contains(id);
            string state = selected && viewed ? ", selected, viewed"
                : selected ? ", selected"
                : viewed ? ", viewed"
                : string.Empty;
            AutomationProperties.SetName(button, title + state);
        }
    }

    private static string LearningSectionAutomationName(string title, bool expanded, bool highlighted) =>
        $"{title}, {(expanded ? "expanded" : "collapsed")}{(highlighted ? ", answer for this issue" : string.Empty)}";

    private void ShowAssistant(object sender, RoutedEventArgs e)
    {
        LearningNavigationPanel.Visibility = Visibility.Collapsed;
        AssistantNavigationPanel.Visibility = Visibility.Visible;
        LearningContentScroll.Visibility = Visibility.Collapsed;
        AssistantContentScroll.Visibility = Visibility.Visible;
        ContextTitle.Text = "VaultKind Assistant";
        ContextSubtitle.Text = "Private, offline diagnostic guidance for common VaultKind problems.";
        ShowAssistantCaseList(selectedAssistantCategory);
        DispatcherQueue.TryEnqueue(() =>
        {
            AssistantContentScroll.ChangeView(null, 0, null, true);
            AssistantSearch.Focus(FocusState.Programmatic);
        });
    }

    private void ReturnToLearningCenter(object sender, RoutedEventArgs e) => ShowLearningCenter(sender, e);

    private void BrowseAssistantCases(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string category })
        {
            selectedAssistantCategory = category;
            ShowAssistantCaseList(category);
        }
    }

    private void ShowAssistantCaseList(string category)
    {
        AssistantBackToCasesButton.Visibility = Visibility.Collapsed;
        AssistantResultsPanel.Children.Clear();
        string query = AssistantSearch.Text.Trim();
        IEnumerable<AssistantCase> cases = query.Length > 0
            ? AssistantCases.Where(item => AssistantCaseMatches(item, query))
            : category == "all" ? AssistantCases : AssistantCases.Where(item => item.Category == category);
        List<AssistantCase> visibleCases = cases.ToList();
        string categoryName = category switch { "startup" => "Startup", "vault" => "Vault", "filesystem" => "Filesystem", "recovery" => "Recovery", _ => "All" };
        AssistantResultsTitle.Text = query.Length > 0
            ? $"Search results ({visibleCases.Count})"
            : $"{categoryName} diagnostic cases";

        foreach (AssistantCase item in visibleCases)
        {
            var caseContent = new Grid { ColumnSpacing = 10 };
            caseContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            caseContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            caseContent.Children.Add(new TextBlock
            {
                Text = item.Id,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255)),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            var caseTitle = new TextBlock { Text = item.Title, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(caseTitle, 1);
            caseContent.Children.Add(caseTitle);

            var button = new Button
            {
                Tag = item.Id,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(14, 10, 14, 10),
                Content = caseContent
            };
            AutomationProperties.SetName(button, $"Open {item.Id}, {item.Title}");
            button.Click += OpenAssistantCaseFromButton;
            AssistantResultsPanel.Children.Add(button);
        }

        if (visibleCases.Count == 0)
        {
            AssistantResultsPanel.Children.Add(new TextBlock
            {
                Text = "No matching diagnostic cases. Try fewer words or a broader description.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 193, 199)),
                FontSize = 15
            });
        }
        DispatcherQueue.TryEnqueue(() => AssistantContentScroll.ChangeView(null, 0, null, true));
        QueueTextScaleRefresh();
    }

    private void FilterAssistantCases(object sender, TextChangedEventArgs e)
    {
        ShowAssistantCaseList(selectedAssistantCategory);
    }

    private static bool AssistantCaseMatches(AssistantCase item, string query)
    {
        string searchableText = string.Join(' ', item.Id, item.Title, item.Cause, item.Checks, item.Fix, item.Keywords);
        string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.All(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenAssistantQuickCase(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) ShowAssistantCase(id, 100, "Based on the common problem area you selected. Run the local checks before treating this as confirmed.");
    }

    private void OpenAssistantCaseFromButton(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) ShowAssistantCase(id, 100, "You opened this reviewed diagnostic case directly.");
    }

    private void BackToAssistantCases(object sender, RoutedEventArgs e)
    {
        ShowAssistantCaseList(selectedAssistantCategory);
        FocusAfterNavigation(AssistantSearch);
    }

    private void AssistantSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            FindAssistantFix(sender, e);
            e.Handled = true;
        }
    }

    private void FindAssistantFix(object sender, RoutedEventArgs e)
    {
        string query = AssistantSearch.Text.Trim();
        if (query.Length == 0)
        {
            ShowAssistantMessage("Tell VaultKind what happened", "Enter an error code, error message, or a few words such as ‘cannot lock’ or ‘cloud conflict.’ Nothing you type here leaves this device.");
            return;
        }

        (AssistantCase? item, int score, string evidence) best = (null, 0, string.Empty);
        string normalized = query.ToLowerInvariant();
        foreach (AssistantCase item in AssistantCases)
        {
            int score = normalized.Equals(item.Id, StringComparison.OrdinalIgnoreCase) ? 100 : 0;
            var matches = item.Keywords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (score == 0 && item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) score = 85;
            if (score == 0 && matches.Length > 0) score = Math.Min(95, 58 + matches.Length * 12);
            if (score == 0)
            {
                int tokenMatches = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(token => token.Length >= 3 && (item.Title.Contains(token, StringComparison.OrdinalIgnoreCase) || item.Keywords.Contains(token, StringComparison.OrdinalIgnoreCase)));
                score = Math.Min(70, tokenMatches * 22);
            }
            if (score > best.score) best = (item, score, matches.Length > 0 ? $"Matched your description against: {string.Join(", ", matches)}" : $"Matched the wording in {item.Id}.");
        }

        if (best.item is null || best.score == 0)
        {
            ShowAssistantMessage("No close match yet", "Try fewer words or choose one of the common problem areas above. This local catalogue will grow as new issues and verified solutions are documented.");
            return;
        }
        ShowAssistantCase(best.item.Id, best.score, best.evidence);
    }

    private void ShowAssistantMessage(string title, string body)
    {
        AssistantResultsTitle.Text = title;
        AssistantBackToCasesButton.Visibility = Visibility.Visible;
        AssistantResultsPanel.Children.Clear();
        AssistantResultsPanel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromArgb(255, 185, 193, 199)), FontSize = 15 });
        FocusAfterNavigation(AssistantBackToCasesButton);
        QueueTextScaleRefresh();
    }

    private void ShowAssistantCase(string id, int score, string evidence)
    {
        AssistantCase item = AssistantCases.First(candidate => candidate.Id == id);
        AssistantResultsTitle.Text = $"{item.Id}  —  {item.Title}";
        AssistantBackToCasesButton.Visibility = Visibility.Visible;
        AssistantResultsPanel.Children.Clear();

        string confidence = score >= 80 ? "Strong match" : score >= 35 ? "Possible match" : "More checks needed";
        AssistantResultsPanel.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Left, BorderBrush = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 5, 11, 5), Child = new TextBlock { Text = confidence, Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112)), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } });
        AssistantResultsPanel.Children.Add(AssistantSection("LIKELY CAUSE", item.Cause));
        AssistantResultsPanel.Children.Add(AssistantSection("LOCAL CHECKS", item.Checks));
        AssistantResultsPanel.Children.Add(AssistantSection("WHY THIS MATCHED", evidence));
        AssistantResultsPanel.Children.Add(AssistantSection("SUGGESTED FIX", item.Fix));
        LearningDestination learningDestination = RelatedLearningDestination(item);
        AssistantResultsPanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 37, 57, 74)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 54, 130, 198)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    AssistantLearningText(learningDestination),
                    AssistantLearningButton(learningDestination)
                }
            }
        });
        DispatcherQueue.TryEnqueue(() => AssistantContentScroll.ChangeView(null, 0, null, true));
        FocusAfterNavigation(AssistantBackToCasesButton);
        QueueTextScaleRefresh();
    }

    private static StackPanel AssistantLearningText(LearningDestination destination) => new()
    {
        Spacing = 3,
        Children =
        {
            new TextBlock
            {
                Text = "Learn more",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            },
            new TextBlock
            {
                Text = $"Open {LearningTopicName(destination.Topic)} directly at “{destination.Section}” without leaving VaultKind.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 190, 211, 230)),
                FontSize = 14
            }
        }
    };

    private Button AssistantLearningButton(LearningDestination destination)
    {
        var button = new Button
        {
            Tag = destination,
            Content = $"Open {LearningTopicName(destination.Topic)}",
            Padding = new Thickness(16, 9, 16, 9),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetColumn(button, 1);
        button.Click += OpenRelatedLearningTopic;
        AutomationProperties.SetName(button, $"Open {LearningTopicName(destination.Topic)} at {destination.Section}");
        return button;
    }

    private void OpenRelatedLearningTopic(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LearningDestination destination })
        {
            return;
        }

        ShowLearningCenter(sender, e);
        ShowLearningTopic(destination.Topic, destination.Section, highlightAnswer: true);
    }

    private static LearningDestination RelatedLearningDestination(AssistantCase item) => item.Id switch
    {
        "VK-0001" => new("faq", "What if VaultKind settings cannot be loaded?"),
        "VK-0002" => new("security", "Blocked or missing app components"),
        "VK-1001" => new("faq", "Why was the correct password rejected?"),
        "VK-1002" => new("cloud", "Offline files"),
        "VK-1003" => new("faq", "What if a vault configuration is missing or invalid?"),
        "VK-1004" => new("first", "After setup"),
        "VK-2001" => new("security", "Folder permissions and write access"),
        "VK-2002" => new("cloud", "Storage space and quota"),
        "VK-2003" => new("drive", "If locking fails"),
        "VK-2004" => new("cloud", "Let synchronization finish"),
        "VK-2005" => new("drive", "Drive availability"),
        "VK-3001" => new("recovery", "How recovery works"),
        "VK-3002" => new("security", "Vault integrity and safe verification"),
        _ => new("faq", "Where are my files actually stored?")
    };

    private static string LearningTopicName(string topic) => topic switch
    {
        "first" => "Your First Vault",
        "recovery" => "Recovery Keys",
        "cloud" => "Cloud Storage",
        "drive" => "Virtual Drives",
        "security" => "Security Tips",
        "keyboard" => "Keyboard Shortcuts",
        _ => "FAQ"
    };

    private static LearningArticle CreateKeyboardControlsLearningArticle()
    {
        KeyboardControlsGuide guide = KeyboardControlsDocument.Load(typeof(MainPage).Assembly);
        return new LearningArticle(
            guide.Introduction,
            guide.Sections.Select(section => new LearningSection(section.Title, section.Body)).ToArray(),
            guide.Tip);
    }

    private static StackPanel AssistantSection(string label, string body) => new()
    {
        Spacing = 5,
        Children =
        {
            new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255)), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
            new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 15, LineHeight = 23 }
        }
    };

    private void ShowRecoveryHub(object sender, RoutedEventArgs e)
    {
        recoveryOpenedFromVaultManagement = false;
        activeVault = null;
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Visible;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Repair or Recover";
        ContextSubtitle.Text = "Restore access, reconnect a vault, or inspect its health without guesswork.";
        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
        DispatcherQueue.TryEnqueue(() => RecoveryHubView.ChangeView(null, 0, null, true));
        FocusAfterNavigation(RecoveryPasswordButton);
    }

    private async void ShowRecoveryReset(object sender, RoutedEventArgs e)
    {
        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        if (snapshot.ConnectionState == BackendConnectionState.Ready)
        {
            ApplySnapshot(snapshot);
        }

        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Visible;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Restore Password Access";
        ContextSubtitle.Text = "Use a saved recovery key to choose a new password for a locked vault.";
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");

        RecoveryVaultPicker.Items.Clear();
        foreach (VaultSummary vault in knownVaults.Where(vault => vault.State.Equals("locked", StringComparison.OrdinalIgnoreCase)))
        {
            RecoveryVaultPicker.Items.Add(new ComboBoxItem { Content = $"{vault.Name}  —  {vault.Path}", Tag = vault.Id });
        }

        RecoveryVaultPicker.SelectedIndex = RecoveryVaultPicker.Items.Count == 1 ? 0 : -1;
        if (!string.IsNullOrWhiteSpace(recoveryTargetVaultId))
        {
            for (int index = 0; index < RecoveryVaultPicker.Items.Count; index++)
            {
                if (RecoveryVaultPicker.Items[index] is ComboBoxItem item && recoveryTargetVaultId.Equals(item.Tag as string, StringComparison.Ordinal))
                {
                    RecoveryVaultPicker.SelectedIndex = index;
                    break;
                }
            }
        }
        recoveryTargetVaultId = null;
        RecoveryKeyInput.Text = string.Empty;
        RecoveryNewPassword.Password = string.Empty;
        RecoveryConfirmPassword.Password = string.Empty;
        RecoveryAcknowledge.IsChecked = false;
        RecoveryStatus.Text = RecoveryVaultPicker.Items.Count == 0
            ? "No locked vaults are currently available. Lock the vault first, or reconnect it from the previous screen."
            : string.Empty;
        RecoveryStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
        RecoveryStatus.Visibility = RecoveryVaultPicker.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecoveryResetBackButton.Content = recoveryOpenedFromVaultManagement ? "‹ Back to Vault Management" : "Back";
        AutomationProperties.SetName(RecoveryResetBackButton, recoveryOpenedFromVaultManagement ? "Back to Vault Management" : "Back to recovery choices");
        UpdateRecoveryForm();
        DispatcherQueue.TryEnqueue(() => RecoveryResetView.ChangeView(null, 0, null, true));
        FocusAfterNavigation(RecoveryKeyInput);
    }

    private void LeaveRecoveryReset(object sender, RoutedEventArgs e)
    {
        if (recoveryOpenedFromVaultManagement && activeVault is not null)
        {
            RecoveryResetView.Visibility = Visibility.Collapsed;
            recoveryOpenedFromVaultManagement = false;
            ShowVaultManagement(sender, e);
            VaultManagementView.ChangeView(null, 0, null, true);
            FocusAfterNavigation(ManagedRecoveryButton);
            return;
        }

        ShowRecoveryHub(sender, e);
    }

    private void RecoveryFormChanged(object sender, RoutedEventArgs e) => UpdateRecoveryForm();

    private void RecoveryPasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && RecoveryResetButton.IsEnabled)
        {
            e.Handled = true;
            ResetVaultPassword(sender, new RoutedEventArgs());
        }
    }

    private void UpdateRecoveryForm()
    {
        bool passwordLongEnough = RecoveryNewPassword.Password.Length >= 8;
        bool passwordsMatch = passwordLongEnough && RecoveryNewPassword.Password == RecoveryConfirmPassword.Password;
        RecoveryPasswordStatus.Text = RecoveryNewPassword.Password.Length == 0 && RecoveryConfirmPassword.Password.Length == 0
            ? "Use at least 8 characters."
            : !passwordLongEnough
                ? "Use at least 8 characters."
                : passwordsMatch
                    ? "✓ Passwords match"
                    : "The passwords do not match yet.";
        RecoveryPasswordStatus.Foreground = new SolidColorBrush(passwordsMatch
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 174, 183, 190));
        RecoveryResetButton.IsEnabled = RecoveryVaultPicker.SelectedItem is ComboBoxItem
            && !string.IsNullOrWhiteSpace(RecoveryKeyInput.Text)
            && passwordsMatch
            && RecoveryAcknowledge.IsChecked == true
            && !RecoveryProgress.IsActive;
    }

    private async void ResetVaultPassword(object sender, RoutedEventArgs e)
    {
        if (RecoveryVaultPicker.SelectedItem is not ComboBoxItem selected || selected.Tag is not string vaultId || !RecoveryResetButton.IsEnabled)
        {
            return;
        }

        string recoveryKey = RecoveryKeyInput.Text;
        string newPassword = RecoveryNewPassword.Password;
        RecoveryResetButton.IsEnabled = false;
        RecoveryProgress.IsActive = true;
        RecoveryProgress.Visibility = Visibility.Visible;
        RecoveryStatus.Text = "Checking the recovery key and preparing a protected backup...";
        RecoveryStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        RecoveryStatus.Visibility = Visibility.Visible;

        VaultCommandResult result = await backend.ResetPasswordAsync(vaultId, recoveryKey, newPassword);
        recoveryKey = string.Empty;
        newPassword = string.Empty;
        RecoveryProgress.IsActive = false;
        RecoveryProgress.Visibility = Visibility.Collapsed;

        if (!result.Succeeded)
        {
            RecoveryStatus.Text = result.Error switch
            {
                "invalid_recovery_key" => "That recovery key is invalid or belongs to a different vault. Nothing was changed.",
                "vault_unlocked" => "Lock this vault before restoring password access.",
                "vault_not_found" => "This vault is no longer connected to VaultKind.",
                "recovery_write_failed" => "VaultKind could not safely update the master-key file. Nothing was intentionally changed; review the vault folder and try again.",
                "unknown_operation" => "The local vault engine is from an older build. Restart VaultKind, then try again.",
                "engine_unavailable" => "The local vault engine is unavailable. Restart VaultKind, then try again.",
                "timeout" => "Password recovery took too long. Return to the vault and verify its state before trying again.",
                _ => "VaultKind could not restore password access. Nothing was intentionally changed."
            };
            RecoveryStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            UpdateRecoveryForm();
            return;
        }

        RecoveryKeyInput.Text = string.Empty;
        RecoveryNewPassword.Password = string.Empty;
        RecoveryConfirmPassword.Password = string.Empty;
        RecoveryAcknowledge.IsChecked = false;
        RecoveryStatus.Text = "✓ Password access restored. VaultKind backed up the previous master-key file; you can now unlock this vault with the new password.";
        RecoveryStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112));
        RecoveryStatus.Visibility = Visibility.Visible;
        string recoveredVaultName = knownVaults.FirstOrDefault(vault => vault.Id == vaultId)?.Name ?? "A vault";
        LogActivity("Password access restored", $"{recoveredVaultName} received a new password using its recovery key.", "recovery");
        UpdateRecoveryForm();
    }

    private async void ChooseExistingVaultFolder(object sender, RoutedEventArgs e)
    {
        if (((App)Application.Current).MainWindow is not Window window)
        {
            return;
        }

        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        selectedConnectVaultPath = folder.Path;
        ConnectVaultNameText.Text = Path.GetFileName(folder.Path.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : "Existing vault";
        ConnectVaultPathText.Text = folder.Path;
        ConnectVaultReviewCard.Visibility = Visibility.Visible;
        string? configurationFile = new[] { "vault.cryptomator", "masterkey.cryptomator" }
            .FirstOrDefault(fileName => File.Exists(Path.Combine(folder.Path, fileName)));
        if (configurationFile is null)
        {
            ConnectVaultConfigText.Text = "Not found";
            ConnectVaultConfigText.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            ConnectVaultStatus.Text = "This folder does not appear to contain a supported VaultKind vault. Choose the folder containing vault.cryptomator or masterkey.cryptomator.";
            ConnectVaultStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            ConnectVaultStatus.Visibility = Visibility.Visible;
            ConnectVaultStepText.Text = "STEP 1 OF 2";
            ConnectVaultSecondStepBar.Background = new SolidColorBrush(Color.FromArgb(255, 88, 97, 104));
            ConnectVaultButton.IsEnabled = false;
            return;
        }

        ConnectVaultConfigText.Text = configurationFile;
        ConnectVaultConfigText.Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112));
        ConnectVaultStatus.Text = "Supported vault configuration found. Review the location, then connect it to VaultKind.";
        ConnectVaultStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112));
        ConnectVaultStatus.Visibility = Visibility.Visible;
        ConnectVaultStepText.Text = "STEP 2 OF 2";
        ConnectVaultSecondStepBar.Background = new SolidColorBrush(Color.FromArgb(255, 43, 131, 231));
        ConnectVaultButton.IsEnabled = true;
        ConnectVaultButton.Focus(FocusState.Programmatic);
    }

    private async void ConnectExistingVault(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(selectedConnectVaultPath))
        {
            return;
        }

        ConnectVaultButton.IsEnabled = false;
        ConnectVaultProgress.IsActive = true;
        ConnectVaultProgress.Visibility = Visibility.Visible;
        ConnectVaultStatus.Text = "Checking the selected vault with the local VaultKind engine...";
        ConnectVaultStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        ConnectVaultStatus.Visibility = Visibility.Visible;
        VaultCreateResult result = await backend.ConnectAsync(selectedConnectVaultPath);
        ConnectVaultProgress.IsActive = false;
        ConnectVaultProgress.Visibility = Visibility.Collapsed;
        if (!result.Succeeded)
        {
            ConnectVaultStatus.Text = result.Error switch
            {
                "already_connected" => "This vault is already connected to VaultKind.",
                "not_a_vault" => "That folder does not contain a supported encrypted vault.",
                "location_unavailable" => "That vault folder is no longer available.",
                "timeout" => "Connecting the vault took too long. Nothing was changed.",
                "engine_unavailable" => "The local VaultKind engine is unavailable. Nothing was changed.",
                _ => "VaultKind could not connect this vault. Its encrypted files were not changed."
            };
            ConnectVaultStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            ConnectVaultStatus.Visibility = Visibility.Visible;
            ConnectVaultButton.IsEnabled = true;
            return;
        }

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        ApplySnapshot(snapshot);
        VaultSummary? connected = snapshot.Vaults.FirstOrDefault(vault => vault.Id == result.VaultId);
        LogActivity("Vault connected", $"{connected?.Name ?? "An existing vault"} was added to this VaultKind installation.", "connect");
        if (connected is not null && FindVaultButton(connected.Id) is Button button)
        {
            ShowVault(connected, button);
        }
        else
        {
            ShowDashboard(sender, e);
        }
    }

    private async void ShowCreateVault(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Visible;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Create a Vault";
        ContextSubtitle.Text = "Set up a new encrypted space in four clear steps.";
        CreateVaultNameInput.Text = string.Empty;
        CreateVaultNameStatus.Visibility = Visibility.Collapsed;
        CreateVaultNextButton.IsEnabled = false;
        selectedCreateVaultParentPath = null;
        CreateVaultStoragePath.Text = string.Empty;
        CreateVaultStorageStatus.Visibility = Visibility.Collapsed;
        CreateVaultStorageNextButton.IsEnabled = false;
        CreateVaultPasswordInput.Password = string.Empty;
        CreateVaultPasswordConfirmInput.Password = string.Empty;
        CreateRecoveryKeyOption.IsChecked = false;
        CreateWithoutRecoveryKeyOption.IsChecked = false;
        CreateVaultFinalButton.IsEnabled = false;
        await Task.Delay(100);
        CreateVaultNameInput.Focus(FocusState.Programmatic);
    }

    private void CreateVaultNameChanged(object sender, TextChangedEventArgs e)
    {
        string name = CreateVaultNameInput.Text.Trim();
        bool valid = name.Length > 0 && name.Length <= 64 && name.All(character =>
            char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character is '-' or '_');
        CreateVaultNextButton.IsEnabled = valid;
        CreateVaultNameStatus.Text = valid ? "✓ Valid vault name" : name.Length == 0 ? string.Empty : "Use only the characters listed.";
        CreateVaultNameStatus.Foreground = new SolidColorBrush(valid
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 102, 93));
        CreateVaultNameStatus.Visibility = name.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CreateVaultNameKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && CreateVaultNextButton.IsEnabled)
        {
            e.Handled = true;
            ShowCreateVaultStorage(sender, e);
        }
    }

    private void ShowCreateVaultStorage(object sender, RoutedEventArgs e)
    {
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Visible;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Create a Vault";
        ContextSubtitle.Text = "Set up a new encrypted space in four clear steps.";
        RefreshCreateVaultStorageLocation();
        FocusAfterNavigation(CreateVaultStorageFolderButton);
    }

    private void ReturnToCreateVaultName(object sender, RoutedEventArgs e)
    {
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Visible;
        DispatcherQueue.TryEnqueue(() =>
        {
            CreateVaultNameInput.Focus(FocusState.Programmatic);
            CreateVaultNameInput.Select(CreateVaultNameInput.Text.Length, 0);
        });
    }

    private async void ChooseCreateVaultFolder(object sender, RoutedEventArgs e)
    {
        if (((App)Application.Current).MainWindow is not Window window)
        {
            return;
        }

        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));

        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        selectedCreateVaultParentPath = folder.Path;
        RefreshCreateVaultStorageLocation();
    }

    private void RefreshCreateVaultStorageLocation()
    {
        if (string.IsNullOrWhiteSpace(selectedCreateVaultParentPath))
        {
            CreateVaultStoragePath.Text = string.Empty;
            CreateVaultStorageStatus.Visibility = Visibility.Collapsed;
            CreateVaultStorageNextButton.IsEnabled = false;
            return;
        }

        string targetPath = Path.Combine(selectedCreateVaultParentPath, CreateVaultNameInput.Text.Trim());
        CreateVaultStoragePath.Text = targetPath;

        bool targetAlreadyExists;
        try
        {
            targetAlreadyExists = Directory.Exists(targetPath) || File.Exists(targetPath);
        }
        catch (UnauthorizedAccessException)
        {
            targetAlreadyExists = true;
        }

        bool suitable = !targetAlreadyExists;
        CreateVaultStorageStatus.Text = suitable ? "✓ Suitable location for your vault" : "A file or folder already uses this vault name. Choose a different name or parent folder.";
        CreateVaultStorageStatus.Foreground = new SolidColorBrush(suitable
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 102, 93));
        CreateVaultStorageStatus.Visibility = Visibility.Visible;
        CreateVaultStorageNextButton.IsEnabled = suitable;
    }

    private void ShowCreateVaultReview(object sender, RoutedEventArgs e)
    {
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Visible;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        CreateVaultReviewName.Text = CreateVaultNameInput.Text.Trim();
        CreateVaultReviewPath.Text = CreateVaultStoragePath.Text;
        FocusAfterNavigation(CreateVaultShortNamesOption);
    }

    private void CreateVaultShortNamesChanged(object sender, RoutedEventArgs e)
    {
        CreateVaultShortNamesNotice.Visibility = CreateVaultShortNamesOption.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ReturnToCreateVaultStorage(object sender, RoutedEventArgs e)
    {
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Visible;
        FocusAfterNavigation(CreateVaultStorageFolderButton);
    }

    private async void ShowCreateVaultProtection(object sender, RoutedEventArgs e)
    {
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Visible;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        await Task.Delay(100);
        CreateVaultPasswordInput.Focus(FocusState.Programmatic);
    }

    private void ReturnToCreateVaultReview(object sender, RoutedEventArgs e)
    {
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Visible;
        FocusAfterNavigation(CreateVaultShortNamesOption);
    }

    private void CreateVaultPasswordChanged(object sender, RoutedEventArgs e)
    {
        string password = CreateVaultPasswordInput.Password;
        string confirmation = CreateVaultPasswordConfirmInput.Password;
        int score = 0;
        if (password.Length >= 8) score++;
        if (password.Length >= 12) score++;
        if (password.Any(char.IsUpper) && password.Any(char.IsLower)) score++;
        if (password.Any(char.IsDigit) && password.Any(character => !char.IsLetterOrDigit(character))) score++;

        Border[] strengthSegments = [PasswordStrengthOne, PasswordStrengthTwo, PasswordStrengthThree, PasswordStrengthFour];
        Color strengthColor = score switch
        {
            <= 1 => Color.FromArgb(255, 255, 102, 93),
            2 => Color.FromArgb(255, 232, 176, 52),
            3 => Color.FromArgb(255, 78, 161, 255),
            _ => Color.FromArgb(255, 73, 205, 112)
        };
        for (int index = 0; index < strengthSegments.Length; index++)
        {
            strengthSegments[index].Background = new SolidColorBrush(index < score ? strengthColor : Color.FromArgb(255, 88, 97, 104));
        }

        CreateVaultPasswordStrength.Text = password.Length < 8 ? "Use at least 8 characters" : score switch
        {
            1 => "Weak",
            2 => "Fair",
            3 => "Good",
            _ => "Strong"
        };
        CreateVaultPasswordStrength.Foreground = new SolidColorBrush(password.Length < 8
            ? Color.FromArgb(255, 170, 178, 184)
            : strengthColor);

        bool hasConfirmation = confirmation.Length > 0;
        bool passwordsMatch = hasConfirmation && password == confirmation;
        CreateVaultPasswordMatchStatus.Text = passwordsMatch ? "✓ Passwords match" : "Passwords do not match";
        CreateVaultPasswordMatchStatus.Foreground = new SolidColorBrush(passwordsMatch
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 102, 93));
        CreateVaultPasswordMatchStatus.Visibility = hasConfirmation ? Visibility.Visible : Visibility.Collapsed;
        UpdateCreateVaultFinalButton();
    }

    private void CreateVaultRecoveryChanged(object sender, RoutedEventArgs e)
    {
        UpdateCreateVaultFinalButton();
    }

    private void UpdateCreateVaultFinalButton()
    {
        bool validPassword = CreateVaultPasswordInput.Password.Length >= 8;
        bool passwordsMatch = validPassword && CreateVaultPasswordInput.Password == CreateVaultPasswordConfirmInput.Password;
        bool recoverySelected = CreateRecoveryKeyOption.IsChecked == true || CreateWithoutRecoveryKeyOption.IsChecked == true;
        CreateVaultFinalButton.IsEnabled = passwordsMatch && recoverySelected;
    }

    private void CreateVaultPasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        if (ReferenceEquals(sender, CreateVaultPasswordInput))
        {
            CreateVaultPasswordConfirmInput.Focus(FocusState.Keyboard);
        }
        else if (CreateVaultPasswordInput.Password == CreateVaultPasswordConfirmInput.Password && CreateVaultPasswordInput.Password.Length >= 8)
        {
            CreateRecoveryKeyOption.Focus(FocusState.Keyboard);
        }
    }

    private async void SubmitCreateVault(object sender, RoutedEventArgs e)
    {
        if (!CreateVaultFinalButton.IsEnabled || string.IsNullOrWhiteSpace(CreateVaultStoragePath.Text))
        {
            return;
        }

        string password = CreateVaultPasswordInput.Password;
        CreateVaultFinalButton.IsEnabled = false;
        CreateVaultCreationProgress.IsActive = true;
        CreateVaultCreationProgress.Visibility = Visibility.Visible;
        CreateVaultCreationStatus.Text = "Creating your encrypted vault locally…";
        CreateVaultCreationStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        CreateVaultCreationStatus.Visibility = Visibility.Visible;

        VaultCreateResult result = await backend.CreateAsync(
            CreateVaultStoragePath.Text,
            password,
            CreateRecoveryKeyOption.IsChecked == true,
            CreateVaultShortNamesOption.IsChecked == true);
        password = string.Empty;
        CreateVaultCreationProgress.IsActive = false;
        CreateVaultCreationProgress.Visibility = Visibility.Collapsed;

        if (!result.Succeeded)
        {
            CreateVaultCreationStatus.Text = FriendlyCreateError(result.Error);
            CreateVaultCreationStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            UpdateCreateVaultFinalButton();
            return;
        }

        CreateVaultPasswordInput.Password = string.Empty;
        CreateVaultPasswordConfirmInput.Password = string.Empty;

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        for (int attempt = 0; attempt < 10 && snapshot.Vaults.All(vault => vault.Id != result.VaultId); attempt++)
        {
            await Task.Delay(100);
            snapshot = await backend.GetSnapshotAsync();
        }
        ApplySnapshot(snapshot);
        createdVault = snapshot.Vaults.FirstOrDefault(vault => vault.Id == result.VaultId)
            ?? snapshot.Vaults.FirstOrDefault(vault => vault.Path.Equals(CreateVaultStoragePath.Text, StringComparison.OrdinalIgnoreCase));
        LogActivity("Vault created", $"{CreateVaultNameInput.Text.Trim()} was created and added to VaultKind.", "create");

        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Visible;
        ContextTitle.Text = "Vault Created";
        ContextSubtitle.Text = "Your new encrypted space is ready when you are.";
        CreatedVaultNameText.Text = CreateVaultNameInput.Text.Trim();
        CreatedRecoveryKeyText.Text = result.RecoveryKey ?? string.Empty;
        bool hasRecoveryKey = !string.IsNullOrWhiteSpace(result.RecoveryKey);
        CreatedRecoveryKeyPanel.Visibility = hasRecoveryKey ? Visibility.Visible : Visibility.Collapsed;
        CreatedRecoveryKeyCopyStatus.Text = string.Empty;
        CreatedRecoveryKeyCopyStatus.Visibility = Visibility.Collapsed;
        CreatedRecoveryKeySaved.IsChecked = false;
        CreatedVaultDoneButton.IsEnabled = !hasRecoveryKey;
        CreatedVaultUnlockButton.IsEnabled = !hasRecoveryKey;
        FocusAfterNavigation(hasRecoveryKey ? CreatedRecoveryKeyText : CreatedVaultUnlockButton);
    }

    private void CopyCreatedRecoveryKey(object sender, RoutedEventArgs e)
    {
        CreatedRecoveryKeyCopyStatus.Text = string.Empty;
        CreatedRecoveryKeyCopyStatus.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(CreatedRecoveryKeyText.Text))
        {
            return;
        }

        try
        {
            CopySensitiveTextToClipboard(CreatedRecoveryKeyText.Text, CreatedRecoveryKeyCopyStatus);

            CreatedRecoveryKeyCopyStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 211, 111));
            CreatedRecoveryKeyCopyStatus.Text = "Recovery key copied. Clipboard clears in 60 seconds.";
        }
        catch (Exception)
        {
            CreatedRecoveryKeyCopyStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 91, 82));
            CreatedRecoveryKeyCopyStatus.Text = "VaultKind could not access the Windows clipboard. Select the recovery key text and press Ctrl+C.";
        }

        CreatedRecoveryKeyCopyStatus.Visibility = Visibility.Visible;
    }

    private void CreatedRecoveryKeySavedChanged(object sender, RoutedEventArgs e)
    {
        bool acknowledged = CreatedRecoveryKeySaved.IsChecked == true;
        CreatedVaultDoneButton.IsEnabled = acknowledged;
        CreatedVaultUnlockButton.IsEnabled = acknowledged;
    }

    private void FinishCreateVault(object sender, RoutedEventArgs e)
    {
        ShowDashboard(sender, e);
    }

    private void UnlockCreatedVault(object sender, RoutedEventArgs e)
    {
        if (createdVault is not null && FindVaultButton(createdVault.Id) is Button button)
        {
            ShowVault(createdVault, button);
            ShowUnlock(sender, e);
        }
        else
        {
            ShowDashboard(sender, e);
        }
    }

    private static string FriendlyCreateError(string? error) => error switch
    {
        "location_exists" => "That vault folder already exists. Go back and choose a different name or parent folder.",
        "invalid_path" => "That storage location is not valid. Go back and choose another folder.",
        "create_failed_folder" => "VaultKind could not create the selected folder. Check its permissions or choose another location.",
        "create_failed_masterkey" => "VaultKind could not securely write the vault key. Check the selected drive and try again.",
        "create_failed_recovery_key" => "The encrypted vault was created, but its recovery key could not be prepared. Do not reuse this folder; choose a new vault name and try again.",
        "create_failed_vault_structure" => "VaultKind could not initialize the encrypted vault structure. Do not reuse this folder; choose a new vault name and try again.",
        "create_failed_storage_readme" => "The encrypted vault was created, but its storage guide could not be written. Do not reuse this folder; choose a new vault name and try again.",
        "create_failed_registration" => "The encrypted vault was created, but VaultKind could not add it to the vault list. Use Connect an existing vault to add it safely.",
        "timeout" => "Vault creation took too long. Check the selected folder before trying again.",
        "engine_unavailable" => "The local VaultKind engine is unavailable. Nothing was created.",
        _ => "VaultKind could not finish creating this vault. Review the selected location and try again."
    };

    private void ShowVault(VaultSummary vault, Button selectedButton)
    {
        activeVault = vault;
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Visible;
        VaultManagementView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Vaults";
        ContextSubtitle.Text = "Unlock, manage, and review your encrypted vaults.";

        SelectedVaultName.Text = vault.Name;
        SelectedVaultPath.Text = vault.Path;
        ToolTipService.SetToolTip(SelectedVaultPath, vault.Path);
        SelectedVaultStatus.Text = FriendlyVaultState(vault.State).ToUpperInvariant();
        SelectedVaultIcon.Glyph = vault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase) ? "\uE785" : "\uE72E";

        bool unlocked = vault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase);
        SelectedVaultStateTitle.Text = unlocked ? "Your vault is open" : "Your vault is protected";
        SelectedVaultStateDescription.Text = unlocked
            ? "The readable view is available while this vault remains unlocked."
            : "Its readable view is closed. The encrypted files remain safely stored at the location above.";
        SelectedVaultStateIcon.Glyph = unlocked ? "\uE785" : "\uE72E";
        var stateColor = new SolidColorBrush(unlocked
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 78, 161, 255));
        SelectedVaultIcon.Foreground = stateColor;
        SelectedVaultStateIcon.Foreground = stateColor;
        SelectedVaultStatus.Foreground = stateColor;
        SelectedVaultStatusBorder.BorderBrush = stateColor;
        UnlockVaultButton.Visibility = vault.State.Equals("locked", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
        UnlockedVaultActions.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        UnlockedVaultTools.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        OpenDriveButton.IsEnabled = true;
        LockVaultButton.IsEnabled = true;
        VaultActionProgress.IsActive = false;
        VaultActionProgress.Visibility = Visibility.Collapsed;
        VaultActionStatus.Visibility = Visibility.Collapsed;
        RemoveVaultConfirmation.Visibility = Visibility.Collapsed;
        RemoveVaultNameInput.Text = string.Empty;
        RemoveVaultButton.Visibility = unlocked ? Visibility.Collapsed : Visibility.Visible;
        RemoveVaultButton.IsEnabled = true;
        ConfirmRemoveVaultButton.IsEnabled = false;

        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
        selectedButton.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
        selectedButton.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        selectedButton.BorderThickness = new Thickness(3, 0, 0, 0);
        AutomationProperties.SetName(selectedButton, $"{vault.Name}, selected, {FriendlyVaultState(vault.State)}, {vault.Path}");
        FocusAfterNavigation(unlocked ? OpenDriveButton : UnlockVaultButton);
    }

    private void ShowVaultManagement(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Visible;
        VaultManagementHome.Visibility = Visibility.Visible;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        VaultStatisticsPanel.Visibility = Visibility.Collapsed;
        VaultLocateEncryptedFilePanel.Visibility = Visibility.Collapsed;
        VaultDecryptFileNamePanel.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Manage Vault";
        ContextSubtitle.Text = "Share, recover, inspect, or remove this vault without leaving the main window.";

        ManagedVaultName.Text = activeVault.Name;
        ManagedVaultNameDisplay.Visibility = Visibility.Visible;
        ManagedVaultRenameEditor.Visibility = Visibility.Collapsed;
        ManagedVaultRenameStatus.Visibility = Visibility.Collapsed;
        ManagedVaultPath.Text = activeVault.Path;
        ToolTipService.SetToolTip(ManagedVaultPath, activeVault.Path);
        ManagedVaultStatus.Text = FriendlyVaultState(activeVault.State).ToUpperInvariant();
        bool unlocked = activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase);
        var stateColor = new SolidColorBrush(unlocked
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 78, 161, 255));
        ManagedVaultIcon.Glyph = unlocked ? "\uE785" : "\uE72E";
        ManagedVaultIcon.Foreground = stateColor;
        ManagedVaultStatus.Foreground = stateColor;
        ManagedVaultStatusBorder.BorderBrush = stateColor;
        ManagedRecoveryButton.IsEnabled = !unlocked;
        ManagedRecoveryButton.Content = unlocked ? "Lock Vault to Recover" : "Use Recovery Key";
        ManagedChangePasswordButton.IsEnabled = !unlocked;
        ManagedChangePasswordButton.Content = unlocked ? "Lock Vault to Change" : "Change Password";
        ManagedRemoveButton.IsEnabled = !unlocked;
        ManagedRemoveHint.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
        FocusAfterNavigation(ManagedVaultRenameButton);
    }

    private async void ShowVaultStatistics(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || !activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Visible;
        VaultManagementHome.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        VaultLocateEncryptedFilePanel.Visibility = Visibility.Collapsed;
        VaultDecryptFileNamePanel.Visibility = Visibility.Collapsed;
        VaultStatisticsPanel.Visibility = Visibility.Visible;
        ContextTitle.Text = "Vault Statistics";
        ContextSubtitle.Text = "View local read, write, cache, and access activity for this vault.";
        VaultStatisticsSubtitle.Text = $"Current local activity for {activeVault.Name}.";
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(RefreshVaultStatisticsButton);
        await RefreshVaultStatisticsAsync();
    }

    private void HideVaultStatistics(object sender, RoutedEventArgs e)
    {
        VaultStatisticsPanel.Visibility = Visibility.Collapsed;
        if (activeVault is not null && FindVaultButton(activeVault.Id) is Button button)
        {
            ShowVault(activeVault, button);
            FocusAfterNavigation(VaultStatisticsButton);
        }
    }

    private void ShowLocateEncryptedFile(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || !activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Visible;
        VaultManagementHome.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        VaultStatisticsPanel.Visibility = Visibility.Collapsed;
        VaultDecryptFileNamePanel.Visibility = Visibility.Collapsed;
        VaultLocateEncryptedFilePanel.Visibility = Visibility.Visible;
        ContextTitle.Text = "Locate Encrypted File";
        ContextSubtitle.Text = "Find the encrypted storage entry behind a readable file.";
        LocateEncryptedFileSubtitle.Text = $"Trace a readable file through {activeVault.Name} without changing it.";
        LocateReadableDriveHint.Text = $"The file picker opens directly in {activeVault.Name}'s readable drive. Choose the file by its familiar name; VaultKind will find its encrypted entry for you.";
        LocateEncryptedFileResult.Visibility = Visibility.Collapsed;
        LocateEncryptedFileStatusBorder.Visibility = Visibility.Collapsed;
        LocateEncryptedFileProgress.Visibility = Visibility.Collapsed;
        LocateEncryptedFileProgress.IsActive = false;
        ChooseReadableFileButton.IsEnabled = true;
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(ChooseReadableFileButton);
    }

    private async void OpenReadableDriveForLocate(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        ShowLocateEncryptedFileStatus("Opening this vault's readable Windows drive...", true);
        VaultCommandResult result = await backend.RevealAsync(activeVault.Id);
        ShowLocateEncryptedFileStatus(
            result.Succeeded
                ? "Readable drive opened. Choose a normal file from that drive, then return here for step 2."
                : FriendlyVaultActionError(result.Error, false),
            result.Succeeded);
    }

    private void HideLocateEncryptedFile(object sender, RoutedEventArgs e)
    {
        VaultLocateEncryptedFilePanel.Visibility = Visibility.Collapsed;
        if (activeVault is not null && FindVaultButton(activeVault.Id) is Button button)
        {
            ShowVault(activeVault, button);
            FocusAfterNavigation(VaultLocateEncryptedFileButton);
        }
    }

    private async void ChooseReadableFileToLocate(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        if (((App)Application.Current).MainWindow is not Window window)
        {
            ShowLocateEncryptedFileStatus("VaultKind could not open the Windows file picker.", false);
            return;
        }

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        VaultSummary? currentVault = snapshot.Vaults.FirstOrDefault(vault => vault.Id == activeVault.Id);
        string? readableRoot = currentVault?.MountPath;
        if (string.IsNullOrWhiteSpace(readableRoot))
        {
            ShowLocateEncryptedFileStatus("VaultKind could not determine this vault's readable Windows drive. Lock and unlock the vault, then try again.", false);
            return;
        }

        string? selectedPath;
        try
        {
            selectedPath = NativeFilePicker.PickFile(
                WinRT.Interop.WindowNative.GetWindowHandle(window),
                readableRoot,
                $"Choose a file from {activeVault.Name}",
                "Choose File");
        }
        catch (Exception)
        {
            ShowLocateEncryptedFileStatus("Windows could not open this vault's readable file picker. Lock and unlock the vault, then try again.", false);
            return;
        }

        if (selectedPath is null)
        {
            return;
        }

        ChooseReadableFileButton.IsEnabled = false;
        LocateEncryptedFileResult.Visibility = Visibility.Collapsed;
        LocateEncryptedFileProgress.IsActive = true;
        LocateEncryptedFileProgress.Visibility = Visibility.Visible;
        LocateEncryptedFileStatus.Text = "Locating the matching encrypted entry locally...";
        LocateEncryptedFileStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        LocateEncryptedFileStatusBorder.Background = new SolidColorBrush(Color.FromArgb(255, 41, 58, 74));
        LocateEncryptedFileStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 54, 125, 194));
        LocateEncryptedFileStatusBorder.Visibility = Visibility.Visible;

        FileNameDecryptResult result = await backend.LocateEncryptedFileAsync(activeVault.Id, selectedPath);
        ChooseReadableFileButton.IsEnabled = true;
        LocateEncryptedFileProgress.IsActive = false;
        LocateEncryptedFileProgress.Visibility = Visibility.Collapsed;

        if (!result.Succeeded || result.Mapping is null)
        {
            bool selectedEncryptedEntry = selectedPath.EndsWith(".c9r", StringComparison.OrdinalIgnoreCase);
            ShowLocateEncryptedFileStatus(result.Error switch
            {
                "foreign_file" when selectedEncryptedEntry => "That is an encrypted .c9r storage entry. Use Decrypt File Name for that file, or choose a normal file from the readable drive here.",
                "foreign_file" => "That file is outside this vault's open readable drive. Use step 1, then choose a normal file from the drive VaultKind opens.",
                "unsupported_mount" => "This vault's current drive type cannot expose an encrypted storage path.",
                "vault_locked" => "This vault is no longer open. Unlock it before locating an encrypted file.",
                "vault_not_found" => "This vault is no longer connected to VaultKind.",
                "invalid_request" => "Choose an existing file from the open readable drive.",
                "timeout" => "The local vault engine took too long to locate the encrypted entry.",
                _ => "VaultKind could not locate the encrypted entry. The readable file was not changed."
            }, false);
            return;
        }

        LocatedReadableFileNameText.Text = result.Mapping.CleartextName;
        LocatedEncryptedFilePathText.Text = result.Mapping.EncryptedName;
        LocateEncryptedFileResult.Visibility = Visibility.Visible;
        ShowLocateEncryptedFileStatus("Encrypted storage entry identified locally. Neither file nor path was changed.", true);
        FocusAfterNavigation(CopyLocatedEncryptedPathButton);
    }

    private void CopyLocatedEncryptedPath(object sender, RoutedEventArgs e)
    {
        try
        {
            DataPackage package = new();
            package.SetText(LocatedEncryptedFilePathText.Text);
            Clipboard.SetContent(package);
            ShowLocateEncryptedFileStatus("Encrypted path copied to the Windows clipboard.", true);
        }
        catch (Exception)
        {
            ShowLocateEncryptedFileStatus("VaultKind could not copy the path. You can still select it above.", false);
        }
    }

    private async void OpenLocatedEncryptedFolder(object sender, RoutedEventArgs e)
    {
        string? folderPath = Path.GetDirectoryName(LocatedEncryptedFilePathText.Text);
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            ShowLocateEncryptedFileStatus("The encrypted entry's containing folder is not currently available.", false);
            return;
        }

        try
        {
            Windows.Storage.StorageFolder folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
            bool opened = await Launcher.LaunchFolderAsync(folder);
            ShowLocateEncryptedFileStatus(opened
                ? "Opened the encrypted entry's containing folder."
                : "Windows could not open the encrypted entry's containing folder.", opened);
        }
        catch (Exception)
        {
            ShowLocateEncryptedFileStatus("Windows could not open the encrypted entry's containing folder.", false);
        }
    }

    private void ShowLocateEncryptedFileStatus(string message, bool success)
    {
        LocateEncryptedFileStatus.Text = message;
        LocateEncryptedFileStatus.Foreground = new SolidColorBrush(success
            ? Color.FromArgb(255, 185, 234, 199)
            : Color.FromArgb(255, 255, 172, 166));
        LocateEncryptedFileStatusBorder.Background = new SolidColorBrush(success
            ? Color.FromArgb(255, 30, 56, 41)
            : Color.FromArgb(255, 62, 39, 40));
        LocateEncryptedFileStatusBorder.BorderBrush = new SolidColorBrush(success
            ? Color.FromArgb(255, 38, 139, 69)
            : Color.FromArgb(255, 201, 83, 75));
        LocateEncryptedFileStatusBorder.Visibility = Visibility.Visible;
    }

    private void ShowDecryptFileName(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || !activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Visible;
        VaultManagementHome.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        VaultStatisticsPanel.Visibility = Visibility.Collapsed;
        VaultLocateEncryptedFilePanel.Visibility = Visibility.Collapsed;
        VaultDecryptFileNamePanel.Visibility = Visibility.Visible;
        ContextTitle.Text = "Decrypt File Name";
        ContextSubtitle.Text = "Identify a readable name from this vault's encrypted storage.";
        DecryptFileNameSubtitle.Text = $"Translate an encrypted .c9r file name through {activeVault.Name}.";
        DecryptFileNameResult.Visibility = Visibility.Collapsed;
        DecryptFileNameStatus.Visibility = Visibility.Collapsed;
        DecryptFileNameProgress.Visibility = Visibility.Collapsed;
        DecryptFileNameProgress.IsActive = false;
        FindEncryptedFileButton.IsEnabled = true;
        ChooseEncryptedFileButton.IsEnabled = true;
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(FindEncryptedFileButton);
    }

    private void HideDecryptFileName(object sender, RoutedEventArgs e)
    {
        VaultDecryptFileNamePanel.Visibility = Visibility.Collapsed;
        if (activeVault is not null && FindVaultButton(activeVault.Id) is Button button)
        {
            ShowVault(activeVault, button);
            FocusAfterNavigation(VaultDecryptFileNameButton);
        }
    }

    private async void ChooseEncryptedFileForDecryption(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        if (((App)Application.Current).MainWindow is not Window window)
        {
            ShowDecryptFileNameStatus("VaultKind could not open the Windows file picker.", false);
            return;
        }

        string? selectedPath;
        try
        {
            selectedPath = NativeFilePicker.PickFile(
                WinRT.Interop.WindowNative.GetWindowHandle(window),
                activeVault.Path,
                $"Choose an encrypted file from {activeVault.Name}",
                "Choose Encrypted File");
        }
        catch (Exception)
        {
            ShowDecryptFileNameStatus("Windows could not open this vault's encrypted storage picker.", false);
            return;
        }

        if (selectedPath is null)
        {
            return;
        }

        await DecryptSelectedFileAsync(selectedPath);
    }

    private async void FindEncryptedFileForDecryption(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        FindEncryptedFileButton.IsEnabled = false;
        ChooseEncryptedFileButton.IsEnabled = false;
        DecryptFileNameResult.Visibility = Visibility.Collapsed;
        DecryptFileNameProgress.IsActive = true;
        DecryptFileNameProgress.Visibility = Visibility.Visible;
        DecryptFileNameStatus.Text = "Searching this vault for a readable encrypted entry...";
        DecryptFileNameStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        DecryptFileNameStatus.Visibility = Visibility.Visible;

        FileNameDecryptResult? result = null;
        try
        {
            string dataPath = Path.Combine(activeVault.Path, "d");
            IEnumerable<string> candidates = Directory
                .EnumerateFiles(dataPath, "*.c9r", SearchOption.AllDirectories)
                .Where(path => !Path.GetFileName(path).Equals("dirid.c9r", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(100);

            foreach (string candidate in candidates)
            {
                FileNameDecryptResult attempt = await backend.DecryptFileNameAsync(activeVault.Id, candidate);
                if (attempt.Succeeded && attempt.Mapping is not null)
                {
                    result = attempt;
                    break;
                }

                if (attempt.Error is "vault_locked" or "vault_not_found" or "timeout")
                {
                    result = attempt;
                    break;
                }
            }
        }
        catch (IOException)
        {
            ShowDecryptFileNameStatus("VaultKind could not read this vault's encrypted storage right now.", false);
        }
        catch (UnauthorizedAccessException)
        {
            ShowDecryptFileNameStatus("Windows did not allow VaultKind to inspect this vault's encrypted storage.", false);
        }

        if (result is null)
        {
            FindEncryptedFileButton.IsEnabled = true;
            ChooseEncryptedFileButton.IsEnabled = true;
            DecryptFileNameProgress.IsActive = false;
            DecryptFileNameProgress.Visibility = Visibility.Collapsed;
            if (DecryptFileNameStatus.Visibility != Visibility.Visible ||
                DecryptFileNameStatus.Text.StartsWith("Searching", StringComparison.Ordinal))
            {
                ShowDecryptFileNameStatus("No readable encrypted file entries were found. Add a file to the unlocked drive, then try again.", false);
            }
            return;
        }

        await DisplayDecryptedFileNameResultAsync(result);
    }

    private async Task DecryptSelectedFileAsync(string filePath)
    {
        if (activeVault is null)
        {
            return;
        }

        FindEncryptedFileButton.IsEnabled = false;
        ChooseEncryptedFileButton.IsEnabled = false;
        DecryptFileNameResult.Visibility = Visibility.Collapsed;
        DecryptFileNameProgress.IsActive = true;
        DecryptFileNameProgress.Visibility = Visibility.Visible;
        DecryptFileNameStatus.Text = "Identifying the readable name locally...";
        DecryptFileNameStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        DecryptFileNameStatus.Visibility = Visibility.Visible;

        FileNameDecryptResult result = await backend.DecryptFileNameAsync(activeVault.Id, filePath);
        await DisplayDecryptedFileNameResultAsync(result);
    }

    private Task DisplayDecryptedFileNameResultAsync(FileNameDecryptResult result)
    {
        FindEncryptedFileButton.IsEnabled = true;
        ChooseEncryptedFileButton.IsEnabled = true;
        DecryptFileNameProgress.IsActive = false;
        DecryptFileNameProgress.Visibility = Visibility.Collapsed;

        if (!result.Succeeded || result.Mapping is null)
        {
            ShowDecryptFileNameStatus(result.Error switch
            {
                "foreign_file" => "Choose a .c9r file from this vault's encrypted storage folder.",
                "vault_internal_file" => "That is an internal vault file without a readable file name.",
                "vault_locked" => "This vault is no longer open. Unlock it before identifying a file name.",
                "vault_not_found" => "This vault is no longer connected to VaultKind.",
                "invalid_request" => "Choose an existing .c9r file from the encrypted vault folder.",
                "timeout" => "The local vault engine took too long to identify this file name.",
                _ => "VaultKind could not identify this encrypted file name. The file was not changed."
            }, false);
            return Task.CompletedTask;
        }

        EncryptedFileNameText.Text = result.Mapping.EncryptedName;
        ReadableFileNameText.Text = result.Mapping.CleartextName;
        DecryptFileNameResult.Visibility = Visibility.Visible;
        ShowDecryptFileNameStatus("Readable name identified locally. The encrypted file was not changed.", true);
        return Task.CompletedTask;
    }

    private void CopyReadableFileName(object sender, RoutedEventArgs e)
    {
        try
        {
            DataPackage package = new();
            package.SetText(ReadableFileNameText.Text);
            Clipboard.SetContent(package);
            ShowDecryptFileNameStatus("Readable name copied to the Windows clipboard.", true);
        }
        catch (Exception)
        {
            ShowDecryptFileNameStatus("VaultKind could not copy the readable name. You can still select it above.", false);
        }
    }

    private void ShowDecryptFileNameStatus(string message, bool success)
    {
        DecryptFileNameStatus.Text = message;
        DecryptFileNameStatus.Foreground = new SolidColorBrush(success
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 102, 93));
        DecryptFileNameStatus.Visibility = Visibility.Visible;
    }

    private async void RefreshVaultStatistics(object sender, RoutedEventArgs e) => await RefreshVaultStatisticsAsync();

    private async Task RefreshVaultStatisticsAsync()
    {
        if (activeVault is null)
        {
            return;
        }

        RefreshVaultStatisticsButton.IsEnabled = false;
        VaultStatisticsProgress.IsActive = true;
        VaultStatisticsProgress.Visibility = Visibility.Visible;
        VaultStatisticsStatus.Text = "Reading current activity from the local vault engine...";
        VaultStatisticsStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        VaultStatisticsStatus.Visibility = Visibility.Visible;

        VaultStatisticsResult result = await backend.GetStatisticsAsync(activeVault.Id);
        VaultStatisticsProgress.IsActive = false;
        VaultStatisticsProgress.Visibility = Visibility.Collapsed;
        RefreshVaultStatisticsButton.IsEnabled = true;

        if (!result.Succeeded || result.Statistics is null)
        {
            VaultStatisticsStatus.Text = result.Error switch
            {
                "vault_locked" => "Lock state changed. Unlock this vault to view its current statistics.",
                "vault_not_found" => "This vault is no longer connected to VaultKind.",
                "timeout" => "The vault engine took too long to return its activity counters.",
                _ => "VaultKind could not read statistics for this vault right now. No vault data was changed."
            };
            VaultStatisticsStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            return;
        }

        VaultStatistics statistics = result.Statistics;
        StatsCacheHitRate.Text = $"{Math.Clamp(statistics.CacheHitRate, 0d, 1d):P0}";
        StatsReadRate.Text = $"{FormatBytes(statistics.BytesPerSecondRead)}/s";
        StatsWriteRate.Text = $"{FormatBytes(statistics.BytesPerSecondWritten)}/s";
        StatsTotalAccesses.Text = statistics.TotalFilesAccessed.ToString("N0", CultureInfo.CurrentCulture);
        StatsTotalRead.Text = FormatBytes(statistics.TotalBytesRead);
        StatsTotalWritten.Text = FormatBytes(statistics.TotalBytesWritten);
        StatsTotalDecrypted.Text = FormatBytes(statistics.TotalBytesDecrypted);
        StatsTotalEncrypted.Text = FormatBytes(statistics.TotalBytesEncrypted);
        VaultStatisticsStatus.Text = $"Updated locally at {DateTime.Now:t}. No information was sent anywhere.";
        VaultStatisticsStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 73, 205, 112));
    }

    private static string FormatBytes(long value)
    {
        double size = Math.Max(0, value);
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        string format = unit == 0 ? "N0" : size >= 100 ? "N0" : size >= 10 ? "N1" : "N2";
        return $"{size.ToString(format, CultureInfo.CurrentCulture)} {units[unit]}";
    }

    private void BeginManagedVaultRename(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        ManagedVaultRenameInput.Text = activeVault.Name;
        ManagedVaultRenameStatus.Visibility = Visibility.Collapsed;
        ManagedVaultNameDisplay.Visibility = Visibility.Collapsed;
        ManagedVaultRenameEditor.Visibility = Visibility.Visible;
        FocusAfterNavigation(ManagedVaultRenameInput);
        ManagedVaultRenameInput.SelectAll();
    }

    private void CancelManagedVaultRename(object sender, RoutedEventArgs e)
    {
        ManagedVaultRenameEditor.Visibility = Visibility.Collapsed;
        ManagedVaultNameDisplay.Visibility = Visibility.Visible;
        ManagedVaultRenameStatus.Visibility = Visibility.Collapsed;
        FocusAfterNavigation(ManagedVaultRenameButton);
    }

    private void ManagedVaultRenameKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            SaveManagedVaultRename(sender, e);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            CancelManagedVaultRename(sender, e);
        }
    }

    private async void SaveManagedVaultRename(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        string newName = ManagedVaultRenameInput.Text.Trim();
        if (newName.Length == 0 || newName.Length > 50)
        {
            ShowManagedRenameStatus("Enter a name between 1 and 50 characters.", false);
            return;
        }

        string vaultId = activeVault.Id;
        string oldName = activeVault.Name;
        ManagedVaultRenameSave.IsEnabled = false;
        VaultCommandResult result = await backend.RenameAsync(vaultId, newName);
        ManagedVaultRenameSave.IsEnabled = true;
        if (!result.Succeeded)
        {
            ShowManagedRenameStatus(result.Error switch
            {
                "invalid_name" => "Enter a name between 1 and 50 characters.",
                "vault_not_found" => "This vault is no longer connected to VaultKind.",
                "invalid_state" => "Wait for the current vault operation to finish, then try again.",
                "timeout" => "Renaming took too long. The vault files were not changed.",
                _ => "VaultKind could not rename this vault. Its encrypted files were not changed."
            }, false);
            return;
        }

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        ApplySnapshot(snapshot);
        VaultSummary? renamed = snapshot.Vaults.FirstOrDefault(vault => vault.Id == vaultId);
        if (renamed is null)
        {
            ShowDashboard(sender, e);
            return;
        }

        activeVault = renamed;
        ShowVaultManagement(sender, e);
        ShowManagedRenameStatus($"Renamed from {oldName} to {renamed.Name}. The encrypted storage folder was not renamed.", true);
        LogActivity("Vault renamed", $"{oldName} is now shown as {renamed.Name}. Its encrypted storage folder was unchanged.", "manage");
    }

    private void ShowManagedRenameStatus(string message, bool success)
    {
        ManagedVaultRenameStatus.Text = message;
        ManagedVaultRenameStatus.Foreground = new SolidColorBrush(success
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 102, 93));
        ManagedVaultRenameStatus.Visibility = Visibility.Visible;
    }

    private void ReturnToManagedVault(object sender, RoutedEventArgs e)
    {
        if (activeVault is not null && FindVaultButton(activeVault.Id) is Button button)
        {
            ShowVault(activeVault, button);
        }
    }

    private void ShowVaultShareGuide(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        VaultManagementHome.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Visible;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        ShareVaultPathText.Text = activeVault.Path;
        ShareVaultStatus.Text = string.Empty;
        ShareVaultStatus.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Share Vault";
        ContextSubtitle.Text = "Share encrypted storage safely without exposing readable files.";
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(ShareOpenStorageFolderButton);
    }

    private async void OpenShareVaultFolder(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || string.IsNullOrWhiteSpace(activeVault.Path) || !Directory.Exists(activeVault.Path))
        {
            ShowShareVaultStatus("The encrypted storage folder is not currently available.", false);
            return;
        }

        try
        {
            Windows.Storage.StorageFolder folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(activeVault.Path);
            bool opened = await Launcher.LaunchFolderAsync(folder);
            ShowShareVaultStatus(opened
                ? "Opened the encrypted storage folder in Windows Explorer."
                : "Windows could not open the encrypted storage folder.", opened);
        }
        catch (Exception)
        {
            ShowShareVaultStatus("Windows could not open the encrypted storage folder.", false);
        }
    }

    private async void CopyShareVaultPath(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || string.IsNullOrWhiteSpace(activeVault.Path))
        {
            ShowShareVaultStatus("No encrypted storage folder is available to copy.", false);
            return;
        }

        string path = activeVault.Path;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                DataPackage package = new();
                package.SetText(path);
                Clipboard.SetContent(package);
                Clipboard.Flush();
                ShowShareVaultStatus("Encrypted storage folder path copied to the Windows clipboard.", true);
                return;
            }
            catch (Exception) when (attempt < 2)
            {
                // Windows can hold the clipboard briefly while another process is using it.
                // Retry inside the same click so the user never has to click Copy twice.
                await System.Threading.Tasks.Task.Delay(80);
            }
            catch (Exception)
            {
                break;
            }
        }

        ShowShareVaultStatus("VaultKind could not copy the folder path. You can still select it above.", false);
    }

    private void ShowShareVaultStatus(string message, bool success)
    {
        ShareVaultStatus.Text = message;
        ShareVaultStatus.Foreground = new SolidColorBrush(success
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 255, 102, 93));
        ShareVaultStatus.Visibility = Visibility.Visible;
    }

    private void HideVaultShareGuide(object sender, RoutedEventArgs e)
    {
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        VaultManagementHome.Visibility = Visibility.Visible;
        ContextTitle.Text = "Manage Vault";
        ContextSubtitle.Text = "Share, recover, inspect, or remove this vault without leaving the main window.";
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(ManagedShareGuideButton);
    }

    private void ShowManagedChangePassword(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        VaultManagementHome.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Visible;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        ChangePasswordForm.Visibility = Visibility.Visible;
        ChangePasswordSuccess.Visibility = Visibility.Collapsed;
        ChangeCurrentPassword.Password = string.Empty;
        ChangeNewPassword.Password = string.Empty;
        ChangeConfirmPassword.Password = string.Empty;
        ChangePasswordAcknowledge.IsChecked = false;
        ChangePasswordStatus.Text = string.Empty;
        ChangePasswordStatus.Visibility = Visibility.Collapsed;
        ChangePasswordProgress.IsActive = false;
        ChangePasswordProgress.Visibility = Visibility.Collapsed;
        ChangeCurrentPasswordLabel.Text = $"Current password for {activeVault.Name}";
        ContextTitle.Text = "Change Password";
        ContextSubtitle.Text = "Replace this vault's password without changing its encrypted files.";
        UpdateManagedChangePasswordForm();
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(ChangeCurrentPassword);
    }

    private void HideManagedChangePassword(object sender, RoutedEventArgs e)
    {
        ChangeCurrentPassword.Password = string.Empty;
        ChangeNewPassword.Password = string.Empty;
        ChangeConfirmPassword.Password = string.Empty;
        ChangePasswordAcknowledge.IsChecked = false;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        VaultManagementHome.Visibility = Visibility.Visible;
        ContextTitle.Text = "Manage Vault";
        ContextSubtitle.Text = "Share, recover, inspect, or remove this vault without leaving the main window.";
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(ManagedChangePasswordButton);
    }

    private void ChangePasswordFormChanged(object sender, RoutedEventArgs e) => UpdateManagedChangePasswordForm();

    private void UpdateManagedChangePasswordForm()
    {
        bool newPasswordValid = ChangeNewPassword.Password.Length >= 8;
        bool passwordsMatch = newPasswordValid && ChangeNewPassword.Password == ChangeConfirmPassword.Password;
        ChangePasswordMatchStatus.Text = ChangeNewPassword.Password.Length == 0 && ChangeConfirmPassword.Password.Length == 0
            ? "Use at least 8 characters."
            : !newPasswordValid
                ? "Use at least 8 characters."
                : passwordsMatch
                    ? "Passwords match"
                    : "The passwords do not match yet.";
        ChangePasswordMatchStatus.Foreground = new SolidColorBrush(passwordsMatch
            ? Color.FromArgb(255, 73, 205, 112)
            : Color.FromArgb(255, 174, 183, 190));
        ChangePasswordSubmitButton.IsEnabled = ChangeCurrentPassword.Password.Length > 0
            && passwordsMatch
            && ChangePasswordAcknowledge.IsChecked == true
            && !ChangePasswordProgress.IsActive;
    }

    private void ChangePasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ChangePasswordSubmitButton.IsEnabled)
        {
            e.Handled = true;
            SubmitManagedChangePassword(sender, new RoutedEventArgs());
        }
    }

    private async void SubmitManagedChangePassword(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || !ChangePasswordSubmitButton.IsEnabled)
        {
            return;
        }

        string vaultId = activeVault.Id;
        string vaultName = activeVault.Name;
        string currentPassword = ChangeCurrentPassword.Password;
        string newPassword = ChangeNewPassword.Password;
        ChangePasswordSubmitButton.IsEnabled = false;
        ChangePasswordProgress.IsActive = true;
        ChangePasswordProgress.Visibility = Visibility.Visible;
        ChangePasswordStatus.Text = "Changing the password and preserving a protected master-key backup...";
        ChangePasswordStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        ChangePasswordStatus.Visibility = Visibility.Visible;

        VaultCommandResult result = await backend.ChangePasswordAsync(vaultId, currentPassword, newPassword);
        currentPassword = string.Empty;
        newPassword = string.Empty;
        ChangePasswordProgress.IsActive = false;
        ChangePasswordProgress.Visibility = Visibility.Collapsed;

        if (!result.Succeeded)
        {
            ChangePasswordStatus.Text = result.Error switch
            {
                "wrong_password" => "The current password is incorrect. Nothing was changed.",
                "vault_unlocked" => "Lock this vault before changing its password.",
                "vault_not_found" => "This vault is no longer connected to VaultKind.",
                "password_change_failed" => "VaultKind could not safely change this password. Nothing was intentionally changed.",
                "timeout" => "The password change took too long. Verify the vault state before trying again.",
                _ => "VaultKind could not change this password. Nothing was changed."
            };
            ChangePasswordStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            ChangeCurrentPassword.Password = string.Empty;
            UpdateManagedChangePasswordForm();
            FocusAfterNavigation(ChangeCurrentPassword);
            return;
        }

        ChangeCurrentPassword.Password = string.Empty;
        ChangeNewPassword.Password = string.Empty;
        ChangeConfirmPassword.Password = string.Empty;
        ChangePasswordAcknowledge.IsChecked = false;
        ChangePasswordForm.Visibility = Visibility.Collapsed;
        ChangePasswordSuccess.Visibility = Visibility.Visible;
        FocusAfterNavigation(ChangePasswordDoneButton);
        LogActivity("Vault password changed", $"{vaultName} now uses a new password.", "security");
    }

    private void ShowManagedRecoveryKey(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        VaultManagementHome.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Visible;
        RecoveryKeyPasswordForm.Visibility = Visibility.Visible;
        RecoveryKeyDisplayResult.Visibility = Visibility.Collapsed;
        RecoveryKeyPassword.Password = string.Empty;
        ManagedRecoveryKeyText.Text = string.Empty;
        ManagedRecoveryKeyCopyStatus.Text = string.Empty;
        ManagedRecoveryKeyCopyStatus.Visibility = Visibility.Collapsed;
        RecoveryKeyDisplayStatus.Text = string.Empty;
        RecoveryKeyDisplayStatus.Visibility = Visibility.Collapsed;
        RecoveryKeyDisplayProgress.IsActive = false;
        RecoveryKeyDisplayProgress.Visibility = Visibility.Collapsed;
        RecoveryKeyPasswordLabel.Text = $"Password for {activeVault.Name}";
        RecoveryKeyDisplaySubmitButton.IsEnabled = false;
        ContextTitle.Text = "Show Recovery Key";
        ContextSubtitle.Text = "View and securely store the emergency recovery key for this vault.";
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(RecoveryKeyPassword);
    }

    private void HideManagedRecoveryKey(object sender, RoutedEventArgs e)
    {
        RecoveryKeyPassword.Password = string.Empty;
        ManagedRecoveryKeyText.Text = string.Empty;
        VaultRecoveryKeyDisplay.Visibility = Visibility.Collapsed;
        VaultChangePassword.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultManagementHome.Visibility = Visibility.Visible;
        ContextTitle.Text = "Manage Vault";
        ContextSubtitle.Text = "Share, recover, inspect, or remove this vault without leaving the main window.";
        VaultManagementView.ChangeView(null, 0, null, true);
        FocusAfterNavigation(ManagedShowRecoveryKeyButton);
    }

    private void RecoveryKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        RecoveryKeyDisplaySubmitButton.IsEnabled = RecoveryKeyPassword.Password.Length > 0 && !RecoveryKeyDisplayProgress.IsActive;
    }

    private void RecoveryKeyPasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && RecoveryKeyDisplaySubmitButton.IsEnabled)
        {
            e.Handled = true;
            SubmitManagedRecoveryKey(sender, new RoutedEventArgs());
        }
    }

    private async void SubmitManagedRecoveryKey(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || !RecoveryKeyDisplaySubmitButton.IsEnabled)
        {
            return;
        }

        string vaultId = activeVault.Id;
        string vaultName = activeVault.Name;
        string password = RecoveryKeyPassword.Password;
        RecoveryKeyPassword.Password = string.Empty;
        RecoveryKeyDisplaySubmitButton.IsEnabled = false;
        RecoveryKeyDisplayProgress.IsActive = true;
        RecoveryKeyDisplayProgress.Visibility = Visibility.Visible;
        RecoveryKeyDisplayStatus.Text = "Confirming the password and deriving the recovery key locally...";
        RecoveryKeyDisplayStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        RecoveryKeyDisplayStatus.Visibility = Visibility.Visible;

        VaultCommandResult result = await backend.ShowRecoveryKeyAsync(vaultId, password);
        password = string.Empty;
        RecoveryKeyDisplayProgress.IsActive = false;
        RecoveryKeyDisplayProgress.Visibility = Visibility.Collapsed;

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.RecoveryKey))
        {
            RecoveryKeyDisplayStatus.Text = result.Error switch
            {
                "wrong_password" => "That password is incorrect. The recovery key was not shown.",
                "vault_not_found" => "This vault is no longer connected to VaultKind.",
                "recovery_key_failed" => "VaultKind could not derive the recovery key. No vault data was changed.",
                "unknown_operation" => "The local vault engine is from an older build. Restart VaultKind, then try again.",
                "engine_unavailable" => "The local vault engine is unavailable. Restart VaultKind, then try again.",
                "timeout" => "The request took too long. Verify the vault state before trying again.",
                _ => "VaultKind could not show the recovery key. No vault data was changed."
            };
            RecoveryKeyDisplayStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            RecoveryKeyDisplaySubmitButton.IsEnabled = false;
            FocusAfterNavigation(RecoveryKeyPassword);
            return;
        }

        ManagedRecoveryKeyText.Text = result.RecoveryKey;
        RecoveryKeyPasswordForm.Visibility = Visibility.Collapsed;
        RecoveryKeyDisplayResult.Visibility = Visibility.Visible;
        FocusAfterNavigation(ManagedRecoveryKeyText);
        LogActivity("Recovery key viewed", $"The recovery key for {vaultName} was displayed locally.", "recovery");
    }

    private void CopyManagedRecoveryKey(object sender, RoutedEventArgs e)
    {
        ManagedRecoveryKeyCopyStatus.Text = string.Empty;
        ManagedRecoveryKeyCopyStatus.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(ManagedRecoveryKeyText.Text))
        {
            return;
        }

        try
        {
            CopySensitiveTextToClipboard(ManagedRecoveryKeyText.Text, ManagedRecoveryKeyCopyStatus);
            ManagedRecoveryKeyCopyStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 211, 111));
            ManagedRecoveryKeyCopyStatus.Text = "Recovery key copied. Clipboard clears in 60 seconds.";
        }
        catch (Exception)
        {
            ManagedRecoveryKeyCopyStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 91, 82));
            ManagedRecoveryKeyCopyStatus.Text = "Clipboard unavailable. Select the key and press Ctrl+C.";
        }
        ManagedRecoveryKeyCopyStatus.Visibility = Visibility.Visible;
    }

    private void CopySensitiveTextToClipboard(string text, TextBlock status)
    {
        DataPackage package = new();
        package.SetText(text);
        ClipboardContentOptions options = new()
        {
            IsAllowedInHistory = false,
            IsRoamable = false
        };
        if (!Clipboard.SetContentWithOptions(package, options))
        {
            throw new InvalidOperationException("Windows did not accept the sensitive clipboard content.");
        }

        sensitiveClipboardClearCancellation?.Cancel();
        sensitiveClipboardClearCancellation?.Dispose();
        sensitiveClipboardClearCancellation = new CancellationTokenSource();
        _ = ClearSensitiveClipboardAfterDelayAsync(text, status, sensitiveClipboardClearCancellation);
    }

    private async Task ClearSensitiveClipboardAfterDelayAsync(string expectedText, TextBlock status, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cancellation.Token);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    DataPackageView current = Clipboard.GetContent();
                    if (!current.Contains(StandardDataFormats.Text))
                    {
                        return;
                    }

                    string currentText = await current.GetTextAsync();
                    if (!string.Equals(currentText, expectedText, StringComparison.Ordinal))
                    {
                        return;
                    }

                    Clipboard.Clear();
                    status.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
                    status.Text = "Recovery key removed from the clipboard.";
                    status.Visibility = Visibility.Visible;
                    return;
                }
                catch (Exception) when (attempt < 4)
                {
                    await Task.Delay(100, cancellation.Token);
                }
            }

            status.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 46));
            status.Text = "Windows kept the clipboard busy. Copy something else to replace the recovery key.";
            status.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            // A newer sensitive copy owns the clipboard timeout.
        }
        catch (Exception)
        {
            // Clipboard access can be temporarily blocked by another process. Never interrupt vault work.
        }
        finally
        {
            if (ReferenceEquals(sensitiveClipboardClearCancellation, cancellation))
            {
                sensitiveClipboardClearCancellation.Dispose();
                sensitiveClipboardClearCancellation = null;
            }
        }
    }

    private void ShowManagedRecovery(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        recoveryOpenedFromVaultManagement = true;
        recoveryTargetVaultId = activeVault.Id;
        ShowRecoveryReset(sender, e);
    }

    private void ShowManagedRemoval(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (FindVaultButton(activeVault.Id) is Button button)
        {
            ShowVault(activeVault, button);
            ShowRemoveVaultConfirmation(sender, e);
        }
    }

    private void ShowUnlock(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        VaultView.Visibility = Visibility.Collapsed;
        VaultManagementView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        ConnectVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Visible;
        RecoveryHubView.Visibility = Visibility.Collapsed;
        RecoveryResetView.Visibility = Visibility.Collapsed;
        ActivityView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        LearningView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Unlock Vault";
        ContextSubtitle.Text = "Enter your password to securely open this vault.";
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(SettingsButton, "Preferences");
        SetDestinationUnselected(LearningButton, "Learning Center");
        UnlockVaultName.Text = activeVault.Name;
        UnlockVaultPath.Text = activeVault.Path;
        UnlockPassword.Password = string.Empty;
        UnlockStatus.Text = string.Empty;
        UnlockStatus.Visibility = Visibility.Collapsed;
        UnlockSubmitButton.IsEnabled = true;
        FocusAfterNavigation(UnlockPassword);
    }

    private void FocusAfterNavigation(Control control)
    {
        // A button invoked with the keyboard can reclaim focus as its click event
        // completes. Waiting briefly ensures the newly visible form control wins.
        navigationFocusTimer?.Stop();
        navigationFocusTimer = DispatcherQueue.CreateTimer();
        navigationFocusTimer.Interval = TimeSpan.FromMilliseconds(100);
        navigationFocusTimer.IsRepeating = false;
        navigationFocusTimer.Tick += (_, _) =>
        {
            navigationFocusTimer?.Stop();
            control.UpdateLayout();
            control.StartBringIntoView();
            control.Focus(FocusState.Keyboard);
            navigationFocusTimer = null;
        };
        navigationFocusTimer.Start();
    }

    private void CancelUnlock(object sender, RoutedEventArgs e)
    {
        UnlockPassword.Password = string.Empty;
        if (activeVault is not null && FindVaultButton(activeVault.Id) is Button button)
        {
            ShowVault(activeVault, button);
        }
    }

    private async void SubmitUnlock(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || string.IsNullOrEmpty(UnlockPassword.Password))
        {
            ShowUnlockError("Enter your vault password to continue.");
            return;
        }

        string password = UnlockPassword.Password;
        UnlockPassword.Password = string.Empty;
        UnlockSubmitButton.IsEnabled = false;
        UnlockProgress.IsActive = true;
        UnlockStatus.Text = "Opening your vault securely...";
        UnlockStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        UnlockStatus.Visibility = Visibility.Visible;

        VaultCommandResult result = await backend.UnlockAsync(activeVault.Id, password);
        password = string.Empty;
        UnlockProgress.IsActive = false;
        UnlockSubmitButton.IsEnabled = true;

        if (!result.Succeeded)
        {
            ShowUnlockError(FriendlyUnlockError(result.Error));
            FocusAfterNavigation(UnlockPassword);
            return;
        }

        string unlockedVaultName = activeVault.Name;
        signatureSounds.Play(SignatureSound.VaultOpen);
        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        ApplySnapshot(snapshot);
        LogActivity("Vault unlocked", $"{unlockedVaultName}'s readable Windows drive is now open.", "unlock");
        VaultSummary? updated = snapshot.Vaults.FirstOrDefault(vault => vault.Id == activeVault.Id);
        if (updated is not null && FindVaultButton(updated.Id) is Button button)
        {
            ShowVault(updated, button);
        }
    }

    private void UnlockPasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && UnlockSubmitButton.IsEnabled)
        {
            e.Handled = true;
            SubmitUnlock(UnlockSubmitButton, e);
        }
    }

    private void ShowUnlockError(string message)
    {
        UnlockStatus.Text = message;
        UnlockStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
        UnlockStatus.Visibility = Visibility.Visible;
    }

    private Button? FindVaultButton(string vaultId) => vaultButtons.FirstOrDefault(button => vaultId.Equals(button.Tag as string, StringComparison.Ordinal));

    private static string FriendlyUnlockError(string? error) => error switch
    {
        "wrong_password" => "That password did not unlock this vault. Check it and try again.",
        "mount_failed" => "The password was accepted, but Windows could not open the readable drive.",
		"vault_key_invalid" => "VaultKind could not verify this vault's configuration. Nothing was changed.",
        "already_unlocked" => "This vault is already open.",
        "timeout" => "Opening the vault took too long. Nothing was changed; please try again.",
        "engine_unavailable" => "The local VaultKind engine is unavailable. Please return to the Dashboard and try again.",
        _ => "VaultKind could not open this vault. Nothing was changed."
    };

    private async void OpenDrive(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        SetVaultActionBusy(true, "Opening the readable drive...");
        VaultCommandResult result = await backend.RevealAsync(activeVault.Id);
        SetVaultActionBusy(false, result.Succeeded ? string.Empty : FriendlyVaultActionError(result.Error, false));
    }

    private async void LockVault(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        string vaultId = activeVault.Id;
        string vaultName = activeVault.Name;
        SetVaultActionBusy(true, "Closing the readable drive securely...");
        VaultCommandResult result = await backend.LockAsync(vaultId);
        if (!result.Succeeded)
        {
            if (SignatureSoundPolicy.ShouldWarnForLockFailure(result.Error))
            {
                signatureSounds.Play(SignatureSound.Warning, SoundEmphasis.Strong);
            }

            SetVaultActionBusy(false, FriendlyVaultActionError(result.Error, true));
            return;
        }

        signatureSounds.Play(SignatureSound.VaultLocked);
        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        ApplySnapshot(snapshot);
        LogActivity("Vault locked", $"{vaultName}'s readable Windows drive was closed securely.", "lock");
        VaultSummary? updated = snapshot.Vaults.FirstOrDefault(vault => vault.Id == vaultId);
        if (updated is not null && FindVaultButton(updated.Id) is Button button)
        {
            ShowVault(updated, button);
        }
    }

    private void ShowRemoveVaultConfirmation(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RemoveVaultLocationText.Text = $"{activeVault.Name}  •  {activeVault.Path}";
        RemoveVaultPromptText.Text = $"Type {activeVault.Name} to confirm.";
        RemoveVaultNameInput.Text = string.Empty;
        ConfirmRemoveVaultButton.IsEnabled = false;
        RemoveVaultConfirmation.Visibility = Visibility.Visible;
        RemoveVaultButton.Visibility = Visibility.Collapsed;
        FocusAfterNavigation(RemoveVaultNameInput);
    }

    private void RemoveVaultNameChanged(object sender, TextChangedEventArgs e)
    {
        ConfirmRemoveVaultButton.IsEnabled = activeVault is not null
            && RemoveVaultNameInput.Text.Equals(activeVault.Name, StringComparison.Ordinal);
    }

    private void CancelRemoveVault(object sender, RoutedEventArgs e)
    {
        RemoveVaultConfirmation.Visibility = Visibility.Collapsed;
        RemoveVaultNameInput.Text = string.Empty;
        RemoveVaultButton.Visibility = Visibility.Visible;
        RemoveVaultButton.Focus(FocusState.Programmatic);
    }

    private async void ConfirmRemoveVault(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || !RemoveVaultNameInput.Text.Equals(activeVault.Name, StringComparison.Ordinal))
        {
            return;
        }

        string vaultId = activeVault.Id;
        string vaultName = activeVault.Name;
        ConfirmRemoveVaultButton.IsEnabled = false;
        VaultActionProgress.IsActive = true;
        VaultActionProgress.Visibility = Visibility.Visible;
        VaultActionStatus.Text = "Removing this vault from the VaultKind list...";
        VaultActionStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190));
        VaultActionStatus.Visibility = Visibility.Visible;

        VaultCommandResult result = await backend.RemoveAsync(vaultId);
        if (!result.Succeeded)
        {
            ConfirmRemoveVaultButton.IsEnabled = RemoveVaultNameInput.Text.Equals(activeVault?.Name, StringComparison.Ordinal);
            VaultActionProgress.IsActive = false;
            VaultActionProgress.Visibility = Visibility.Collapsed;
            VaultActionStatus.Text = result.Error switch
            {
                "vault_unlocked" => "Lock this vault before removing it from VaultKind.",
                "vault_not_found" => "This vault is no longer in the VaultKind list.",
                "timeout" => "Removing the vault took too long. Nothing was deleted.",
                _ => "VaultKind could not remove this vault from the list. Nothing was deleted."
            };
            VaultActionStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            return;
        }

        activeVault = null;
        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        ApplySnapshot(snapshot);
        LogActivity("Vault removed", $"{vaultName} was removed from the VaultKind list. Its encrypted files were not deleted.", "remove");
        ShowDashboard(sender, e);
    }

    private void SetVaultActionBusy(bool busy, string message)
    {
        OpenDriveButton.IsEnabled = !busy;
        LockVaultButton.IsEnabled = !busy;
        VaultActionProgress.IsActive = busy;
        VaultActionProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        VaultActionStatus.Text = message;
        VaultActionStatus.Foreground = new SolidColorBrush(message.Length > 0 && !busy
            ? Color.FromArgb(255, 255, 102, 93)
            : Color.FromArgb(255, 174, 183, 190));
        VaultActionStatus.Visibility = message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string FriendlyVaultActionError(string? error, bool locking) => error switch
    {
        "vault_in_use" => "VaultKind could not lock this vault because a file or app is still using its readable drive. Close it and try again.",
        "already_locked" or "vault_locked" => "This vault is already locked.",
        "mount_unavailable" or "reveal_failed" => "Windows could not open the readable drive. The vault remains safely unlocked.",
        "timeout" => locking ? "Locking took too long. The vault remains unlocked." : "Opening the drive took too long. Please try again.",
        "engine_unavailable" => "The local VaultKind engine is unavailable. Return to the Dashboard and try again.",
        _ => locking ? "VaultKind could not lock this vault. It remains unlocked." : "VaultKind could not open the readable drive."
    };

    private void RunDoctorPreview(object sender, RoutedEventArgs e) => _ = RunDoctorChecksAsync();

    private async Task RunDoctorChecksAsync()
    {
        if (doctorRunInProgress)
        {
            return;
        }

        doctorRunInProgress = true;
        DoctorProgressRing.Visibility = Visibility.Visible;
        DoctorProgressRing.IsActive = true;
        DoctorRunAgainButton.IsEnabled = false;
        DoctorRunAgainButton.Content = "Running…";
        DoctorCompletionCard.Visibility = Visibility.Collapsed;
        DoctorSummary.Text = "Vault Doctor is running local, read-only checks...";
        DoctorChecksPanel.Children.Clear();
        DoctorSaveReportButton.IsEnabled = false;
        DoctorSaveReportStatus.Visibility = Visibility.Collapsed;

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        if (snapshot.ConnectionState == BackendConnectionState.Ready)
        {
            ApplySnapshot(snapshot);
        }

        var checks = new List<DoctorCheck>();
        checks.Add(snapshot.ConnectionState == BackendConnectionState.Ready
            ? new("The local VaultKind engine is responding", "healthy")
            : new("The local VaultKind engine is not available", "attention", "VK-0002"));
        checks.Add(OperatingSystem.IsWindows()
            ? new("Windows desktop compatibility is available", "healthy")
            : new("VaultKind is not running on its supported Windows desktop platform", "attention", "VK-0002"));

        IReadOnlyList<VaultSummary> targets = snapshot.Vaults;
        if (!string.IsNullOrWhiteSpace(doctorFocusVaultId))
        {
            VaultSummary? focused = snapshot.Vaults.FirstOrDefault(vault => vault.Id == doctorFocusVaultId);
            targets = focused is null ? [] : [focused];
            DoctorScope.Text = focused is null ? "Selected vault is no longer connected" : $"Focused report for {focused.Name}";
            DoctorChecksTitle.Text = focused is null ? "Selected vault checks" : $"Automatic checks for {focused.Name}";
        }
        else
        {
            DoctorScope.Text = snapshot.Vaults.Count == 0 ? "VaultKind and Windows" : $"VaultKind and {snapshot.Vaults.Count} configured vault(s)";
            DoctorChecksTitle.Text = "Automatic checks";
        }

        if (targets.Count == 0)
        {
            checks.Add(new(doctorFocusVaultId is null ? "No vaults are currently configured" : "The selected vault is no longer connected", "information"));
        }

        foreach (VaultSummary vault in targets)
        {
            bool pathExists = Directory.Exists(vault.Path);
            checks.Add(pathExists
                ? new($"{vault.Name}: encrypted storage location is present", "healthy")
                : new($"{vault.Name}: encrypted storage location could not be found", "attention", "VK-1002"));

            checks.Add(CheckEngineVaultState(vault.Name, vault.State));

            if (pathExists)
            {
                string configurationPath = Path.Combine(vault.Path, "vault.cryptomator");
                checks.Add(CheckVaultConfiguration(vault.Name, configurationPath));

                string encryptedDataPath = Path.Combine(vault.Path, "d");
                checks.Add(CheckEncryptedDataDirectory(vault.Name, encryptedDataPath));

                try
                {
                    string? root = Path.GetPathRoot(vault.Path);
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        var drive = new DriveInfo(root);
                        if (drive.IsReady)
                        {
                            double freeGigabytes = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                            checks.Add(freeGigabytes < 1d
                                ? new($"{vault.Name}: only {freeGigabytes:N1} GB is available on {root}", "attention", "VK-2002")
                                : new($"{vault.Name}: {freeGigabytes:N1} GB available on {root}", "information"));
                        }
                        else
                        {
                            checks.Add(new($"{vault.Name}: Windows reports the storage device is not ready", "attention", "VK-1002"));
                        }
                    }
                }
                catch (IOException)
                {
                    checks.Add(new($"{vault.Name}: Windows could not read storage capacity", "information"));
                }
                catch (UnauthorizedAccessException)
                {
                    checks.Add(new($"{vault.Name}: storage capacity is not available to this account", "information"));
                }
            }
        }

        latestDoctorChecks = checks.ToList();
        DateTimeOffset completedAt = DateTimeOffset.Now;
        latestDoctorRunAt = completedAt.LocalDateTime;
        int healthy = checks.Count(check => check.Kind == "healthy");
        int attention = checks.Count(check => check.Kind == "attention");
        int information = checks.Count(check => check.Kind == "information");
        if (checks.Any(check => DoctorFindingPolicy.IsCritical(check.Kind, check.AssistantCaseId)))
        {
            signatureSounds.Play(SignatureSound.Warning, SoundEmphasis.Strong);
        }

        DoctorHealthyCount.Text = healthy.ToString();
        DoctorAttentionCount.Text = attention.ToString();
        DoctorInformationCount.Text = information.ToString();
        DoctorSummary.Text = attention == 0
            ? $"Vault Doctor didn't find any issues in the checks it completed at {DateTime.Now:h:mm tt}."
            : $"Vault Doctor found {attention} item{(attention == 1 ? string.Empty : "s")} worth reviewing.";
        bool needsAttention = attention > 0;
        DoctorCompletionTitle.Text = needsAttention
            ? $"Check complete — {attention} item{(attention == 1 ? string.Empty : "s")} worth reviewing"
            : "Check complete — no issues found";
        DoctorCompletionDescription.Text = $"Completed locally at {completedAt.LocalDateTime:h:mm tt}. Read-only checks made no changes to your vault data.";
        DoctorCompletionIcon.Glyph = needsAttention ? "\uE7BA" : "\uE73E";
        DoctorCompletionIcon.Foreground = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 255, 193, 46)
            : Color.FromArgb(255, 73, 205, 112));
        DoctorCompletionCard.BorderBrush = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 177, 139, 32)
            : Color.FromArgb(255, 62, 150, 91));
        DoctorCompletionCard.Visibility = Visibility.Visible;
        AutomationProperties.SetName(DoctorCompletionCard, DoctorCompletionTitle.Text + ". " + DoctorCompletionDescription.Text);
        UpdateDashboardDoctorStatus(healthy, attention, information, completedAt);
        DoctorSummaryStore.Save(new DoctorRunSummary(healthy, attention, information, completedAt));
        LogActivity(
            attention == 0 ? "Vault Doctor found no issues" : "Vault Doctor found items worth reviewing",
            attention == 0
                ? $"Completed locally with {healthy} healthy and {information} information result{(information == 1 ? string.Empty : "s")}."
                : $"Completed locally with {attention} item{(attention == 1 ? string.Empty : "s")} worth reviewing, {healthy} healthy, and {information} information result{(information == 1 ? string.Empty : "s")}.",
            "doctor");
        DoctorCheck? firstFinding = checks.FirstOrDefault(check => check.Kind == "attention");
        doctorAssistantCaseId = firstFinding?.AssistantCaseId;
        doctorAssistantEvidence = firstFinding?.Message ?? string.Empty;
        DoctorAssistantButton.Content = attention == 0 ? "Open Assistant" : "Review Finding in Assistant";
        DoctorSaveReportButton.IsEnabled = true;

        AddDoctorCheckGroup(checks, "attention", "Needs attention", "!", expandWhenPopulated: true);
        AddDoctorCheckGroup(checks, "healthy", "Healthy", "✓");
        AddDoctorCheckGroup(checks, "information", "Information", "ⓘ");
        DoctorProgressRing.IsActive = false;
        DoctorProgressRing.Visibility = Visibility.Collapsed;
        DoctorRunAgainButton.Content = "Run Doctor Again";
        DoctorRunAgainButton.IsEnabled = true;
        doctorRunInProgress = false;
        QueueTextScaleRefresh();
    }

    private void UpdateDashboardDoctorStatus(int healthy, int attention, int information, DateTimeOffset completedAt)
    {
        bool needsAttention = attention > 0;
        DoctorNavStatusBadge.Visibility = Visibility.Visible;
        DoctorNavStatusBadge.Background = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 62, 55, 31)
            : Color.FromArgb(255, 38, 63, 49));
        DoctorNavStatusBadge.BorderBrush = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 177, 139, 32)
            : Color.FromArgb(255, 63, 150, 91));
        DoctorNavStatusIcon.Glyph = needsAttention ? "\uE7BA" : "\uE73E";
        DoctorNavStatusIcon.Foreground = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 255, 193, 46)
            : Color.FromArgb(255, 81, 212, 120));
        string navigationStatus = needsAttention
            ? $"Latest Vault Doctor check found {attention} item{(attention == 1 ? string.Empty : "s")} worth reviewing"
            : "Latest Vault Doctor check found no issues";
        AutomationProperties.SetName(DoctorNavStatusBadge, navigationStatus);
        ToolTipService.SetToolTip(DoctorNavStatusBadge, navigationStatus);
        DashboardDoctorTitle.Text = needsAttention
            ? $"Vault Doctor found {attention} item{(attention == 1 ? string.Empty : "s")} worth reviewing"
            : "Vault Doctor found no issues";
        DashboardDoctorDescription.Text = $"{healthy} healthy  \u2022  {information} information  \u2022  Completed at {completedAt.LocalDateTime:h:mm tt}";
        DashboardDoctorButton.Content = "Review Results";
        AutomationProperties.SetName(DashboardDoctorButton, "Review the latest Vault Doctor results");
        DashboardDoctorIcon.Glyph = needsAttention ? "\uE7BA" : "\uE73E";
        DashboardDoctorIcon.Foreground = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 255, 193, 46)
            : Color.FromArgb(255, 73, 205, 112));
        DashboardDoctorCard.Background = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 62, 55, 31)
            : Color.FromArgb(255, 41, 63, 52));
        DashboardDoctorCard.BorderBrush = new SolidColorBrush(needsAttention
            ? Color.FromArgb(255, 177, 139, 32)
            : Color.FromArgb(255, 62, 150, 91));
    }

    private static DoctorCheck CheckVaultConfiguration(string vaultName, string configurationPath)
    {
        if (!File.Exists(configurationPath))
        {
            return new($"{vaultName}: vault.cryptomator is missing from the encrypted storage folder", "attention", "VK-1003");
        }

        try
        {
            using FileStream configuration = new(
                configurationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return configuration.ReadByte() == -1
                ? new($"{vaultName}: vault.cryptomator is empty", "attention", "VK-1003")
                : new($"{vaultName}: vault.cryptomator is present, non-empty, and readable", "healthy");
        }
        catch (UnauthorizedAccessException)
        {
            return new($"{vaultName}: vault.cryptomator cannot be read by this Windows account", "attention", "VK-1003");
        }
        catch (IOException)
        {
            return new($"{vaultName}: vault.cryptomator is present but could not be read", "attention", "VK-1003");
        }
    }

    private static DoctorCheck CheckEngineVaultState(string vaultName, string state) => state.ToLowerInvariant() switch
    {
        "locked" or "unlocked" => new($"{vaultName}: local engine reports {FriendlyVaultState(state).ToLowerInvariant()} and the vault configuration is available", "healthy"),
        "processing" => new($"{vaultName}: local engine is currently working; run Vault Doctor again when it finishes", "information"),
        "missing" or "all_missing" => new($"{vaultName}: local engine reports the storage location is unavailable", "attention", "VK-1002"),
        "vault_config_missing" => new($"{vaultName}: local engine reports the vault configuration is missing", "attention", "VK-1003"),
        "needs_migration" => new($"{vaultName}: local engine reports the vault configuration requires an update", "attention", "VK-1003"),
        "error" => new($"{vaultName}: local engine could not load the vault configuration", "attention", "VK-1003"),
        _ => new($"{vaultName}: local engine reported an unsupported vault state", "attention")
    };

    private static DoctorCheck CheckEncryptedDataDirectory(string vaultName, string encryptedDataPath)
    {
        if (!Directory.Exists(encryptedDataPath))
        {
            return new($"{vaultName}: encrypted data directory is missing", "attention", "VK-3002");
        }

        try
        {
            using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(encryptedDataPath).GetEnumerator();
            _ = entries.MoveNext();
            return new($"{vaultName}: encrypted data directory is present and readable", "healthy");
        }
        catch (UnauthorizedAccessException)
        {
            return new($"{vaultName}: encrypted data directory cannot be read by this Windows account", "attention", "VK-3002");
        }
        catch (IOException)
        {
            return new($"{vaultName}: encrypted data directory is present but could not be read", "attention", "VK-3002");
        }
    }

    private async void SaveDoctorReport(object sender, RoutedEventArgs e)
    {
        if (latestDoctorRunAt is null
            || latestDoctorChecks.Count == 0
            || ((App)Application.Current).MainWindow is not Window window)
        {
            return;
        }

        try
        {
            FileSavePicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"VaultKind-Doctor-Report-{latestDoctorRunAt.Value:yyyyMMdd-HHmm}"
            };
            picker.FileTypeChoices.Add("Text document", new List<string> { ".txt" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, BuildDoctorReport());
            DoctorSaveReportStatus.Text = "Report saved.";
            DoctorSaveReportStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 81, 212, 120));
            DoctorSaveReportStatus.Visibility = Visibility.Visible;
            LogActivity("Doctor report saved", "A local Vault Doctor report was saved without private vault data.", "doctor");
        }
        catch (Exception)
        {
            DoctorSaveReportStatus.Text = "Could not save report.";
            DoctorSaveReportStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 102, 93));
            DoctorSaveReportStatus.Visibility = Visibility.Visible;
        }
    }

    private string BuildDoctorReport()
    {
        int healthy = latestDoctorChecks.Count(check => check.Kind == "healthy");
        int attention = latestDoctorChecks.Count(check => check.Kind == "attention");
        int information = latestDoctorChecks.Count(check => check.Kind == "information");
        var report = new System.Text.StringBuilder();
        report.AppendLine("VAULTKIND VAULT DOCTOR REPORT");
        report.AppendLine("=============================");
        report.AppendLine($"Completed locally: {latestDoctorRunAt:yyyy-MM-dd h:mm tt}");
        report.AppendLine($"Scope: {DoctorScope.Text}");
        report.AppendLine($"Summary: {healthy} healthy, {attention} needs attention, {information} information");
        report.AppendLine();

        foreach ((string kind, string heading) in new[]
        {
            ("attention", "NEEDS ATTENTION"),
            ("healthy", "HEALTHY"),
            ("information", "INFORMATION")
        })
        {
            List<DoctorCheck> group = latestDoctorChecks.Where(check => check.Kind == kind).ToList();
            if (group.Count == 0)
            {
                continue;
            }

            report.AppendLine(heading);
            foreach (DoctorCheck check in group)
            {
                report.AppendLine($"- {check.Message}");
            }
            report.AppendLine();
        }

        report.AppendLine("PRIVACY");
        report.AppendLine("This report was generated on this computer. It does not contain passwords, recovery keys, file contents, or readable file names.");
        return report.ToString();
    }

    private void OpenDoctorAssistant(object sender, RoutedEventArgs e)
    {
        ShowLearningCenter(sender, e);
        ShowAssistant(sender, e);
        if (!string.IsNullOrWhiteSpace(doctorAssistantCaseId))
        {
            AssistantSearch.Text = string.Empty;
            ShowAssistantCase(doctorAssistantCaseId, 100, $"Vault Doctor observed locally: {doctorAssistantEvidence}");
        }
    }

    private void AddDoctorCheckGroup(
        IReadOnlyList<DoctorCheck> checks,
        string kind,
        string title,
        string symbol,
        bool expandWhenPopulated = false)
    {
        List<DoctorCheck> matchingChecks = checks.Where(check => check.Kind == kind).ToList();
        if (matchingChecks.Count == 0)
        {
            return;
        }

        Color accent = kind switch
        {
            "healthy" => Color.FromArgb(255, 81, 212, 120),
            "attention" => Color.FromArgb(255, 255, 193, 7),
            _ => Color.FromArgb(255, 78, 161, 255)
        };
        Color surface = kind switch
        {
            "healthy" => Color.FromArgb(255, 31, 58, 43),
            "attention" => Color.FromArgb(255, 62, 53, 25),
            _ => Color.FromArgb(255, 41, 58, 74)
        };

        var details = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 4) };
        foreach (DoctorCheck check in matchingChecks)
        {
            details.Children.Add(CreateDoctorCheckRow(check));
        }

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = symbol,
            Foreground = new SolidColorBrush(accent),
            FontSize = 19,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleText, 1);
        header.Children.Add(titleText);
        var count = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 3, 9, 3),
            Child = new TextBlock
            {
                Text = matchingChecks.Count.ToString(CultureInfo.InvariantCulture),
                Foreground = new SolidColorBrush(accent),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            }
        };
        Grid.SetColumn(count, 2);
        header.Children.Add(count);

        var expander = new Expander
        {
            Header = header,
            Content = details,
            IsExpanded = expandWhenPopulated,
            Background = new SolidColorBrush(surface),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 2, 8, 8)
        };
        AutomationProperties.SetName(expander, $"{title}, {matchingChecks.Count} check{(matchingChecks.Count == 1 ? string.Empty : "s")}");
        DoctorChecksPanel.Children.Add(expander);
    }

    private Border CreateDoctorCheckRow(DoctorCheck check)
    {
        string kind = check.Kind;
        string message = check.Message;
        Color foreground = check.Kind switch
        {
            "healthy" => Color.FromArgb(255, 81, 212, 120),
            "attention" => Color.FromArgb(255, 255, 193, 7),
            _ => Color.FromArgb(255, 175, 200, 226)
        };
        string symbol = kind switch { "healthy" => "✓", "attention" => "!", _ => "ⓘ" };
        var content = new Grid { ColumnSpacing = 14 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(new TextBlock
        {
            Text = $"{symbol}  {message}",
            Foreground = new SolidColorBrush(foreground),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
        if (kind == "attention" && !string.IsNullOrWhiteSpace(check.AssistantCaseId))
        {
            var reviewButton = new Button
            {
                Tag = new DoctorAssistantHandoff(check.AssistantCaseId, check.Message, DoctorScope.Text),
                Content = "Review in Assistant",
                Padding = new Thickness(13, 7, 13, 7),
                VerticalAlignment = VerticalAlignment.Center
            };
            reviewButton.Click += OpenDoctorFindingAssistant;
            AutomationProperties.SetName(reviewButton, $"Review {check.Message} in VaultKind Assistant");
            Grid.SetColumn(reviewButton, 1);
            content.Children.Add(reviewButton);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(150, 31, 35, 37)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14, 11, 14, 11),
            Child = content
        };
    }

    private void OpenDoctorFindingAssistant(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DoctorAssistantHandoff handoff })
        {
            return;
        }

        doctorAssistantCaseId = handoff.CaseId;
        doctorAssistantEvidence = handoff.Evidence;
        ShowLearningCenter(sender, e);
        ShowAssistant(sender, e);
        AssistantSearch.Text = string.Empty;
        string evidence = $"Vault Doctor observed this during a local, read-only check:\n{handoff.Evidence}\nReport scope: {handoff.Scope}";
        ShowAssistantCase(handoff.CaseId, 100, evidence);
    }

    private void SelectSidebarDestination(Button selected, string selectedName)
    {
        SolidColorBrush blue = PaletteBrush("BrandBlueBrush");
        selected.Background = PaletteBrush("SelectedSurfaceBrush");
        selected.BorderBrush = blue;
        selected.BorderThickness = new Thickness(3, 0, 0, 0);
        SetContentForeground(selected, blue);
        AutomationProperties.SetName(selected, $"{selectedName}, selected");
    }

    private void LogActivity(string title, string detail, string category)
    {
        if (!RecordActivityHistoryToggle.IsOn)
        {
            return;
        }

        activityHistory.Add(new SessionActivity(DateTime.Now, title, detail, category));
        if (activityHistory.Count > 500)
        {
            activityHistory.RemoveRange(0, activityHistory.Count - 500);
        }
        SaveActivityHistory();
        UpdateDashboardRecentActivity();
        if (ActivityView.Visibility == Visibility.Visible)
        {
            RenderActivity();
        }
    }

    private void RenderActivity()
    {
        ActivityEventsPanel.Children.Clear();
        ClearActivityButton.IsEnabled = activityHistory.Count > 0;
        string query = ActivitySearchInput.Text.Trim();
        List<SessionActivity> matchingActivity = activityHistory
            .Where(activity => ActivityMatchesSearch(activity, query))
            .ToList();
        bool searching = query.Length > 0;

        ActivityFilterSummary.Text = searching
            ? $"{matchingActivity.Count} of {activityHistory.Count} matching"
            : $"{activityHistory.Count} event{(activityHistory.Count == 1 ? string.Empty : "s")} recorded";
        ActivityEmptyState.Visibility = matchingActivity.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ActivityEventsPanel.Visibility = matchingActivity.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ActivityEmptyTitle.Text = activityHistory.Count == 0 ? "No activity yet" : "No matching activity";
        ActivityEmptyDescription.Text = activityHistory.Count == 0
            ? "Important vault, recovery, password, and Doctor actions will appear here."
            : "Try a vault name, action, or a word from the activity description.";

        AddActivityCategoryGroup("vaults", "Vaults", "\uE8B7", Color.FromArgb(255, 78, 161, 255), matchingActivity, !searching, searching);
        AddActivityCategoryGroup("recovery", "Recovery", "\uE8D7", Color.FromArgb(255, 73, 205, 112), matchingActivity, !searching, searching);
        AddActivityCategoryGroup("passwords", "Passwords", "\uE72E", Color.FromArgb(255, 224, 171, 52), matchingActivity, !searching, searching);
        AddActivityCategoryGroup("doctor", "Doctor", "\uE95E", Color.FromArgb(255, 78, 161, 255), matchingActivity, !searching, searching);
        QueueTextScaleRefresh();
    }

    private void UpdateDashboardRecentActivity()
    {
        if (DashboardRecentActivityTitle is null)
        {
            return;
        }

        SessionActivity? latest = activityHistory.LastOrDefault();
        if (latest is null)
        {
            DashboardRecentActivityIcon.Glyph = "\uE7ED";
            DashboardRecentActivityTitle.Text = RecordActivityHistoryToggle?.IsOn == false
                ? "Activity history is paused"
                : "No activity recorded yet";
            DashboardRecentActivityDescription.Text = RecordActivityHistoryToggle?.IsOn == false
                ? "Turn local Activity history on in Preferences to record future VaultKind actions."
                : "Important vault, recovery, password, and Doctor actions will appear here.";
            DashboardRecentActivityTime.Text = string.Empty;
            DashboardRecentActivityButton.IsEnabled = false;
            DashboardRecentActivityButton.Tag = null;
            return;
        }

        string section = ActivitySectionFor(latest.Category);
        DashboardRecentActivityIcon.Glyph = section switch
        {
            "recovery" => "\uE8D7",
            "passwords" => "\uE72E",
            "doctor" => "\uE95E",
            _ => "\uE8B7"
        };
        DashboardRecentActivityTitle.Text = latest.Title;
        DashboardRecentActivityDescription.Text = latest.Detail;
        DashboardRecentActivityTime.Text = latest.Timestamp.Date == DateTime.Today
            ? latest.Timestamp.ToString("h:mm tt")
            : latest.Timestamp.ToString("MMM d, h:mm tt");
        DashboardRecentActivityButton.IsEnabled = true;
        DashboardRecentActivityButton.Tag = section;
        AutomationProperties.SetName(DashboardRecentActivityButton, $"View {ActivityCategoryLabel(section)} activity. Latest: {latest.Title}");
    }

    private void SearchActivityHistory(object sender, TextChangedEventArgs e) => RenderActivity();

    private static bool ActivityMatchesSearch(SessionActivity activity, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        return activity.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || activity.Detail.Contains(query, StringComparison.OrdinalIgnoreCase)
            || ActivityCategoryLabel(ActivitySectionFor(activity.Category)).Contains(query, StringComparison.OrdinalIgnoreCase)
            || activity.Timestamp.ToString("dddd, MMMM d, yyyy h:mm tt").Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void AddActivityCategoryGroup(string category, string title, string glyph, Color accent, IEnumerable<SessionActivity> source, bool includeEmpty, bool expandMatches)
    {
        List<SessionActivity> events = source
            .Where(activity => ActivitySectionFor(activity.Category) == category)
            .Reverse()
            .ToList();

        if (events.Count == 0 && !includeEmpty)
        {
            return;
        }

        var content = new StackPanel { Spacing = 10, Padding = new Thickness(4, 8, 4, 4) };
        if (events.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = category switch
                {
                    "vaults" => "No vault activity has been recorded yet.",
                    "recovery" => "No recovery activity has been recorded yet.",
                    "passwords" => "No password activity has been recorded yet. Passwords themselves are never recorded.",
                    _ => "No Vault Doctor activity has been recorded yet."
                },
                Foreground = PaletteBrush("MutedTextBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 4, 8, 8)
            });
        }
        else
        {
            foreach (IGrouping<DateTime, SessionActivity> day in events.GroupBy(activity => activity.Timestamp.Date))
            {
                content.Children.Add(new TextBlock
                {
                    Text = ActivityDayLabel(day.Key),
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = PaletteBrush("MutedTextBrush"),
                    Margin = new Thickness(8, content.Children.Count == 0 ? 2 : 12, 8, 0)
                });

                foreach (SessionActivity activity in day)
                {
                    content.Children.Add(CreateActivityEventCard(activity));
                }
            }
        }

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = 19,
            Foreground = new SolidColorBrush(accent),
            VerticalAlignment = VerticalAlignment.Center
        });
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 19,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 1);
        header.Children.Add(titleBlock);
        var countBadge = new Border
        {
            MinWidth = 38,
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(14),
            Background = PaletteBrush("SubtleBadgeBrush"),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = events.Count.ToString(),
                Foreground = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        Grid.SetColumn(countBadge, 2);
        header.Children.Add(countBadge);

        var expander = new Expander
        {
            Header = header,
            Content = content,
            IsExpanded = expandMatches ? events.Count > 0 : expandedActivityCategories.Contains(category),
            Tag = category,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        expander.Expanding += (_, _) => expandedActivityCategories.Add(category);
        expander.Collapsed += (_, _) => expandedActivityCategories.Remove(category);
        AutomationProperties.SetName(expander, $"{title}, {events.Count} event{(events.Count == 1 ? string.Empty : "s")}");

        ActivityEventsPanel.Children.Add(new Border
        {
            Background = PaletteBrush("CardBrush"),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 8, 14, 8),
            Child = expander
        });
    }

    private static string ActivityDayLabel(DateTime date)
    {
        DateTime today = DateTime.Today;
        if (date == today)
        {
            return "TODAY";
        }

        if (date == today.AddDays(-1))
        {
            return "YESTERDAY";
        }

        return date.ToString("dddd, MMMM d, yyyy").ToUpperInvariant();
    }

    private Border CreateActivityEventCard(SessionActivity activity)
    {
        (string glyph, Color color) = activity.Category switch
        {
            "unlock" => ("\uE785", Color.FromArgb(255, 73, 205, 112)),
            "lock" => ("\uE72E", Color.FromArgb(255, 78, 161, 255)),
            "create" => ("\uE710", Color.FromArgb(255, 78, 161, 255)),
            "connect" => ("\uE8B7", Color.FromArgb(255, 78, 161, 255)),
            "recovery" => ("\uE8D7", Color.FromArgb(255, 73, 205, 112)),
            "security" => ("\uE72E", Color.FromArgb(255, 224, 171, 52)),
            "doctor" => ("\uE95E", Color.FromArgb(255, 78, 161, 255)),
            "remove" => ("\uE74D", Color.FromArgb(255, 174, 183, 190)),
            _ => ("\uE7ED", Color.FromArgb(255, 78, 161, 255))
        };

        var grid = new Grid { ColumnSpacing = 15 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = PaletteBrush("SubtleBadgeBrush"),
            Child = new FontIcon { Glyph = glyph, FontSize = 20, Foreground = new SolidColorBrush(color) }
        });

        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = activity.Title, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock
        {
            Text = activity.Detail,
            FontSize = 13,
            Foreground = PaletteBrush("MutedTextBrush"),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var time = new TextBlock
        {
            Text = activity.Timestamp.ToString("h:mm tt"),
            FontSize = 12,
            Foreground = PaletteBrush("MutedTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(time, 2);
        grid.Children.Add(time);

        var card = new Border
        {
            Background = PaletteBrush("ListSurfaceStrongBrush"),
            BorderBrush = PaletteBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = grid
        };
        AutomationProperties.SetName(card, $"{activity.Title}. {activity.Detail}. {activity.Timestamp:h:mm tt}");
        return card;
    }

    private static string ActivitySectionFor(string category) => category switch
    {
        "recovery" => "recovery",
        "security" => "passwords",
        "doctor" => "doctor",
        "unlock" or "lock" or "create" or "connect" or "remove" or "manage" => "vaults",
        _ => "vaults"
    };

    private static string ActivityCategoryLabel(string category) => category switch
    {
        "vaults" => "Vaults",
        "recovery" => "Recovery",
        "passwords" => "Passwords",
        "doctor" => "Doctor",
        _ => "All"
    };

    private void LoadActivityHistory()
    {
        try
        {
            if (!File.Exists(ActivityHistoryPath))
            {
                return;
            }

            string json = File.ReadAllText(ActivityHistoryPath);
            List<SessionActivity>? stored = JsonSerializer.Deserialize<List<SessionActivity>>(json);
            if (stored is not null)
            {
                activityHistory.AddRange(stored
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Category))
                    .TakeLast(500));
            }
        }
        catch (JsonException)
        {
            // Preserve an unreadable history file and begin with an empty in-memory history.
        }
        catch (IOException)
        {
            // Activity is optional and must never prevent VaultKind from starting.
        }
        catch (UnauthorizedAccessException)
        {
            // Activity is optional and must never prevent VaultKind from starting.
        }

        UpdateDashboardRecentActivity();
    }

    private void SaveActivityHistory()
    {
        try
        {
            string? directory = Path.GetDirectoryName(ActivityHistoryPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = ActivityHistoryPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(activityHistory));
            File.Move(temporaryPath, ActivityHistoryPath, true);
        }
        catch (IOException)
        {
            // A history write failure must not interrupt a vault operation.
        }
        catch (UnauthorizedAccessException)
        {
            // A history write failure must not interrupt a vault operation.
        }
    }

    private void SetSelectedDestination(Button selected, Button unselected, string selectedName)
    {
        SolidColorBrush blue = PaletteBrush("BrandBlueBrush");
        selected.Background = PaletteBrush("SelectedSurfaceBrush");
        selected.BorderBrush = blue;
        selected.BorderThickness = new Thickness(3, 0, 0, 0);
        SetContentForeground(selected, blue);
        AutomationProperties.SetName(selected, $"{selectedName}, selected");

        SetDestinationUnselected(unselected, unselected == DashboardButton ? "Dashboard" : "Vault Doctor");
    }

    private void SetDestinationUnselected(Button button, string name)
    {
        button.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        button.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        button.BorderThickness = new Thickness(0);
        SetContentForeground(button, PaletteBrush("PrimaryTextBrush"));
        AutomationProperties.SetName(button, name);
    }

    private void ClearVaultSelection()
    {
        foreach (Button button in vaultButtons)
        {
            button.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            button.BorderThickness = new Thickness(0);
        }
    }

    private void SetAddVaultUnselected()
    {
        AddVaultButton.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        AddVaultButton.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        AddVaultButton.BorderThickness = new Thickness(0);
        AutomationProperties.SetName(AddVaultButton, "Add Vault");
    }

    private static void SetContentForeground(Button button, Brush foreground)
    {
        if (button.Content is not StackPanel panel)
        {
            return;
        }

        foreach (var child in panel.Children)
        {
            switch (child)
            {
                case FontIcon icon:
                    icon.Foreground = foreground;
                    break;
                case TextBlock text:
                    text.Foreground = foreground;
                    break;
            }
        }
    }

    private sealed record SessionActivity(DateTime Timestamp, string Title, string Detail, string Category);
    private sealed record DoctorCheck(string Message, string Kind, string? AssistantCaseId = null);
    private sealed record DoctorAssistantHandoff(string CaseId, string Evidence, string Scope);
    private sealed record AssistantCase(string Id, string Category, string Title, string Cause, string Checks, string Fix, string Keywords);
    private sealed record LearningDestination(string Topic, string Section);
    private sealed record LearningArticle(string Introduction, IReadOnlyList<LearningSection> Sections, string Tip);
    private sealed record LearningSection(string Title, string Body);
    private sealed record LearningSectionViewEntry(LearningSection Section, string Category, Button Button, TextBlock Answer, FontIcon Chevron, bool IsHighlightedAnswer);
}
