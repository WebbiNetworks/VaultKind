package org.cryptomator.ui.mainwindow;

import org.cryptomator.ui.preferences.SelectedPreferencesTab;
import org.cryptomator.ui.fxapp.FxApplicationScoped;

import javax.inject.Inject;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.ReadOnlyObjectProperty;
import javafx.beans.property.ReadOnlyObjectWrapper;
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

@FxApplicationScoped
public class MainWindowNavigation {

	public enum Destination {
		HOME,
		VAULTS,
		ACTIVITY,
		HOW_IT_WORKS,
		ADD_VAULT,
		SETTINGS
	}

	private final ObjectProperty<Destination> destination = new SimpleObjectProperty<>(Destination.HOME);
	private final ObjectProperty<SelectedPreferencesTab> selectedPreferencesTab = new SimpleObjectProperty<>(SelectedPreferencesTab.GENERAL);
	private final ReadOnlyObjectWrapper<Node> addVaultContent = new ReadOnlyObjectWrapper<>();
	private final Map<Scene, Parent> addVaultRoots = new IdentityHashMap<>();
	private Stage addVaultStage;
	private ChangeListener<Scene> addVaultSceneListener;

	@Inject
	MainWindowNavigation() {
	}

	public void showHome() {
		leaveAddVault();
		destination.set(Destination.HOME);
	}

	public void showVaults() {
		leaveAddVault();
		destination.set(Destination.VAULTS);
	}

	public void showSettings(SelectedPreferencesTab tab) {
		leaveAddVault();
		selectedPreferencesTab.set(tab == SelectedPreferencesTab.ANY ? SelectedPreferencesTab.GENERAL : tab);
		destination.set(Destination.SETTINGS);
	}

	public void showActivity() {
		leaveAddVault();
		destination.set(Destination.ACTIVITY);
	}

	public void showHowItWorks() {
		leaveAddVault();
		destination.set(Destination.HOW_IT_WORKS);
	}

	public void showAddVault(Stage wizardStage) {
		leaveAddVault();
		this.addVaultStage = wizardStage;
		this.addVaultSceneListener = (_, _, scene) -> mountAddVaultScene(scene);
		wizardStage.sceneProperty().addListener(addVaultSceneListener);
		mountAddVaultScene(wizardStage.getScene());
		destination.set(Destination.ADD_VAULT);
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

	public ObjectProperty<SelectedPreferencesTab> selectedPreferencesTabProperty() {
		return selectedPreferencesTab;
	}
}
