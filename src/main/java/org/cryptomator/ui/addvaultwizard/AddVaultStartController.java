package org.cryptomator.ui.addvaultwizard;

import dagger.Lazy;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.common.FxmlFile;
import org.cryptomator.ui.common.FxmlScene;
import org.cryptomator.ui.mainwindow.MainWindowNavigation;

import javax.inject.Inject;
import javax.inject.Named;
import javafx.fxml.FXML;
import javafx.scene.Scene;
import javafx.stage.Stage;
import java.util.ResourceBundle;

@AddVaultWizardScoped
public class AddVaultStartController implements FxController {

	private final Stage window;
	private final Lazy<Scene> newVaultScene;
	private final Lazy<Scene> existingVaultScene;
	private final Runnable recoveryAction;
	private final ResourceBundle resourceBundle;
	private final MainWindowNavigation mainWindowNavigation;

	@Inject
	AddVaultStartController(@AddVaultWizardWindow Stage window,
							@FxmlScene(FxmlFile.ADDVAULT_NEW_NAME) Lazy<Scene> newVaultScene,
							@FxmlScene(FxmlFile.ADDVAULT_EXISTING) Lazy<Scene> existingVaultScene,
							@Named("recoveryAction") Runnable recoveryAction,
							ResourceBundle resourceBundle,
							MainWindowNavigation mainWindowNavigation) {
		this.window = window;
		this.newVaultScene = newVaultScene;
		this.existingVaultScene = existingVaultScene;
		this.recoveryAction = recoveryAction;
		this.resourceBundle = resourceBundle;
		this.mainWindowNavigation = mainWindowNavigation;
	}

	@FXML
	public void createNewVault() {
		window.setScene(newVaultScene.get());
		window.setTitle(resourceBundle.getString("addvaultwizard.new.title"));
		window.sizeToScene();
	}

	@FXML
	public void addExistingVault() {
		window.setScene(existingVaultScene.get());
		window.setTitle(resourceBundle.getString("addvaultwizard.existing.title"));
		window.sizeToScene();
	}

	@FXML
	public void recoverVault() {
		window.close();
		mainWindowNavigation.showHome();
		recoveryAction.run();
	}
}
