package org.cryptomator.ui.addvaultwizard;

import dagger.Lazy;
import org.cryptomator.common.vaults.NotAVaultDirectoryException;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.common.vaults.VaultListManager;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.common.FxmlFile;
import org.cryptomator.ui.common.FxmlScene;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.ReadOnlyBooleanProperty;
import javafx.beans.property.ReadOnlyStringProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.StringProperty;
import javafx.fxml.FXML;
import javafx.scene.Scene;
import javafx.stage.DirectoryChooser;
import javafx.stage.Stage;
import java.io.File;
import java.io.IOException;
import java.nio.file.Path;
import java.util.ResourceBundle;

@AddVaultWizardScoped
public class ChooseExistingVaultController implements FxController {

	private static final Logger LOG = LoggerFactory.getLogger(ChooseExistingVaultController.class);

	private final Stage window;
	private final Lazy<Scene> startScene;
	private final Lazy<Scene> successScene;
	private final ObjectProperty<Path> vaultPath;
	private final ObjectProperty<Vault> vault;
	private final VaultListManager vaultListManager;
	private final ResourceBundle resourceBundle;
	private final BooleanProperty selectionReady = new SimpleBooleanProperty(false);
	private final BooleanProperty selectionError = new SimpleBooleanProperty(false);
	private final BooleanProperty selectionMade = new SimpleBooleanProperty(false);
	private final StringProperty selectedVaultName = new SimpleStringProperty("");
	private final StringProperty selectedVaultLocation = new SimpleStringProperty("");
	private final StringProperty selectionMessage = new SimpleStringProperty("");
	private final StringProperty stepCount = new SimpleStringProperty();

	@Inject
	ChooseExistingVaultController(@AddVaultWizardWindow Stage window, //
								  @FxmlScene(FxmlFile.ADDVAULT_START) Lazy<Scene> startScene, //
								  @FxmlScene(FxmlFile.ADDVAULT_SUCCESS) Lazy<Scene> successScene, //
								  ObjectProperty<Path> vaultPath, //
								  @AddVaultWizardWindow ObjectProperty<Vault> vault, //
								  VaultListManager vaultListManager, //
								  ResourceBundle resourceBundle) {
		this.window = window;
		this.startScene = startScene;
		this.successScene = successScene;
		this.vaultPath = vaultPath;
		this.vault = vault;
		this.vaultListManager = vaultListManager;
		this.resourceBundle = resourceBundle;
		this.stepCount.bind(selectionReady.map(ready -> resourceBundle.getString(ready ? "addvaultwizard.existing.progress.review" : "addvaultwizard.existing.progress.select")));
	}

	@FXML
	public void back() {
		window.setScene(startScene.get());
	}

	@FXML
	public void chooseFolder() {
		DirectoryChooser directoryChooser = new DirectoryChooser();
		directoryChooser.setTitle(resourceBundle.getString("addvaultwizard.existing.folderPickerTitle"));
		File selectedFolder = directoryChooser.showDialog(window);
		if (selectedFolder != null) {
			validateSelection(selectedFolder.toPath().toAbsolutePath());
		}
	}

	private void validateSelection(Path selectedPath) {
		vaultPath.set(selectedPath);
		selectedVaultName.set(selectedPath.getFileName() == null ? selectedPath.toString() : selectedPath.getFileName().toString());
		selectedVaultLocation.set(selectedPath.toString());
		selectionMade.set(true);
		selectionReady.set(false);
		selectionError.set(false);
		try {
			VaultListManager.assertIsVaultDirectory(selectedPath);
			if (vaultListManager.isAlreadyAdded(selectedPath.normalize().toAbsolutePath())) {
				showSelectionError(resourceBundle.getString("addvaultwizard.existing.alreadyAdded"));
			} else {
				selectionReady.set(true);
				selectionMessage.set(resourceBundle.getString("addvaultwizard.existing.validSelection"));
			}
		} catch (NotAVaultDirectoryException e) {
			LOG.warn("Selected folder is not a vault directory: {}", e.getMessage());
			String descriptionKey = switch (e.notAVaultReason()) {
				case MISSING_DATA_DIR -> "addvaultwizard.existing.notAVault.description.missingDataDir";
				case DATA_NOT_A_DIRECTORY -> "addvaultwizard.existing.notAVault.description.dataNotADirectory";
				case MISSING_VAULT_CONFIG -> "addvaultwizard.existing.notAVault.description.missingVaultConfig";
				case VAULT_CONFIG_ACCESS_DENIED -> "addvaultwizard.existing.notAVault.description.vaultConfigAccessDenied";
				case UNSUPPORTED_STRUCTURE -> "addvaultwizard.existing.notAVault.description.unsupportedStructure";
			};
			showSelectionError(resourceBundle.getString(descriptionKey).formatted(selectedVaultName.get()));
		} catch (IOException e) {
			LOG.error("Failed to validate existing vault.", e);
			showSelectionError(resourceBundle.getString("addvaultwizard.existing.validationFailed"));
		}
	}

	private void showSelectionError(String message) {
		selectionReady.set(false);
		selectionError.set(true);
		selectionMessage.set(message);
	}

	@FXML
	public void connectVault() {
		if (!selectionReady.get() || vaultPath.get() == null) {
			return;
		}
		try {
			Vault newVault = vaultListManager.add(vaultPath.get());
			vault.set(newVault);
			window.setScene(successScene.get());
		} catch (NotAVaultDirectoryException e) {
			validateSelection(vaultPath.get());
		} catch (IOException e) {
			LOG.error("Failed to open existing vault.", e);
			showSelectionError(resourceBundle.getString("addvaultwizard.existing.connectFailed"));
		}
	}

	public ReadOnlyBooleanProperty selectionReadyProperty() {
		return selectionReady;
	}

	public boolean isSelectionReady() {
		return selectionReady.get();
	}

	public ReadOnlyBooleanProperty selectionErrorProperty() {
		return selectionError;
	}

	public ReadOnlyBooleanProperty selectionMadeProperty() {
		return selectionMade;
	}

	public boolean isSelectionMade() {
		return selectionMade.get();
	}

	public boolean isSelectionError() {
		return selectionError.get();
	}

	public ReadOnlyStringProperty selectedVaultNameProperty() {
		return selectedVaultName;
	}

	public String getSelectedVaultName() {
		return selectedVaultName.get();
	}

	public ReadOnlyStringProperty selectedVaultLocationProperty() {
		return selectedVaultLocation;
	}

	public String getSelectedVaultLocation() {
		return selectedVaultLocation.get();
	}

	public ReadOnlyStringProperty selectionMessageProperty() {
		return selectionMessage;
	}

	public String getSelectionMessage() {
		return selectionMessage.get();
	}

	public ReadOnlyStringProperty stepCountProperty() {
		return stepCount;
	}

	public String getStepCount() {
		return stepCount.get();
	}

}
