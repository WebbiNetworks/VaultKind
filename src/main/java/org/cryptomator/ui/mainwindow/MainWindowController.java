package org.cryptomator.ui.mainwindow;

import org.apache.commons.lang3.SystemUtils;
import org.cryptomator.common.settings.Settings;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.common.vaults.VaultListManager;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.preferences.SelectedPreferencesTab;
import org.cryptomator.common.Passphrase;
import org.cryptomator.ui.controls.NiceSecurePasswordField;
import org.cryptomator.ui.keyloading.masterkeyfile.PassphraseEntryResult;
import org.cryptomator.integrations.mount.MountService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javafx.beans.Observable;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.ReadOnlyBooleanProperty;
import javafx.beans.property.ReadOnlyObjectProperty;
import javafx.beans.property.ReadOnlyStringProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.StringProperty;
import javafx.fxml.FXML;
import javafx.geometry.Rectangle2D;
import javafx.scene.Node;
import javafx.scene.layout.StackPane;
import javafx.scene.control.CheckBox;
import javafx.scene.control.Label;
import javafx.stage.Screen;
import javafx.stage.Stage;
import javafx.stage.WindowEvent;
import javafx.collections.ObservableList;
import javafx.beans.property.IntegerProperty;
import javafx.beans.property.SimpleIntegerProperty;
import java.nio.file.Files;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.ResourceBundle;

@MainWindowScoped
public class MainWindowController implements FxController {

	private static final Logger LOG = LoggerFactory.getLogger(MainWindowController.class);

	private final Stage window;
	private final ReadOnlyObjectProperty<Vault> selectedVault;
	private final Settings settings;
	private final MainWindowNavigation navigation;
	private final ResourceBundle resourceBundle;
	private final ObservableList<Vault> vaults;
	private final List<MountService> mountServices;
	private final StringProperty contextTitle = new SimpleStringProperty();
	private final StringProperty contentTitle = new SimpleStringProperty();
	private final StringProperty contentSubtitle = new SimpleStringProperty();
	private final BooleanProperty unlockButtonDisabled = new SimpleBooleanProperty(true);
	private final StringProperty doctorOverallTitle = new SimpleStringProperty();
	private final StringProperty doctorOverallDescription = new SimpleStringProperty();
	private final StringProperty doctorLastChecked = new SimpleStringProperty();
	private final IntegerProperty doctorHealthyCount = new SimpleIntegerProperty();
	private final IntegerProperty doctorAttentionCount = new SimpleIntegerProperty();
	private final IntegerProperty doctorInformationCount = new SimpleIntegerProperty(3);

	@FXML
	private StackPane root;
	@FXML
	private Node workspacePane;
	@FXML
	private Node vaultDoctorPane;
	@FXML
	private Label doctorConfigurationStatus;
	@FXML
	private Label doctorLocationStatus;
	@FXML
	private Label doctorStorageStatus;
	@FXML
	private Label doctorWindowsStatus;
	@FXML
	private Label doctorMountStatus;
	@FXML
	private Label doctorSettingsStatus;
	@FXML
	private Node settingsPane;
	@FXML
	private Node activityPane;
	@FXML
	private Node howItWorksPane;
	@FXML
	private Node addVaultPane;
	@FXML
	private StackPane addVaultContentHost;
	@FXML
	private Node unlockPane;
	@FXML
	private Node shareVaultPane;
	@FXML
	private NiceSecurePasswordField unlockPasswordField;
	@FXML
	private CheckBox unlockSavePasswordCheckbox;
	@FXML
	private Node vaultOptionsPane;
	@FXML
	private StackPane vaultOptionsContentHost;
	@FXML
	private Node vaultToolPane;
	@FXML
	private StackPane vaultToolContentHost;

	@Inject
	public MainWindowController(@MainWindow Stage window, //
								ObjectProperty<Vault> selectedVault, //
								Settings settings, //
								MainWindowNavigation navigation, //
								ResourceBundle resourceBundle, //
								ObservableList<Vault> vaults, //
								List<MountService> mountServices) {
		this.window = window;
		this.selectedVault = selectedVault;
		this.settings = settings;
		this.navigation = navigation;
		this.resourceBundle = resourceBundle;
		this.vaults = vaults;
		this.mountServices = mountServices;
	}

	@FXML
	public void initialize() {
		LOG.trace("init MainWindowController");
		unlockButtonDisabled.bind(unlockPasswordField.textProperty().isEmpty());

		if (SystemUtils.IS_OS_WINDOWS) {
			root.getStyleClass().add("os-windows");
		}
		window.focusedProperty().addListener(this::mainWindowFocusChanged);
		navigation.destinationProperty().addListener((_, _, destination) -> {
			showDestination(destination);
			updateContextTitle();
		});
		navigation.selectedPreferencesTabProperty().addListener((_, _, _) -> updateContextTitle());
		navigation.assistantModeProperty().addListener((_, _, _) -> updateContextTitle());
		selectedVault.addListener((_, _, _) -> updateContextTitle());
		navigation.addVaultContentProperty().addListener((_, _, content) -> showAddVaultContent(content));
		navigation.vaultOptionsContentProperty().addListener((_, _, content) -> showVaultOptionsContent(content));
		navigation.vaultToolContentProperty().addListener((_, _, content) -> showVaultToolContent(content));
		showAddVaultContent(navigation.addVaultContentProperty().get());
		showVaultOptionsContent(navigation.vaultOptionsContentProperty().get());
		showVaultToolContent(navigation.vaultToolContentProperty().get());
		showDestination(navigation.destinationProperty().get());
		updateContextTitle();

		int x = settings.windowXPosition.get();
		int y = settings.windowYPosition.get();
		int width = settings.windowWidth.get();
		int height = settings.windowHeight.get();
		if (windowPositionSaved(x, y, width, height)) {
			window.setX(x);
			window.setY(y);
			window.setWidth(Math.clamp(width, window.getMinWidth(), window.getMaxWidth()));
			window.setHeight(Math.clamp(height, window.getMinHeight(), window.getMaxHeight()));
		}

		window.setOnShowing(this::checkDisplayBounds);

		settings.windowXPosition.bind(window.xProperty());
		settings.windowYPosition.bind(window.yProperty());
		settings.windowWidth.bind(window.widthProperty());
		settings.windowHeight.bind(window.heightProperty());
	}

	private void updateContextTitle() {
		MainWindowNavigation.Destination destination = navigation.destinationProperty().get();
		contextTitle.set(switch (destination) {
			case HOME -> resourceBundle.getString("main.home");
			case VAULT_DOCTOR -> resourceBundle.getString("main.vaultDoctor");
			case VAULTS -> selectedVault.get() == null
					? resourceBundle.getString("main.vaultlist")
					: resourceBundle.getString("main.context.vault").formatted(selectedVault.get().getDisplayName());
			case ACTIVITY -> resourceBundle.getString("main.vaultlist.events");
			case HOW_IT_WORKS -> resourceBundle.getString(navigation.assistantModeProperty().get() ? "howItWorks.assistant.title" : "howItWorks.title");
			case ADD_VAULT -> resourceBundle.getString("addvaultwizard.title");
			case UNLOCK -> resourceBundle.getString("main.content.unlock.title");
			case SHARE_VAULT -> resourceBundle.getString("shareVault.title");
			case VAULT_OPTIONS -> resourceBundle.getString("main.content.vaultOptions.title");
			case VAULT_TOOL -> navigation.vaultToolTitleProperty().get();
			case SETTINGS -> resourceBundle.getString("main.context.settings").formatted(preferencesTabTitle());
		});
		contentTitle.set(destination == MainWindowNavigation.Destination.VAULT_TOOL
				? navigation.vaultToolTitleProperty().get()
				: resourceBundle.getString(switch (destination) {
			case HOME -> "main.content.dashboard.title";
			case VAULT_DOCTOR -> "main.vaultDoctor";
			case VAULTS -> "main.content.vaults.title";
			case ACTIVITY -> "main.content.activity.title";
			case HOW_IT_WORKS -> navigation.assistantModeProperty().get() ? "howItWorks.assistant.title" : "howItWorks.title";
			case ADD_VAULT -> "main.content.addVault.title";
			case UNLOCK -> "main.content.unlock.title";
			case SHARE_VAULT -> "shareVault.title";
			case VAULT_OPTIONS -> "main.content.vaultOptions.title";
			case VAULT_TOOL -> throw new IllegalStateException();
			case SETTINGS -> "main.content.settings.title";
		}));
		contentSubtitle.set(destination == MainWindowNavigation.Destination.VAULT_TOOL
				? navigation.vaultToolSubtitleProperty().get()
				: resourceBundle.getString(switch (destination) {
			case HOME -> "main.content.dashboard.subtitle";
			case VAULT_DOCTOR -> "main.vaultDoctor.subtitle";
			case VAULTS -> "main.content.vaults.subtitle";
			case ACTIVITY -> "main.content.activity.subtitle";
			case HOW_IT_WORKS -> navigation.assistantModeProperty().get() ? "howItWorks.assistant.workspace.subtitle" : "howItWorks.subtitle";
			case ADD_VAULT -> "main.content.addVault.subtitle";
			case UNLOCK -> "main.content.unlock.subtitle";
			case SHARE_VAULT -> "shareVault.workspace.subtitle";
			case VAULT_OPTIONS -> "main.content.vaultOptions.subtitle";
			case VAULT_TOOL -> throw new IllegalStateException();
			case SETTINGS -> "main.content.settings.subtitle";
		}));
	}

	private String preferencesTabTitle() {
		return switch (navigation.selectedPreferencesTabProperty().get()) {
			case GENERAL, ANY, UPDATES -> resourceBundle.getString("preferences.general");
			case INTERFACE -> resourceBundle.getString("preferences.interface");
			case VOLUME -> resourceBundle.getString("preferences.volume");
			case CONTRIBUTE -> resourceBundle.getString("preferences.contribute");
			case ABOUT -> resourceBundle.getString("preferences.about");
		};
	}

	private void showDestination(MainWindowNavigation.Destination destination) {
		showOnly(destination == MainWindowNavigation.Destination.SETTINGS ? settingsPane
				: destination == MainWindowNavigation.Destination.VAULT_DOCTOR ? vaultDoctorPane
				: destination == MainWindowNavigation.Destination.ACTIVITY ? activityPane
				: destination == MainWindowNavigation.Destination.HOW_IT_WORKS ? howItWorksPane
				: destination == MainWindowNavigation.Destination.ADD_VAULT ? addVaultPane
				: destination == MainWindowNavigation.Destination.UNLOCK ? unlockPane
				: destination == MainWindowNavigation.Destination.SHARE_VAULT ? shareVaultPane
				: destination == MainWindowNavigation.Destination.VAULT_OPTIONS ? vaultOptionsPane
				: destination == MainWindowNavigation.Destination.VAULT_TOOL ? vaultToolPane
				: workspacePane);
		if (destination == MainWindowNavigation.Destination.VAULT_DOCTOR) {
			runVaultDoctor();
		}
	}

	private void showVaultOptionsContent(Node content) {
		vaultOptionsContentHost.getChildren().clear();
		if (content != null) {
			vaultOptionsContentHost.getChildren().add(content);
		}
	}

	private void showVaultToolContent(Node content) {
		vaultToolContentHost.getChildren().clear();
		if (content != null) {
			vaultToolContentHost.getChildren().add(content);
		}
	}

	private void showAddVaultContent(Node content) {
		addVaultContentHost.getChildren().clear();
		if (content != null) {
			addVaultContentHost.getChildren().add(content);
		}
	}

	private void showOnly(Node selectedPane) {
		for (Node pane : new Node[]{workspacePane, vaultDoctorPane, settingsPane, activityPane, howItWorksPane, addVaultPane, unlockPane, shareVaultPane, vaultOptionsPane, vaultToolPane}) {
			boolean selected = pane == selectedPane;
			pane.setVisible(selected);
			pane.setManaged(selected);
		}
	}

	@FXML
	public void showDashboard() {
		navigation.showHome();
	}

	@FXML
	public void showVaults() {
		navigation.showVaults();
	}

	@FXML
	public void runVaultDoctor() {
		vaults.forEach(VaultListManager::redetermineVaultState);
		int healthy = 0;
		int attention = 0;

		boolean configurationsHealthy = vaults.stream().allMatch(vault -> switch (vault.getState()) {
			case LOCKED, UNLOCKED, PROCESSING -> true;
			default -> false;
		});
		setDoctorStatus(doctorConfigurationStatus, configurationsHealthy,
				vaults.isEmpty() ? resourceBundle.getString("main.vaultDoctor.status.noVaults") : resourceBundle.getString("main.vaultDoctor.status.configHealthy").formatted(vaults.size()),
				resourceBundle.getString("main.vaultDoctor.status.configAttention"));
		if (vaults.isEmpty()) {
			// No configured vault is not a fault, so keep this result informational.
		} else if (configurationsHealthy) {
			healthy++;
		} else {
			attention++;
		}

		boolean locationsHealthy = vaults.stream().allMatch(vault -> Files.isDirectory(vault.getPath()) && Files.isReadable(vault.getPath()) && Files.isWritable(vault.getPath()));
		setDoctorStatus(doctorLocationStatus, locationsHealthy,
				vaults.isEmpty() ? resourceBundle.getString("main.vaultDoctor.status.noVaults") : resourceBundle.getString("main.vaultDoctor.status.locationHealthy"),
				resourceBundle.getString("main.vaultDoctor.status.locationAttention"));
		if (!vaults.isEmpty()) {
			if (locationsHealthy) healthy++; else attention++;
		}

		boolean storageHealthy = vaults.stream().allMatch(vault -> {
			try {
				return Files.getFileStore(vault.getPath()).getUsableSpace() > 0;
			} catch (Exception e) {
				return false;
			}
		});
		setDoctorStatus(doctorStorageStatus, storageHealthy,
				vaults.isEmpty() ? resourceBundle.getString("main.vaultDoctor.status.noVaults") : resourceBundle.getString("main.vaultDoctor.status.storageHealthy"),
				resourceBundle.getString("main.vaultDoctor.status.storageAttention"));
		if (!vaults.isEmpty()) {
			if (storageHealthy) healthy++; else attention++;
		}

		boolean windowsHealthy = SystemUtils.IS_OS_WINDOWS;
		setDoctorStatus(doctorWindowsStatus, windowsHealthy, resourceBundle.getString("main.vaultDoctor.status.windowsHealthy"), resourceBundle.getString("main.vaultDoctor.status.windowsAttention"));
		if (windowsHealthy) healthy++; else attention++;

		boolean mountHealthy = !mountServices.isEmpty();
		setDoctorStatus(doctorMountStatus, mountHealthy, resourceBundle.getString("main.vaultDoctor.status.mountHealthy"), resourceBundle.getString("main.vaultDoctor.status.mountAttention"));
		if (mountHealthy) healthy++; else attention++;

		setDoctorStatus(doctorSettingsStatus, true, resourceBundle.getString("main.vaultDoctor.status.settingsHealthy"), "");
		healthy++;

		doctorHealthyCount.set(healthy);
		doctorAttentionCount.set(attention);
		doctorLastChecked.set(resourceBundle.getString("main.vaultDoctor.lastChecked").formatted(LocalDateTime.now().format(DateTimeFormatter.ofPattern("MMM d, yyyy h:mm a"))));
		if (attention == 0) {
			doctorOverallTitle.set(resourceBundle.getString("main.vaultDoctor.overallHealthy"));
			doctorOverallDescription.set(resourceBundle.getString("main.vaultDoctor.overallHealthyDescription"));
		} else {
			doctorOverallTitle.set(resourceBundle.getString("main.vaultDoctor.overallAttention").formatted(attention));
			doctorOverallDescription.set(resourceBundle.getString("main.vaultDoctor.overallAttentionDescription"));
		}
	}

	private void setDoctorStatus(Label label, boolean healthy, String healthyText, String attentionText) {
		label.setText((healthy ? "✓  " : "!  ") + (healthy ? healthyText : attentionText));
		label.getStyleClass().removeAll("vault-doctor-status-healthy", "vault-doctor-status-attention");
		label.getStyleClass().add(healthy ? "vault-doctor-status-healthy" : "vault-doctor-status-attention");
	}

	@FXML
	public void submitUnlock() {
		Passphrase password = Passphrase.copyOf(unlockPasswordField.getCharacters());
		navigation.submitUnlock(new PassphraseEntryResult(password, unlockSavePasswordCheckbox.isSelected()));
		unlockPasswordField.wipe();
	}

	@FXML
	public void cancelUnlock() {
		unlockPasswordField.wipe();
		unlockSavePasswordCheckbox.setSelected(false);
		navigation.cancelUnlock();
	}

	public boolean isWindows() {
		return SystemUtils.IS_OS_WINDOWS;
	}

	private boolean windowPositionSaved(int x, int y, int width, int height) {
		return x != 0 || y != 0 || width != 0 || height != 0;
	}

	private void checkDisplayBounds(WindowEvent windowEvent) {
		int x = settings.windowXPosition.get();
		int y = settings.windowYPosition.get();
		int width = settings.windowWidth.get();
		int height = settings.windowHeight.get();

		Rectangle2D primaryScreenBounds = Screen.getPrimary().getBounds();
		if (!isWithinDisplayBounds(x, y, width, height)) { //use stored window position
			LOG.debug("Resetting window position due to insufficient screen overlap");
			var centeredX = (primaryScreenBounds.getWidth() - window.getMinWidth()) / 2;
			var centeredY = (primaryScreenBounds.getHeight() - window.getMinHeight()) / 2;
			//check if we can keep width and height
			if (isWithinDisplayBounds((int) centeredX, (int) centeredY, width, height)) {
				//if so, keep window size
				window.setWidth(Math.clamp(width, window.getMinWidth(), window.getMaxWidth()));
				window.setHeight(Math.clamp(height, window.getMinHeight(), window.getMaxHeight()));
			}
			//reset position of upper left corner
			window.setX(centeredX);
			window.setY(centeredY);
		}
	}

	private boolean isWithinDisplayBounds(int x, int y, int width, int height) {
		// define a rect which is inset on all sides from the window's rect:
		final int shrinkedX = x + 20; // 20px left
		final int shrinkedY = y + 5; // 5px top
		final int shrinkedWidth = width - 40; // 20px left + 20px right
		final int shrinkedHeigth = height - 25; // 5px top + 20px bottom
		return isRectangleWithinBounds(shrinkedX, shrinkedY, 0, shrinkedHeigth) // Left pixel column
				&& isRectangleWithinBounds(shrinkedX + shrinkedWidth, shrinkedY, 0, shrinkedHeigth) // Right pixel column
				&& isRectangleWithinBounds(shrinkedX, shrinkedY, shrinkedWidth, 0) // Top pixel row
				&& isRectangleWithinBounds(shrinkedX, shrinkedY + shrinkedHeigth, shrinkedWidth, 0); // Bottom pixel row
	}

	private boolean isRectangleWithinBounds(int x, int y, int width, int height) {
		return !Screen.getScreensForRectangle(x, y, width, height).isEmpty();
	}

	private void mainWindowFocusChanged(Observable observable) {
		var v = selectedVault.get();
		if (v != null) {
			VaultListManager.redetermineVaultState(v);
		}
	}

	@FXML
	public void showGeneralPreferences() {
		navigation.showSettings(SelectedPreferencesTab.GENERAL);
	}

	public ReadOnlyBooleanProperty debugModeEnabledProperty() {
		return settings.debugMode;
	}

	public boolean getDebugModeEnabled() {
		return debugModeEnabledProperty().get();
	}

	public ReadOnlyStringProperty contextTitleProperty() {
		return contextTitle;
	}

	public String getContextTitle() {
		return contextTitle.get();
	}

	public ReadOnlyStringProperty contentTitleProperty() {
		return contentTitle;
	}

	public String getContentTitle() {
		return contentTitle.get();
	}

	public ReadOnlyStringProperty contentSubtitleProperty() {
		return contentSubtitle;
	}

	public String getContentSubtitle() {
		return contentSubtitle.get();
	}

	public ReadOnlyObjectProperty<String> unlockVaultNameProperty() {
		return navigation.unlockVaultNameProperty();
	}

	public String getUnlockVaultName() {
		return unlockVaultNameProperty().get();
	}

	public ReadOnlyBooleanProperty unlockWrongPasswordProperty() {
		return navigation.unlockWrongPasswordProperty();
	}

	public boolean isUnlockWrongPassword() {
		return unlockWrongPasswordProperty().get();
	}

	public ReadOnlyBooleanProperty unlockSavePasswordAvailableProperty() {
		return navigation.unlockSavePasswordAvailableProperty();
	}

	public boolean isUnlockSavePasswordAvailable() {
		return unlockSavePasswordAvailableProperty().get();
	}

	public ReadOnlyBooleanProperty unlockButtonDisabledProperty() {
		return unlockButtonDisabled;
	}

	public boolean isUnlockButtonDisabled() {
		return unlockPasswordField == null || unlockPasswordField.getText().isEmpty();
	}

	public StringProperty doctorOverallTitleProperty() {
		return doctorOverallTitle;
	}

	public String getDoctorOverallTitle() {
		return doctorOverallTitle.get();
	}

	public StringProperty doctorOverallDescriptionProperty() {
		return doctorOverallDescription;
	}

	public String getDoctorOverallDescription() {
		return doctorOverallDescription.get();
	}

	public StringProperty doctorLastCheckedProperty() {
		return doctorLastChecked;
	}

	public String getDoctorLastChecked() {
		return doctorLastChecked.get();
	}

	public IntegerProperty doctorHealthyCountProperty() {
		return doctorHealthyCount;
	}

	public int getDoctorHealthyCount() {
		return doctorHealthyCount.get();
	}

	public IntegerProperty doctorAttentionCountProperty() {
		return doctorAttentionCount;
	}

	public int getDoctorAttentionCount() {
		return doctorAttentionCount.get();
	}

	public IntegerProperty doctorInformationCountProperty() {
		return doctorInformationCount;
	}

	public int getDoctorInformationCount() {
		return doctorInformationCount.get();
	}

	public ReadOnlyObjectProperty<String> vaultOptionsVaultNameProperty() {
		return navigation.vaultOptionsVaultNameProperty();
	}

	public ReadOnlyObjectProperty<String> shareVaultNameProperty() {
		return navigation.shareVaultNameProperty();
	}

	public String getShareVaultName() {
		return shareVaultNameProperty().get();
	}

	public String getVaultOptionsVaultName() {
		return vaultOptionsVaultNameProperty().get();
	}

	public ReadOnlyObjectProperty<String> vaultToolTitleProperty() {
		return navigation.vaultToolTitleProperty();
	}

	public String getVaultToolTitle() {
		return vaultToolTitleProperty().get();
	}

	public ReadOnlyObjectProperty<String> vaultToolSubtitleProperty() {
		return navigation.vaultToolSubtitleProperty();
	}

	public String getVaultToolSubtitle() {
		return vaultToolSubtitleProperty().get();
	}

	public ReadOnlyObjectProperty<String> vaultToolVaultNameProperty() {
		return navigation.vaultToolVaultNameProperty();
	}

	public String getVaultToolVaultName() {
		return vaultToolVaultNameProperty().get();
	}

}
