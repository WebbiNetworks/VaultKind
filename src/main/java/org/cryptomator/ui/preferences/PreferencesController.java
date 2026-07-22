package org.cryptomator.ui.preferences;

import org.apache.commons.lang3.SystemUtils;
import org.cryptomator.ui.common.FxController;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javafx.beans.property.ObjectProperty;
import javafx.fxml.FXML;
import javafx.scene.control.Tab;
import javafx.scene.control.TabPane;
import javafx.stage.Stage;
import javafx.stage.WindowEvent;

@PreferencesScoped
public class PreferencesController implements FxController {

	private static final Logger LOG = LoggerFactory.getLogger(PreferencesController.class);

	private final Stage window;
	private final ObjectProperty<SelectedPreferencesTab> selectedTabProperty;
	public TabPane tabPane;
	public Tab generalTab;
	public Tab interfaceTab;
	public Tab volumeTab;
	public Tab contributeTab;
	public Tab aboutTab;

	@Inject
	public PreferencesController(@PreferencesWindow Stage window, ObjectProperty<SelectedPreferencesTab> selectedTabProperty) {
		this.window = window;
		this.selectedTabProperty = selectedTabProperty;
	}

	@FXML
	public void initialize() {
		window.setOnShowing(this::windowWillAppear);
		selectedTabProperty.addListener(observable -> this.selectChosenTab());
		tabPane.getSelectionModel().selectedItemProperty().addListener(observable -> this.selectedTabChanged());
	}

	private void selectChosenTab() {
		Tab toBeSelected = getTabToSelect(selectedTabProperty.get());
		tabPane.getSelectionModel().select(toBeSelected);
	}

	private Tab getTabToSelect(SelectedPreferencesTab selectedTab) {
		return switch (selectedTab) {
			case GENERAL -> generalTab;
			case INTERFACE -> interfaceTab;
			case VOLUME -> volumeTab;
			case UPDATES -> generalTab;
			case CONTRIBUTE -> contributeTab;
			case ABOUT -> aboutTab;
			case ANY -> generalTab;
		};
	}

	private void selectedTabChanged() {
		Tab selectedTab = tabPane.getSelectionModel().getSelectedItem();
		try {
			SelectedPreferencesTab selectedPreferencesTab = SelectedPreferencesTab.valueOf(selectedTab.getId());
			selectedTabProperty.set(selectedPreferencesTab);
		} catch (IllegalArgumentException e) {
			LOG.error("Unknown preferences tab id: {}", selectedTab.getId());
		}
	}

	private void windowWillAppear(@SuppressWarnings("unused") WindowEvent windowEvent) {
		selectChosenTab();
	}

	public boolean isWindows() {
		return SystemUtils.IS_OS_WINDOWS;
	}

}
