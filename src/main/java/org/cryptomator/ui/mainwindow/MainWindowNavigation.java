package org.cryptomator.ui.mainwindow;

import org.cryptomator.ui.preferences.SelectedPreferencesTab;
import org.cryptomator.ui.vaultoptions.SelectedVaultOptionsTab;
import org.cryptomator.ui.vaultoptions.VaultOptionsComponent;
import org.cryptomator.ui.keyloading.masterkeyfile.PassphraseEntryResult;
import org.cryptomator.ui.fxapp.FxApplicationScoped;

import javax.inject.Inject;
import javafx.application.Platform;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.ReadOnlyObjectProperty;
import javafx.beans.property.ReadOnlyObjectWrapper;
import javafx.beans.property.ReadOnlyBooleanProperty;
import javafx.beans.property.ReadOnlyBooleanWrapper;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.beans.value.ChangeListener;
import javafx.scene.Node;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.scene.layout.VBox;
import javafx.scene.layout.StackPane;
import javafx.stage.Stage;
import java.util.IdentityHashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

@FxApplicationScoped
public class MainWindowNavigation {

	public enum Destination {
		HOME,
		VAULT_DOCTOR,
		VAULTS,
		ACTIVITY,
		HOW_IT_WORKS,
		ADD_VAULT,
		UNLOCK,
		SHARE_VAULT,
		VAULT_OPTIONS,
		VAULT_TOOL,
		SETTINGS
	}

	private final ObjectProperty<Destination> destination = new SimpleObjectProperty<>(Destination.HOME);
	private final ObjectProperty<SelectedPreferencesTab> selectedPreferencesTab = new SimpleObjectProperty<>(SelectedPreferencesTab.GENERAL);
	private final BooleanProperty assistantMode = new SimpleBooleanProperty(false);
	private final ReadOnlyObjectWrapper<Node> addVaultContent = new ReadOnlyObjectWrapper<>();
	private final ReadOnlyObjectWrapper<String> unlockVaultName = new ReadOnlyObjectWrapper<>("");
	private final ReadOnlyBooleanWrapper unlockWrongPassword = new ReadOnlyBooleanWrapper(false);
	private final ReadOnlyBooleanWrapper unlockSavePasswordAvailable = new ReadOnlyBooleanWrapper(false);
	private final ReadOnlyObjectWrapper<String> shareVaultName = new ReadOnlyObjectWrapper<>("");
	private final ReadOnlyObjectWrapper<Node> vaultOptionsContent = new ReadOnlyObjectWrapper<>();
	private final ReadOnlyObjectWrapper<String> vaultOptionsVaultName = new ReadOnlyObjectWrapper<>("");
	private final ReadOnlyObjectWrapper<Node> vaultToolContent = new ReadOnlyObjectWrapper<>();
	private final ReadOnlyObjectWrapper<String> vaultToolTitle = new ReadOnlyObjectWrapper<>("");
	private final ReadOnlyObjectWrapper<String> vaultToolSubtitle = new ReadOnlyObjectWrapper<>("");
	private final ReadOnlyObjectWrapper<String> vaultToolVaultName = new ReadOnlyObjectWrapper<>("");
	private final Map<Scene, Parent> addVaultRoots = new IdentityHashMap<>();
	private Stage addVaultStage;
	private ChangeListener<Scene> addVaultSceneListener;
	private CompletableFuture<?> unlockResult;
	private Runnable vaultToolOnClose;
	private Scene vaultToolScene;
	private Parent vaultToolRoot;
	private final Map<Scene, Parent> vaultToolRoots = new IdentityHashMap<>();
	private Stage vaultToolStage;
	private ChangeListener<Scene> vaultToolSceneListener;

	@Inject
	MainWindowNavigation() {
	}

	public void showHome() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		destination.set(Destination.HOME);
	}

	public void showVaultDoctor() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		destination.set(Destination.VAULT_DOCTOR);
	}

	public void showVaults() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		destination.set(Destination.VAULTS);
	}

	public void showSettings(SelectedPreferencesTab tab) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		selectedPreferencesTab.set(tab == SelectedPreferencesTab.ANY ? SelectedPreferencesTab.GENERAL : tab);
		destination.set(Destination.SETTINGS);
	}

	public void showActivity() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		destination.set(Destination.ACTIVITY);
	}

	public void showHowItWorks() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		destination.set(Destination.HOW_IT_WORKS);
	}

	public void showAddVault(Stage wizardStage) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		this.addVaultStage = wizardStage;
		this.addVaultSceneListener = (_, _, scene) -> mountAddVaultScene(scene);
		wizardStage.sceneProperty().addListener(addVaultSceneListener);
		mountAddVaultScene(wizardStage.getScene());
		destination.set(Destination.ADD_VAULT);
	}

	public void showUnlock(CompletableFuture<PassphraseEntryResult> result, String vaultName, boolean wrongPassword, boolean savePasswordAvailable) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		unlockVaultName.set(vaultName);
		unlockWrongPassword.set(wrongPassword);
		unlockSavePasswordAvailable.set(savePasswordAvailable);
		unlockResult = result;
		destination.set(Destination.UNLOCK);
		result.whenComplete((_, _) -> javafx.application.Platform.runLater(() -> {
			unlockVaultName.set("");
			unlockWrongPassword.set(false);
			unlockSavePasswordAvailable.set(false);
			if (destination.get() == Destination.UNLOCK) {
				destination.set(Destination.VAULTS);
			}
		}));
	}

	public void showVaultOptions(VaultOptionsComponent component, SelectedVaultOptionsTab selectedTab, String vaultName) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		Scene scene = component.scene().get();
		Parent content = scene.getRoot();
		scene.setRoot(new StackPane());
		content.getStyleClass().add("embedded-vault-options-card");
		vaultOptionsContent.set(content);
		vaultOptionsVaultName.set(vaultName);
		component.selectedTabProperty().set(selectedTab);
		destination.set(Destination.VAULT_OPTIONS);
	}

	public void showShareVault(String vaultName) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		shareVaultName.set(vaultName);
		destination.set(Destination.SHARE_VAULT);
	}

	public void showVaultTool(Scene scene, String title, String subtitle, String vaultName, Runnable onClose) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		Parent content = scene.getRoot();
		scene.setRoot(new StackPane());
		content.getStyleClass().add("embedded-vault-tool-card");
		vaultToolTitle.set(title);
		vaultToolSubtitle.set(subtitle);
		vaultToolVaultName.set(vaultName);
		vaultToolOnClose = onClose;
		vaultToolScene = scene;
		vaultToolRoot = content;
		destination.set(Destination.VAULT_TOOL);
		Platform.runLater(() -> {
			if (destination.get() == Destination.VAULT_TOOL && vaultToolRoot == content) {
				vaultToolContent.set(content);
				content.applyCss();
				content.layout();
			}
		});
	}

	public void showVaultToolWizard(Stage stage, Scene initialScene, String title, String subtitle, String vaultName, Runnable onClose) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		leaveVaultTool();
		vaultToolTitle.set(title);
		vaultToolSubtitle.set(subtitle);
		vaultToolVaultName.set(vaultName);
		vaultToolOnClose = onClose;
		vaultToolStage = stage;
		vaultToolSceneListener = (_, _, scene) -> mountVaultToolScene(scene);
		stage.sceneProperty().addListener(vaultToolSceneListener);
		stage.setScene(initialScene);
		destination.set(Destination.VAULT_TOOL);
		mountVaultToolScene(initialScene);
	}

	private void mountVaultToolScene(Scene scene) {
		if (scene == null) {
			return;
		}
		Parent content = vaultToolRoots.computeIfAbsent(scene, currentScene -> {
			Parent root = currentScene.getRoot();
			currentScene.setRoot(new StackPane());
			root.getStyleClass().add("embedded-vault-tool-card");
			return root;
		});
		Platform.runLater(() -> {
			if (destination.get() == Destination.VAULT_TOOL && vaultToolStage != null) {
				vaultToolContent.set(content);
				content.applyCss();
				content.layout();
			}
		});
	}

	private void leaveUnlock() {
		if (unlockResult != null && !unlockResult.isDone()) {
			unlockResult.cancel(false);
		}
		unlockResult = null;
		unlockVaultName.set("");
		unlockWrongPassword.set(false);
		unlockSavePasswordAvailable.set(false);
	}

	public void submitUnlock(PassphraseEntryResult result) {
		if (unlockResult != null && !unlockResult.isDone()) {
			@SuppressWarnings("unchecked")
			var resultFuture = (CompletableFuture<PassphraseEntryResult>) unlockResult;
			resultFuture.complete(result);
		}
	}

	public void cancelUnlock() {
		leaveUnlock();
		if (destination.get() == Destination.UNLOCK) {
			destination.set(Destination.VAULTS);
		}
	}

	private void leaveVaultOptions() {
		vaultOptionsContent.set(null);
		vaultOptionsVaultName.set("");
	}

	private void leaveVaultTool() {
		if (vaultToolOnClose != null) {
			vaultToolOnClose.run();
		}
		vaultToolOnClose = null;
		vaultToolContent.set(null);
		if (vaultToolStage != null && vaultToolSceneListener != null) {
			vaultToolStage.sceneProperty().removeListener(vaultToolSceneListener);
		}
		vaultToolRoots.forEach(Scene::setRoot);
		vaultToolRoots.clear();
		vaultToolStage = null;
		vaultToolSceneListener = null;
		if (vaultToolScene != null && vaultToolRoot != null) {
			vaultToolScene.setRoot(vaultToolRoot);
		}
		vaultToolScene = null;
		vaultToolRoot = null;
		vaultToolTitle.set("");
		vaultToolSubtitle.set("");
		vaultToolVaultName.set("");
	}

	private void mountAddVaultScene(Scene scene) {
		if (scene == null) {
			return;
		}
		Parent content = addVaultRoots.computeIfAbsent(scene, this::detachRoot);
		addVaultContent.set(content);
	}

	private Parent detachRoot(Scene scene) {
		Parent root = scene.getRoot();
		scene.setRoot(new StackPane());
		if (root instanceof VBox shell && shell.getStyleClass().contains("add-vault-shell") && shell.getChildren().size() > 1) {
			Node content = shell.getChildren().get(1);
			shell.getChildren().remove(content);
			if (content instanceof Parent parent) {
				return parent;
			}
		}
		return root;
	}

	private void leaveAddVault() {
		if (addVaultStage != null && addVaultSceneListener != null) {
			addVaultStage.sceneProperty().removeListener(addVaultSceneListener);
		}
		addVaultStage = null;
		addVaultSceneListener = null;
		addVaultContent.set(null);
		addVaultRoots.clear();
	}

	public ReadOnlyObjectProperty<Destination> destinationProperty() {
		return destination;
	}

	public ReadOnlyObjectProperty<Node> addVaultContentProperty() {
		return addVaultContent.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<String> unlockVaultNameProperty() {
		return unlockVaultName.getReadOnlyProperty();
	}

	public ReadOnlyBooleanProperty unlockWrongPasswordProperty() {
		return unlockWrongPassword.getReadOnlyProperty();
	}

	public ReadOnlyBooleanProperty unlockSavePasswordAvailableProperty() {
		return unlockSavePasswordAvailable.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<String> shareVaultNameProperty() {
		return shareVaultName.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<Node> vaultOptionsContentProperty() {
		return vaultOptionsContent.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<String> vaultOptionsVaultNameProperty() {
		return vaultOptionsVaultName.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<Node> vaultToolContentProperty() {
		return vaultToolContent.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<String> vaultToolTitleProperty() {
		return vaultToolTitle.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<String> vaultToolSubtitleProperty() {
		return vaultToolSubtitle.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<String> vaultToolVaultNameProperty() {
		return vaultToolVaultName.getReadOnlyProperty();
	}

	public ObjectProperty<SelectedPreferencesTab> selectedPreferencesTabProperty() {
		return selectedPreferencesTab;
	}

	public BooleanProperty assistantModeProperty() {
		return assistantMode;
	}
}
