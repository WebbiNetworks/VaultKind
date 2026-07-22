package org.cryptomator.ui.mainwindow;

import org.cryptomator.ui.common.FxController;

import javax.inject.Inject;
import javafx.application.Platform;
import javafx.fxml.FXML;
import javafx.scene.control.ScrollPane;

@MainWindowScoped
public class HowItWorksController implements FxController {

	private final MainWindowNavigation navigation;
	private final VaultListController vaultListController;
	@FXML
	private ScrollPane contentScroll;

	@Inject
	HowItWorksController(MainWindowNavigation navigation, VaultListController vaultListController) {
		this.navigation = navigation;
		this.vaultListController = vaultListController;
	}

	@FXML
	public void initialize() {
		navigation.destinationProperty().addListener((_, _, destination) -> {
			if (destination == MainWindowNavigation.Destination.HOW_IT_WORKS) {
				Platform.runLater(() -> contentScroll.setVvalue(0));
			}
		});
	}

	@FXML
	public void showDashboard() {
		navigation.showHome();
	}

	@FXML
	public void addVault() {
		vaultListController.didClickAddVault();
	}
}
