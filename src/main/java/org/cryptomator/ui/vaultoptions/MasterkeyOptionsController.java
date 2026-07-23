package org.cryptomator.ui.vaultoptions;

import org.cryptomator.common.keychain.KeychainManager;
import org.cryptomator.common.recovery.RecoveryActionType;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.ui.changepassword.ChangePasswordComponent;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.forgetpassword.ForgetPasswordComponent;
import org.cryptomator.ui.recoverykey.RecoveryKeyComponent;
import org.cryptomator.ui.mainwindow.MainWindowNavigation;

import javax.inject.Inject;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.beans.value.ObservableValue;
import javafx.fxml.FXML;
import javafx.stage.Stage;
import java.nio.file.Files;
import java.util.ResourceBundle;

import static org.cryptomator.common.Constants.MASTERKEY_FILENAME;

@VaultOptionsScoped
public class MasterkeyOptionsController implements FxController {

	private final Vault vault;
	private final Stage window;
	private final ChangePasswordComponent.Builder changePasswordWindow;
	private final RecoveryKeyComponent.Factory recoveryKeyWindow;
	private final ForgetPasswordComponent.Builder forgetPasswordWindow;
	private final KeychainManager keychain;
	private final ObservableValue<Boolean> passwordSaved;
	private final BooleanProperty masterkeyFileAvailable;
	private final MainWindowNavigation navigation;
	private final ResourceBundle resourceBundle;


	@Inject
	MasterkeyOptionsController(@VaultOptionsWindow Vault vault, @VaultOptionsWindow Stage window, ChangePasswordComponent.Builder changePasswordWindow, RecoveryKeyComponent.Factory recoveryKeyWindow, ForgetPasswordComponent.Builder forgetPasswordWindow, KeychainManager keychain, MainWindowNavigation navigation, ResourceBundle resourceBundle) {
		this.vault = vault;
		this.window = window;
		this.changePasswordWindow = changePasswordWindow;
		this.recoveryKeyWindow = recoveryKeyWindow;
		this.forgetPasswordWindow = forgetPasswordWindow;
		this.keychain = keychain;
		this.navigation = navigation;
		this.resourceBundle = resourceBundle;
		if (keychain.isSupported() && !keychain.isLocked()) {
			this.passwordSaved = keychain.getPassphraseStoredProperty(vault.getId()).orElse(false);
		} else {
			this.passwordSaved = new SimpleBooleanProperty(false);
		}
		this.masterkeyFileAvailable = new SimpleBooleanProperty(Files.exists(vault.getPath().resolve(MASTERKEY_FILENAME)));
	}

	@FXML
	public void changePassword() {
		var component = changePasswordWindow.vault(vault).owner(window).build();
		navigation.showVaultTool(component.prepareEmbeddedView(navigation::showVaults), //
				resourceBundle.getString("changepassword.title"), //
				resourceBundle.getString("changepassword.workspace.subtitle"), //
				vault.getDisplayName(), //
				component.controller()::cleanup);
	}

	@FXML
	public void showRecoveryKey() {
		var component = recoveryKeyWindow.create(vault, window, new SimpleObjectProperty<>(RecoveryActionType.SHOW_KEY));
		var scene = component.prepareEmbeddedCreation(navigation::showVaults);
		navigation.showVaultToolWizard(component.window(), scene, //
				resourceBundle.getString("recoveryKey.display.title"), //
				resourceBundle.getString("recoveryKey.workspace.subtitle"), //
				vault.getDisplayName(), //
				() -> {
					component.creationController().cleanup();
					component.successController().cleanup();
				});
	}

	@FXML
	public void showRecoverVaultDialog() {
		var component = recoveryKeyWindow.create(vault, window, new SimpleObjectProperty<>(RecoveryActionType.RESET_PASSWORD));
		var scene = component.prepareEmbeddedPasswordReset(navigation::showVaults);
		navigation.showVaultToolWizard(component.window(), scene, resourceBundle.getString("recoveryKey.recover.title"), resourceBundle.getString("recoveryKey.reset.workspace.subtitle"), vault.getDisplayName(), () -> {
			component.recoverController().cleanup();
			component.resetPasswordController().cleanup();
		});
	}

	@FXML
	public void showForgetPasswordDialog() {
		assert keychain.isSupported();
		forgetPasswordWindow.vault(vault).owner(window).build().showForgetPassword();
	}

	public ObservableValue<Boolean> passwordSavedProperty() {
		return passwordSaved;
	}

	public boolean isPasswordSaved() {
		return passwordSaved.getValue();
	}

	public BooleanProperty masterkeyFileAvailableProperty() {
		return masterkeyFileAvailable;
	}

	public boolean isMasterkeyFileAvailable() {
		return masterkeyFileAvailable.get();
	}
}
