using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using System.IO;
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
    private readonly List<Button> vaultButtons = [];
    private VaultSummary? activeVault;
    private VaultSummary? createdVault;
    private string? selectedCreateVaultParentPath;
    private string? selectedConnectVaultPath;
    private IReadOnlyList<VaultSummary> knownVaults = [];
    private readonly List<SessionActivity> activityHistory = [];
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? navigationFocusTimer;
    private bool initializingPreferences = true;
    private static readonly string ActivityHistoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaultKind", "activity.json");
    private static readonly string LearningProgressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaultKind", "learning-progress.json");
    private readonly HashSet<string> viewedLearningTopics = [];
    private string selectedLearningTopic = "how";
    private string selectedAssistantCategory = "all";
    private string doctorAssistantQuery = string.Empty;
    private string? recoveryTargetVaultId;
    private string? doctorFocusVaultId;

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
        new("VK-2005", "filesystem", "The virtual drive could not be mounted", "The selected mounting service may be unavailable, misconfigured, or unable to claim the requested drive location.", "1. Open Settings and review Virtual Drive.\n2. Confirm an available mounting service is selected.\n3. Check whether the requested drive letter or path is already in use.", "Select an available mounting service or mount location, then lock and unlock the vault again.", "mount failed virtual drive missing mounting service bts9 kt9r nosuchelementexception no value present"),
        new("VK-3001", "recovery", "Access must be restored with a recovery key", "The password is no longer known, but a valid recovery key may still restore access.", "1. Locate the recovery key stored outside the vault.\n2. Confirm it belongs to this vault.\n3. Enter every word exactly.", "Complete the recovery workflow and choose a new strong password. Store the recovery key securely and never share it.", "forgot password recovery key reset password recover access"),
        new("VK-3002", "recovery", "Vault integrity needs verification", "Interrupted synchronization, storage failure, or manual changes may have left inconsistent encrypted data.", "1. Stop synchronization changes.\n2. Preserve a backup of the encrypted vault.\n3. Run Vault Doctor and review each reported item.", "Follow only the repair action associated with a verified result. Preserve backups until the vault has been opened and checked successfully.", "health check vault doctor verify integrity damaged vault corrupted vault")
    ];

    public MainPage()
    {
        InitializeComponent();
        foreach ((Button button, string _, string _, FontIcon _) in LearningTopicButtons())
        {
            button.KeyDown += LearningTopicKeyDown;
        }
        LoadActivityHistory();
        LoadLearningProgress();
        AppPreferences preferences = AppPreferencesStore.Load();
        RememberWindowPlacementToggle.IsOn = preferences.RememberWindowPlacement;
        RecordActivityHistoryToggle.IsOn = preferences.RecordActivityHistory;
        initializingPreferences = false;
        Loaded += LoadBackendSnapshot;
        Loaded += EnsureInitialKeyboardTarget;
    }

    private void EnsureInitialKeyboardTarget(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(EnsureKeyboardEntryPoint);
    }

    public void EnsureKeyboardEntryPoint()
    {
        if (XamlRoot is null)
        {
            return;
        }

        DependencyObject? current = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
            {
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        // Focus can initially remain in the native title bar. Give keyboard
        // input a stable route into the page without changing the visible view.
        DashboardButton.Focus(FocusState.Programmatic);
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
        DashboardHealthDescription.Text = snapshot.StatusMessage;
        EngineStatusFooter.Text = snapshot.ConnectionState == BackendConnectionState.Ready
            ? "Connected securely to the local VaultKind engine."
            : "The local VaultKind engine is unavailable.";
        RenderVaultSidebar(snapshot.Vaults);
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
                Tag = vault.Id
            };
            AutomationProperties.SetName(vaultButton, $"{vault.Name}, {FriendlyVaultState(vault.State)}, {vault.Path}");
            vaultButton.Click += (_, _) => ShowVault(vault, vaultButton);
            vaultButtons.Add(vaultButton);
            VaultListPanel.Children.Add(vaultButton);
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
        SetDestinationUnselected(SettingsButton, "Settings");
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
        SetDestinationUnselected(SettingsButton, "Settings");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
        _ = RunDoctorChecksAsync();
    }

    private void ShowAddVault(object sender, RoutedEventArgs e)
    {
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
        SetDestinationUnselected(SettingsButton, "Settings");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        AddVaultButton.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
        AddVaultButton.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        AddVaultButton.BorderThickness = new Thickness(1);
        AutomationProperties.SetName(AddVaultButton, "Add Vault, selected");
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
        SetDestinationUnselected(SettingsButton, "Settings");
        SetDestinationUnselected(LearningButton, "Learning Center");
        SelectSidebarDestination(ActivityButton, "Activity");
        ClearVaultSelection();
        SetAddVaultUnselected();
        RenderActivity();
    }

    private void ClearActivity(object sender, RoutedEventArgs e)
    {
        activityHistory.Clear();
        SaveActivityHistory();
        RenderActivity();
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
        ContextTitle.Text = "Settings";
        ContextSubtitle.Text = "Review VaultKind's appearance, Windows behavior, and privacy defaults.";
        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        SetDestinationUnselected(ActivityButton, "Activity");
        SetDestinationUnselected(LearningButton, "Learning Center");
        SelectSidebarDestination(SettingsButton, "Settings");
        ClearVaultSelection();
        SetAddVaultUnselected();
    }

    private void RememberWindowPlacementChanged(object sender, RoutedEventArgs e)
    {
        SavePreferences();
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
            RecordActivityHistoryToggle.IsOn));
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
        SetDestinationUnselected(SettingsButton, "Settings");
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

    private void ShowLearningTopic(string topic)
    {
        selectedLearningTopic = topic;
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
            "faq" => ("FAQ", "Straight answers to common VaultKind questions.", "Does VaultKind upload my files?\nNo. VaultKind encrypts locally. Your existing cloud application handles synchronization if you choose a cloud folder.\n\nCan VaultKind reset my password?\nNo. Use your recovery key to restore access if you forget the password.\n\nCan I use the same vault on another Windows device?\nYes. Let the encrypted folder synchronize, then connect that existing vault on the other device.", "VaultKind is desktop first, Windows focused, and private by default.", "\uE897"),
            _ => ("How VaultKind Works", "Understand what is encrypted, where it is stored, and how you safely access it.", string.Empty, string.Empty, "\uE72E")
        };

        LearningTopicTitle.Text = title;
        LearningTopicSummary.Text = summary;
        LearningTopicIcon.Glyph = glyph;
        LearningHowContent.Visibility = topic == "how" ? Visibility.Visible : Visibility.Collapsed;
        LearningSimpleContent.Visibility = topic == "how" ? Visibility.Collapsed : Visibility.Visible;
        LearningBodyText.Text = body;
        LearningTipText.Text = tip;

        foreach ((Button button, string id, string _, FontIcon check) in LearningTopicButtons())
        {
            bool selected = id == topic;
            button.Background = new SolidColorBrush(selected ? Color.FromArgb(255, 58, 66, 72) : Color.FromArgb(0, 0, 0, 0));
            button.BorderBrush = new SolidColorBrush(selected ? Color.FromArgb(255, 78, 161, 255) : Color.FromArgb(255, 82, 93, 101));
            button.BorderThickness = selected ? new Thickness(3, 0, 0, 0) : new Thickness(1);
            check.Visibility = viewedLearningTopics.Contains(id) ? Visibility.Visible : Visibility.Collapsed;
        }

        LearningProgressText.Text = $"{viewedLearningTopics.Count} of 7 topics viewed";
        DispatcherQueue.TryEnqueue(() => LearningContentScroll.ChangeView(null, 0, null, true));
    }

    private void FilterLearningTopics(object sender, TextChangedEventArgs e)
    {
        string query = LearningSearch.Text.Trim();
        int visible = 0;
        foreach ((Button button, string _, string title, FontIcon _) in LearningTopicButtons())
        {
            bool matches = query.Length == 0 || title.Contains(query, StringComparison.OrdinalIgnoreCase);
            button.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            visible += matches ? 1 : 0;
        }
        LearningNoResults.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResetLearningProgress(object sender, RoutedEventArgs e)
    {
        viewedLearningTopics.Clear();
        SaveLearningProgress();
        foreach ((Button _, string _, string _, FontIcon check) in LearningTopicButtons())
        {
            check.Visibility = Visibility.Collapsed;
        }
        LearningProgressText.Text = "0 of 7 topics viewed";
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
                string[] validTopics = ["how", "first", "recovery", "cloud", "drive", "security", "faq"];
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
        (LearningFaqButton, "faq", "FAQ", LearningFaqCheck)
    ];

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
        IEnumerable<AssistantCase> cases = category == "all" ? AssistantCases : AssistantCases.Where(item => item.Category == category);
        string categoryName = category switch { "startup" => "Startup", "vault" => "Vault", "filesystem" => "Filesystem", "recovery" => "Recovery", _ => "All" };
        AssistantResultsTitle.Text = $"{categoryName} diagnostic cases";

        foreach (AssistantCase item in cases)
        {
            var button = new Button
            {
                Tag = item.Id,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 10, 14, 10),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = item.Id, Foreground = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255)), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = item.Title, TextWrapping = TextWrapping.Wrap }
                    }
                }
            };
            button.Click += OpenAssistantCaseFromButton;
            AssistantResultsPanel.Children.Add(button);
        }
        DispatcherQueue.TryEnqueue(() => AssistantContentScroll.ChangeView(null, 0, null, true));
    }

    private void OpenAssistantQuickCase(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) ShowAssistantCase(id, 100, "Based on the common problem area you selected. Run the local checks before treating this as confirmed.");
    }

    private void OpenAssistantCaseFromButton(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) ShowAssistantCase(id, 100, "You opened this reviewed diagnostic case directly.");
    }

    private void BackToAssistantCases(object sender, RoutedEventArgs e) => ShowAssistantCaseList(selectedAssistantCategory);

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
        DispatcherQueue.TryEnqueue(() => AssistantContentScroll.ChangeView(null, 0, null, true));
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
        SetDestinationUnselected(SettingsButton, "Settings");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
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
        SetDestinationUnselected(SettingsButton, "Settings");
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
        UpdateRecoveryForm();
        FocusAfterNavigation(RecoveryKeyInput);
    }

    private void RecoveryFormChanged(object sender, RoutedEventArgs e) => UpdateRecoveryForm();

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
            DataPackage package = new();
            package.SetText(CreatedRecoveryKeyText.Text);
            Clipboard.SetContent(package);

            CreatedRecoveryKeyCopyStatus.Foreground = new SolidColorBrush(Color.FromArgb(255, 58, 211, 111));
            CreatedRecoveryKeyCopyStatus.Text = "Recovery key copied.";
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
        SetDestinationUnselected(SettingsButton, "Settings");
        SetDestinationUnselected(LearningButton, "Learning Center");
        ClearVaultSelection();
        SetAddVaultUnselected();
        selectedButton.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
        selectedButton.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        selectedButton.BorderThickness = new Thickness(3, 0, 0, 0);
        AutomationProperties.SetName(selectedButton, $"{vault.Name}, selected, {FriendlyVaultState(vault.State)}, {vault.Path}");
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
        ContextTitle.Text = "Manage Vault";
        ContextSubtitle.Text = "Share, recover, inspect, or remove this vault without leaving the main window.";

        ManagedVaultName.Text = activeVault.Name;
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
        ManagedRemoveButton.IsEnabled = !unlocked;
        ManagedRemoveHint.Visibility = unlocked ? Visibility.Visible : Visibility.Collapsed;
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
        VaultManagementHome.Visibility = Visibility.Collapsed;
        VaultShareGuide.Visibility = Visibility.Visible;
        ContextTitle.Text = "Share Vault";
        ContextSubtitle.Text = "Share encrypted storage safely without exposing readable files.";
        VaultManagementView.ChangeView(null, 0, null, true);
    }

    private void HideVaultShareGuide(object sender, RoutedEventArgs e)
    {
        VaultShareGuide.Visibility = Visibility.Collapsed;
        VaultManagementHome.Visibility = Visibility.Visible;
        ContextTitle.Text = "Manage Vault";
        ContextSubtitle.Text = "Share, recover, inspect, or remove this vault without leaving the main window.";
        VaultManagementView.ChangeView(null, 0, null, true);
    }

    private void ShowManagedRecovery(object sender, RoutedEventArgs e)
    {
        if (activeVault is null || activeVault.State.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

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
        SetDestinationUnselected(SettingsButton, "Settings");
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
            SetVaultActionBusy(false, FriendlyVaultActionError(result.Error, true));
            return;
        }

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
        DoctorSummary.Text = "Vault Doctor is running local, read-only checks...";
        DoctorChecksPanel.Children.Clear();

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        if (snapshot.ConnectionState == BackendConnectionState.Ready)
        {
            ApplySnapshot(snapshot);
        }

        var checks = new List<(string Message, string Kind)>();
        checks.Add(snapshot.ConnectionState == BackendConnectionState.Ready
            ? ("The local VaultKind engine is responding", "healthy")
            : ("The local VaultKind engine is not available", "attention"));
        checks.Add(OperatingSystem.IsWindows()
            ? ("Windows desktop compatibility is available", "healthy")
            : ("VaultKind is not running on its supported Windows desktop platform", "attention"));

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
            checks.Add((doctorFocusVaultId is null ? "No vaults are currently configured" : "The selected vault is no longer connected", "information"));
        }

        foreach (VaultSummary vault in targets)
        {
            bool pathExists = Directory.Exists(vault.Path);
            checks.Add(pathExists
                ? ($"{vault.Name}: encrypted storage location is present", "healthy")
                : ($"{vault.Name}: encrypted storage location could not be found", "attention"));

            bool stateNeedsAttention = vault.State.Equals("missing", StringComparison.OrdinalIgnoreCase)
                || vault.State.Equals("error", StringComparison.OrdinalIgnoreCase)
                || vault.State.Contains("missing", StringComparison.OrdinalIgnoreCase);
            checks.Add(stateNeedsAttention
                ? ($"{vault.Name}: the local engine reports {FriendlyVaultState(vault.State).ToLowerInvariant()}", "attention")
                : ($"{vault.Name}: vault configuration is available to the local engine", "healthy"));

            checks.Add(($"{vault.Name}: currently {FriendlyVaultState(vault.State).ToLowerInvariant()}", "information"));

            if (pathExists)
            {
                try
                {
                    string? root = Path.GetPathRoot(vault.Path);
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        var drive = new DriveInfo(root);
                        if (drive.IsReady)
                        {
                            double freeGigabytes = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                            checks.Add(($"{vault.Name}: {freeGigabytes:N1} GB available on {root}", "information"));
                        }
                    }
                }
                catch (IOException)
                {
                    checks.Add(($"{vault.Name}: Windows could not read storage capacity", "information"));
                }
                catch (UnauthorizedAccessException)
                {
                    checks.Add(($"{vault.Name}: storage capacity is not available to this account", "information"));
                }
            }
        }

        int healthy = checks.Count(check => check.Kind == "healthy");
        int attention = checks.Count(check => check.Kind == "attention");
        int information = checks.Count(check => check.Kind == "information");
        DoctorHealthyCount.Text = healthy.ToString();
        DoctorAttentionCount.Text = attention.ToString();
        DoctorInformationCount.Text = information.ToString();
        DoctorSummary.Text = attention == 0
            ? $"Vault Doctor didn't find any issues in the checks it completed at {DateTime.Now:h:mm tt}."
            : $"Vault Doctor found {attention} item{(attention == 1 ? string.Empty : "s")} worth reviewing.";
        doctorAssistantQuery = checks.FirstOrDefault(check => check.Kind == "attention").Message ?? string.Empty;
        DoctorAssistantButton.Content = attention == 0 ? "Open Assistant" : "Review Finding in Assistant";

        foreach ((string message, string kind) in checks)
        {
            DoctorChecksPanel.Children.Add(CreateDoctorCheckRow(message, kind));
        }
    }

    private void OpenDoctorAssistant(object sender, RoutedEventArgs e)
    {
        ShowLearningCenter(sender, e);
        ShowAssistant(sender, e);
        if (!string.IsNullOrWhiteSpace(doctorAssistantQuery))
        {
            AssistantSearch.Text = doctorAssistantQuery;
            FindAssistantFix(sender, e);
        }
    }

    private static Border CreateDoctorCheckRow(string message, string kind)
    {
        Color foreground = kind switch
        {
            "healthy" => Color.FromArgb(255, 81, 212, 120),
            "attention" => Color.FromArgb(255, 255, 193, 7),
            _ => Color.FromArgb(255, 175, 200, 226)
        };
        string symbol = kind switch { "healthy" => "✓", "attention" => "!", _ => "ⓘ" };
        return new Border
        {
            Background = new SolidColorBrush(kind == "information" ? Color.FromArgb(255, 41, 58, 74) : Color.FromArgb(255, 41, 46, 49)),
            BorderBrush = new SolidColorBrush(kind == "information" ? Color.FromArgb(255, 54, 125, 194) : Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = kind == "information" ? new Thickness(1) : new Thickness(0),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14, 11, 14, 11),
            Child = new TextBlock
            {
                Text = $"{symbol}  {message}",
                Foreground = new SolidColorBrush(foreground),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private void SelectSidebarDestination(Button selected, string selectedName)
    {
        var blue = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        selected.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
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
        if (ActivityView.Visibility == Visibility.Visible)
        {
            RenderActivity();
        }
    }

    private void RenderActivity()
    {
        ActivityEventsPanel.Children.Clear();
        ActivityEmptyState.Visibility = activityHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ActivityEventsPanel.Visibility = activityHistory.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        foreach (SessionActivity activity in activityHistory.AsEnumerable().Reverse())
        {
            (string glyph, Color color) = activity.Category switch
            {
                "unlock" => ("\uE785", Color.FromArgb(255, 73, 205, 112)),
                "lock" => ("\uE72E", Color.FromArgb(255, 78, 161, 255)),
                "create" => ("\uE710", Color.FromArgb(255, 78, 161, 255)),
                "connect" => ("\uE8B7", Color.FromArgb(255, 78, 161, 255)),
                "recovery" => ("\uE8D7", Color.FromArgb(255, 73, 205, 112)),
                "remove" => ("\uE74D", Color.FromArgb(255, 174, 183, 190)),
                _ => ("\uE7ED", Color.FromArgb(255, 78, 161, 255))
            };

            var grid = new Grid { ColumnSpacing = 15 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = new SolidColorBrush(Color.FromArgb(255, 45, 52, 57)),
                Child = new FontIcon { Glyph = glyph, FontSize = 20, Foreground = new SolidColorBrush(color) }
            };

            var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = activity.Title, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            text.Children.Add(new TextBlock
            {
                Text = activity.Detail,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 174, 183, 190)),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(text, 1);

            var time = new TextBlock
            {
                Text = activity.Timestamp.ToString("h:mm tt"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 146, 156, 163)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(time, 2);

            grid.Children.Add(iconBorder);
            grid.Children.Add(text);
            grid.Children.Add(time);

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 82, 93, 101)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 14, 18, 14),
                Child = grid
            };
            AutomationProperties.SetName(card, $"{activity.Title}. {activity.Detail}. {activity.Timestamp:h:mm tt}");
            ActivityEventsPanel.Children.Add(card);
        }
    }

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
        var blue = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        selected.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
        selected.BorderBrush = blue;
        selected.BorderThickness = new Thickness(3, 0, 0, 0);
        SetContentForeground(selected, blue);
        AutomationProperties.SetName(selected, $"{selectedName}, selected");

        SetDestinationUnselected(unselected, unselected == DashboardButton ? "Dashboard" : "Vault Doctor");
    }

    private static void SetDestinationUnselected(Button button, string name)
    {
        button.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        button.BorderBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        button.BorderThickness = new Thickness(0);
        SetContentForeground(button, new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)));
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
    private sealed record AssistantCase(string Id, string Category, string Title, string Cause, string Checks, string Fix, string Keywords);
}
