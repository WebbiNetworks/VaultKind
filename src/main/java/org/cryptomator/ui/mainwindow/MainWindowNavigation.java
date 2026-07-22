package org.cryptomator.ui.mainwindow;

import org.cryptomator.ui.preferences.SelectedPreferencesTab;
import org.cryptomator.ui.vaultoptions.SelectedVaultOptionsTab;
import org.cryptomator.ui.vaultoptions.VaultOptionsComponent;
import org.cryptomator.ui.keyloading.masterkeyfile.PassphraseEntryResult;
import org.cryptomator.ui.fxapp.FxApplicationScoped;

import javax.inject.Inject;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.ReadOnlyObjectProperty;
import javafx.beans.property.ReadOnlyObjectWrapper;
import javafx.beans.property.ReadOnlyBooleanProperty;
import javafx.beans.property.ReadOnlyBooleanWrapper;
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
		VAULTS,
		ACTIVITY,
		HOW_IT_WORKS,
		ADD_VAULT,
		UNLOCK,
		VAULT_OPTIONS,
		SETTINGS
	}

	private final ObjectProperty<Destination> destination = new SimpleObjectProperty<>(Destination.HOME);
	private final ObjectProperty<SelectedPreferencesTab> selectedPreferencesTab = new SimpleObjectProperty<>(SelectedPreferencesTab.GENERAL);
	private final ReadOnlyObjectWrapper<Node> addVaultContent = new ReadOnlyObjectWrapper<>();
	private final ReadOnlyObjectWrapper<String> unlockVaultName = new ReadOnlyObjectWrapper<>("");
	private final ReadOnlyBooleanWrapper unlockWrongPassword = new ReadOnlyBooleanWrapper(false);
	private final ReadOnlyBooleanWrapper unlockSavePasswordAvailable = new ReadOnlyBooleanWrapper(false);
	private final ReadOnlyObjectWrapper<Node> vaultOptionsContent = new ReadOnlyObjectWrapper<>();
	private final ReadOnlyObjectWrapper<String> vaultOptionsVaultName = new ReadOnlyObjectWrapper<>("");
	private final Map<Scene, Parent> addVaultRoots = new IdentityHashMap<>();
	private Stage addVaultStage;
	private ChangeListener<Scene> addVaultSceneListener;
	private CompletableFuture<?> unlockResult;

	@Inject
	MainWindowNavigation() {
	}

	public void showHome() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		destination.set(Destination.HOME);
	}

	public void showVaults() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		destination.set(Destination.VAULTS);
	}

	public void showSettings(SelectedPreferencesTab tab) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		selectedPreferencesTab.set(tab == SelectedPreferencesTab.ANY ? SelectedPreferencesTab.GENERAL : tab);
		destination.set(Destination.SETTINGS);
	}

	public void showActivity() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		destination.set(Destination.ACTIVITY);
	}

	public void showHowItWorks() {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
		destination.set(Destination.HOW_IT_WORKS);
	}

	public void showAddVault(Stage wizardStage) {
		leaveAddVault();
		leaveUnlock();
		leaveVaultOptions();
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
		Scene scene = component.scene().get();
		Parent content = scene.getRoot();
		scene.setRoot(new StackPane());
		content.getStyleClass().add("embedded-vault-options-card");
		vaultOptionsContent.set(content);
		vaultOptionsVaultName.set(vaultName);
		component.selectedTabProperty().set(selectedTab);
		destination.set(Destination.VAULT_OPTIONS);
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

	public ReadOnlyObjectProperty<Node> vaultOptionsContentProperty() {
		return vaultOptionsContent.getReadOnlyProperty();
	}

	public ReadOnlyObjectProperty<String> vaultOptionsVaultNameProperty() {
		return vaultOptionsVaultName.getReadOnlyProperty();
	}

	public ObjectProperty<SelectedPreferencesTab> selectedPreferencesTabProperty() {
		return selectedPreferencesTab;
	}
}
