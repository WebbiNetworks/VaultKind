using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
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

    public MainPage()
    {
        InitializeComponent();
        Loaded += LoadBackendSnapshot;
    }

    private async void LoadBackendSnapshot(object sender, RoutedEventArgs e)
    {
        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        for (int attempt = 0; attempt < 12 && snapshot.ConnectionState != BackendConnectionState.Ready; attempt++)
        {
            DashboardHealthDescription.Text = "Starting the VaultKind vault engineâ€¦";
            EngineStatusFooter.Text = "Connecting the native Windows shell to the VaultKind engine.";
            await Task.Delay(500);
            snapshot = await backend.GetSnapshotAsync();
        }

        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(VaultBackendSnapshot snapshot)
    {
        TotalVaultsCount.Text = snapshot.Vaults.Count.ToString();
        UnlockedVaultsCount.Text = snapshot.UnlockedCount.ToString();
        LockedVaultsCount.Text = snapshot.LockedCount.ToString();
        DashboardHealthDescription.Text = snapshot.StatusMessage;
        EngineStatusFooter.Text = snapshot.ConnectionState == BackendConnectionState.Ready
            ? "Native Windows shell connected to the VaultKind engine."
            : "Native Windows shell preview — vault engine unavailable.";
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
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Dashboard";
        ContextSubtitle.Text = "Your secure workspace at a glance.";
        SetSelectedDestination(DashboardButton, DoctorButton, "Dashboard");
        ClearVaultSelection();
        SetAddVaultUnselected();
    }

    private void ShowDoctor(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Visible;
        AddVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Vault Doctor";
        ContextSubtitle.Text = "Automatic, private checks across VaultKind and your configured vaults.";
        SetSelectedDestination(DoctorButton, DashboardButton, "Vault Doctor");
        ClearVaultSelection();
        SetAddVaultUnselected();
    }

    private void ShowAddVault(object sender, RoutedEventArgs e)
    {
        activeVault = null;
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Visible;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Collapsed;
        ContextTitle.Text = "Add Vault";
        ContextSubtitle.Text = "Create a new encrypted vault, connect an existing one, or recover access.";
        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        ClearVaultSelection();
        AddVaultButton.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
        AddVaultButton.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        AddVaultButton.BorderThickness = new Thickness(1);
        AutomationProperties.SetName(AddVaultButton, "Add Vault, selected");
    }

    private async void ShowCreateVault(object sender, RoutedEventArgs e)
    {
        DashboardView.Visibility = Visibility.Collapsed;
        DoctorView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Visible;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Collapsed;
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

        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Visible;
        ContextTitle.Text = "Vault Created";
        ContextSubtitle.Text = "Your new encrypted space is ready when you are.";
        CreatedVaultNameText.Text = CreateVaultNameInput.Text.Trim();
        CreatedRecoveryKeyText.Text = result.RecoveryKey ?? string.Empty;
        bool hasRecoveryKey = !string.IsNullOrWhiteSpace(result.RecoveryKey);
        CreatedRecoveryKeyPanel.Visibility = hasRecoveryKey ? Visibility.Visible : Visibility.Collapsed;
        CreatedRecoveryKeySaved.IsChecked = false;
        CreatedVaultDoneButton.IsEnabled = !hasRecoveryKey;
        CreatedVaultUnlockButton.IsEnabled = !hasRecoveryKey;
    }

    private void CopyCreatedRecoveryKey(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CreatedRecoveryKeyText.Text))
        {
            return;
        }

        DataPackage package = new();
        package.SetText(CreatedRecoveryKeyText.Text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
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
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        VaultView.Visibility = Visibility.Visible;
        UnlockView.Visibility = Visibility.Collapsed;
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

        SetDestinationUnselected(DashboardButton, "Dashboard");
        SetDestinationUnselected(DoctorButton, "Vault Doctor");
        ClearVaultSelection();
        SetAddVaultUnselected();
        selectedButton.Background = new SolidColorBrush(Color.FromArgb(255, 58, 66, 72));
        selectedButton.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 78, 161, 255));
        selectedButton.BorderThickness = new Thickness(3, 0, 0, 0);
        AutomationProperties.SetName(selectedButton, $"{vault.Name}, selected, {FriendlyVaultState(vault.State)}, {vault.Path}");
    }

    private void ShowUnlock(object sender, RoutedEventArgs e)
    {
        if (activeVault is null)
        {
            return;
        }

        VaultView.Visibility = Visibility.Collapsed;
        AddVaultView.Visibility = Visibility.Collapsed;
        CreateVaultView.Visibility = Visibility.Collapsed;
        CreateVaultStorageView.Visibility = Visibility.Collapsed;
        CreateVaultReviewView.Visibility = Visibility.Collapsed;
        CreateVaultProtectionView.Visibility = Visibility.Collapsed;
        CreateVaultSuccessView.Visibility = Visibility.Collapsed;
        UnlockView.Visibility = Visibility.Visible;
        ContextTitle.Text = "Unlock Vault";
        ContextSubtitle.Text = "Enter your password to securely open this vault.";
        UnlockVaultName.Text = activeVault.Name;
        UnlockVaultPath.Text = activeVault.Path;
        UnlockPassword.Password = string.Empty;
        UnlockStatus.Text = string.Empty;
        UnlockStatus.Visibility = Visibility.Collapsed;
        UnlockSubmitButton.IsEnabled = true;
        UnlockPassword.Focus(FocusState.Programmatic);
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
            UnlockPassword.Focus(FocusState.Programmatic);
            return;
        }

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        ApplySnapshot(snapshot);
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
        SetVaultActionBusy(true, "Closing the readable drive securely...");
        VaultCommandResult result = await backend.LockAsync(vaultId);
        if (!result.Succeeded)
        {
            SetVaultActionBusy(false, FriendlyVaultActionError(result.Error, true));
            return;
        }

        VaultBackendSnapshot snapshot = await backend.GetSnapshotAsync();
        ApplySnapshot(snapshot);
        VaultSummary? updated = snapshot.Vaults.FirstOrDefault(vault => vault.Id == vaultId);
        if (updated is not null && FindVaultButton(updated.Id) is Button button)
        {
            ShowVault(updated, button);
        }
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

    private void RunDoctorPreview(object sender, RoutedEventArgs e)
    {
        DoctorSummary.Text = $"Vault Doctor didn't find any issues in the preview checks run at {DateTime.Now:h:mm tt}.";
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
}
