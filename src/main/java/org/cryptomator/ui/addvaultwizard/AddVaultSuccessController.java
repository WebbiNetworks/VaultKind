package org.cryptomator.ui.addvaultwizard;

import org.cryptomator.common.vaults.Vault;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.fxapp.FxApplicationWindows;
import org.cryptomator.ui.mainwindow.MainWindow;
import org.cryptomator.ui.mainwindow.MainWindowNavigation;

import javax.inject.Inject;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.ReadOnlyObjectProperty;
import javafx.fxml.FXML;
import javafx.stage.Stage;

@AddVaultWizardScoped
public class AddVaultSuccessController implements FxController {

	private final FxApplicationWindows appWindows;
	private final Stage window;
	private final ReadOnlyObjectProperty<Vault> vault;
	private final Stage mainWindow;
	private final MainWindowNavigation mainWindowNavigation;

	@Inject
	AddVaultSuccessController(FxApplicationWindows appWindows,
							 @AddVaultWizardWindow Stage window,
							 @AddVaultWizardWindow ObjectProperty<Vault> vault,
							 @MainWindow Stage mainWindow,
							 MainWindowNavigation mainWindowNavigation) {
		this.appWindows = appWindows;
		this.window = window;
		this.vault = vault;
		this.mainWindow = mainWindow;
		this.mainWindowNavigation = mainWindowNavigation;
	}

	@FXML
	public void unlockAndClose() {
		close();
		appWindows.startUnlockWorkflow(vault.get(), mainWindow);
	}

	@FXML
	public void close() {
		window.close();
		mainWindowNavigation.showHome();
	}

	/* Observables */

	public ReadOnlyObjectProperty<Vault> vaultProperty() {
		return vault;
	}

	public Vault getVault() {
		return vault.get();
	}
}
