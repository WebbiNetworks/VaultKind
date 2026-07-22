package org.cryptomator.ui.mainwindow;

import org.cryptomator.ui.common.FxController;

import javax.inject.Inject;
import javafx.fxml.FXML;

@MainWindowScoped
public class ActivityController implements FxController {

	private final MainWindowNavigation navigation;

	@Inject
	ActivityController(MainWindowNavigation navigation) {
		this.navigation = navigation;
	}

	@FXML
	public void showDashboard() {
		navigation.showHome();
	}
}
