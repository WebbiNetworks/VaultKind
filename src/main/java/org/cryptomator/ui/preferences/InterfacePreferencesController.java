package org.cryptomator.ui.preferences;

import org.cryptomator.common.settings.Settings;
import org.cryptomator.common.settings.UiTheme;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.traymenu.TrayMenuComponent;

import javax.inject.Inject;
import javafx.fxml.FXML;
import javafx.scene.control.CheckBox;
import javafx.scene.control.ChoiceBox;
import javafx.util.StringConverter;
import java.util.ResourceBundle;

public class InterfacePreferencesController implements FxController {

	private final Settings settings;
	private final boolean trayMenuInitialized;
	private final boolean trayMenuSupported;
	private final ResourceBundle resourceBundle;
	public ChoiceBox<UiTheme> themeChoiceBox;
	public CheckBox showTrayIconCheckbox;
	public CheckBox compactModeCheckbox;

	@Inject
	InterfacePreferencesController(Settings settings, TrayMenuComponent trayMenu, ResourceBundle resourceBundle) {
		this.settings = settings;
		this.trayMenuInitialized = trayMenu.isInitialized();
		this.trayMenuSupported = trayMenu.isSupported();
		this.resourceBundle = resourceBundle;
	}

	@FXML
	public void initialize() {
		themeChoiceBox.getItems().addAll(UiTheme.values());
		if (!themeChoiceBox.getItems().contains(settings.theme.get())) {
			settings.theme.set(UiTheme.DARK);
		}
		themeChoiceBox.valueProperty().bindBidirectional(settings.theme);
		themeChoiceBox.setConverter(new UiThemeConverter(resourceBundle));

		showTrayIconCheckbox.selectedProperty().bindBidirectional(settings.showTrayIcon);
		compactModeCheckbox.selectedProperty().bindBidirectional(settings.compactMode);

	}


	public boolean isTrayMenuInitialized() {
		return trayMenuInitialized;
	}

	public boolean isTrayMenuSupported() {
		return trayMenuSupported;
	}

	/* Helper classes */

	private static class UiThemeConverter extends StringConverter<UiTheme> {

		private final ResourceBundle resourceBundle;

		UiThemeConverter(ResourceBundle resourceBundle) {
			this.resourceBundle = resourceBundle;
		}

		@Override
		public String toString(UiTheme impl) {
			return resourceBundle.getString(impl.getDisplayName());
		}

		@Override
		public UiTheme fromString(String string) {
			throw new UnsupportedOperationException();
		}

	}

}
