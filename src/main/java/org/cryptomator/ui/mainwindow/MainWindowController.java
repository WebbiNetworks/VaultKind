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
import javafx.stage.Screen;
import javafx.stage.Stage;
import javafx.stage.WindowEvent;
import java.util.ResourceBundle;

@MainWindowScoped
public class MainWindowController implements FxController {

	private static final Logger LOG = LoggerFactory.getLogger(MainWindowController.class);

	private final Stage window;
	private final ReadOnlyObjectProperty<Vault> selectedVault;
	private final Settings settings;
	private final MainWindowNavigation navigation;
	private final ResourceBundle resourceBundle;
	private final StringProperty contextTitle = new SimpleStringProperty();
	private final StringProperty contentTitle = new SimpleStringProperty();
	private final StringProperty contentSubtitle = new SimpleStringProperty();
	private final BooleanProperty unlockButtonDisabled = new SimpleBooleanProperty(true);

	@FXML
	private StackPane root;
	@FXML
	private Node workspacePane;
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
	private NiceSecurePasswordField unlockPasswordField;
	@FXML
	private CheckBox unlockSavePasswordCheckbox;
	@FXML
	private Node vaultOptionsPane;
	@FXML
	private StackPane vaultOptionsContentHost;

	@Inject
	public MainWindowController(@MainWindow Stage window, //
								ObjectProperty<Vault> selectedVault, //
								Settings settings, //
								MainWindowNavigation navigation, //
								ResourceBundle resourceBundle) {
		this.window = window;
		this.selectedVault = selectedVault;
		this.settings = settings;
		this.navigation = navigation;
		this.resourceBundle = resourceBundle;
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
		selectedVault.addListener((_, _, _) -> updateContextTitle());
		navigation.addVaultContentProperty().addListener((_, _, content) -> showAddVaultContent(content));
		navigation.vaultOptionsContentProperty().addListener((_, _, content) -> showVaultOptionsContent(content));
		showAddVaultContent(navigation.addVaultContentProperty().get());
		showVaultOptionsContent(navigation.vaultOptionsContentProperty().get());
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
			case VAULTS -> selectedVault.get() == null
					? resourceBundle.getString("main.vaultlist")
					: resourceBundle.getString("main.context.vault").formatted(selectedVault.get().getDisplayName());
			case ACTIVITY -> resourceBundle.getString("main.vaultlist.events");
			case HOW_IT_WORKS -> resourceBundle.getString("howItWorks.title");
			case ADD_VAULT -> resourceBundle.getString("addvaultwizard.title");
			case UNLOCK -> resourceBundle.getString("main.content.unlock.title");
			case VAULT_OPTIONS -> resourceBundle.getString("main.content.vaultOptions.title");
			case SETTINGS -> resourceBundle.getString("main.context.settings").formatted(preferencesTabTitle());
		});
		contentTitle.set(resourceBundle.getString(switch (destination) {
			case HOME -> "main.content.dashboard.title";
			case VAULTS -> "main.content.vaults.title";
			case ACTIVITY -> "main.content.activity.title";
			case HOW_IT_WORKS -> "howItWorks.title";
			case ADD_VAULT -> "main.content.addVault.title";
			case UNLOCK -> "main.content.unlock.title";
			case VAULT_OPTIONS -> "main.content.vaultOptions.title";
			case SETTINGS -> "main.content.settings.title";
		}));
		contentSubtitle.set(resourceBundle.getString(switch (destination) {
			case HOME -> "main.content.dashboard.subtitle";
			case VAULTS -> "main.content.vaults.subtitle";
			case ACTIVITY -> "main.content.activity.subtitle";
			case HOW_IT_WORKS -> "howItWorks.subtitle";
			case ADD_VAULT -> "main.content.addVault.subtitle";
			case UNLOCK -> "main.content.unlock.subtitle";
			case VAULT_OPTIONS -> "main.content.vaultOptions.subtitle";
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
				: destination == MainWindowNavigation.Destination.ACTIVITY ? activityPane
				: destination == MainWindowNavigation.Destination.HOW_IT_WORKS ? howItWorksPane
				: destination == MainWindowNavigation.Destination.ADD_VAULT ? addVaultPane
				: destination == MainWindowNavigation.Destination.UNLOCK ? unlockPane
				: destination == MainWindowNavigation.Destination.VAULT_OPTIONS ? vaultOptionsPane
				: workspacePane);
	}

	private void showVaultOptionsContent(Node content) {
		vaultOptionsContentHost.getChildren().clear();
		if (content != null) {
			vaultOptionsContentHost.getChildren().add(content);
		}
	}

	private void showAddVaultContent(Node content) {
		addVaultContentHost.getChildren().clear();
		if (content != null) {
			addVaultContentHost.getChildren().add(content);
		}
	}

	private void showOnly(Node selectedPane) {
		for (Node pane : new Node[]{workspacePane, settingsPane, activityPane, howItWorksPane, addVaultPane, unlockPane, vaultOptionsPane}) {
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

	public ReadOnlyObjectProperty<String> vaultOptionsVaultNameProperty() {
		return navigation.vaultOptionsVaultNameProperty();
	}

	public String getVaultOptionsVaultName() {
		return vaultOptionsVaultNameProperty().get();
	}

}
