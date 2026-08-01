package org.cryptomator.ui.preferences;

import org.apache.commons.lang3.SystemUtils;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.mainwindow.MainWindowNavigation;

import javax.inject.Inject;
import javafx.beans.property.ObjectProperty;
import javafx.fxml.FXML;
import javafx.scene.Node;
import javafx.scene.control.Toggle;
import javafx.scene.control.ToggleButton;
import javafx.scene.control.ToggleGroup;
import javafx.stage.Stage;
import javafx.stage.WindowEvent;

public class PreferencesController implements FxController {

	private final Stage window;
	private final ObjectProperty<SelectedPreferencesTab> selectedTabProperty;
	private final MainWindowNavigation mainWindowNavigation;
	public ToggleGroup navigationGroup;
	public ToggleButton generalNavigation;
	public ToggleButton interfaceNavigation;
	public ToggleButton volumeNavigation;
	public ToggleButton contributeNavigation;
	public ToggleButton aboutNavigation;
	public Node generalPage;
	public Node interfacePage;
	public Node volumePage;
	public Node contributePage;
	public Node aboutPage;

	@Inject
	public PreferencesController(@PreferencesWindow Stage window, ObjectProperty<SelectedPreferencesTab> selectedTabProperty, MainWindowNavigation mainWindowNavigation) {
		this.window = window;
		this.selectedTabProperty = selectedTabProperty;
		this.mainWindowNavigation = mainWindowNavigation;
	}

	@FXML
	public void initialize() {
		window.addEventHandler(WindowEvent.WINDOW_SHOWING, this::windowWillAppear);
		selectedTabProperty.addListener(observable -> this.selectChosenPage());
		navigationGroup.selectedToggleProperty().addListener((observable, oldToggle, newToggle) -> navigationChanged(oldToggle, newToggle));
		selectChosenPage();
	}

	@FXML
	public void showHome() {
		mainWindowNavigation.showHome();
	}

	private void selectChosenPage() {
		SelectedPreferencesTab selectedTab = normalizeSelection(selectedTabProperty.get());
		ToggleButton navigation = navigationFor(selectedTab);
		navigationGroup.selectToggle(navigation);
		showOnly(pageFor(selectedTab));
	}

	private SelectedPreferencesTab normalizeSelection(SelectedPreferencesTab selectedTab) {
		return switch (selectedTab) {
			case GENERAL, INTERFACE, VOLUME, CONTRIBUTE, ABOUT -> selectedTab;
			case UPDATES, ANY -> SelectedPreferencesTab.GENERAL;
		};
	}

	private ToggleButton navigationFor(SelectedPreferencesTab selectedTab) {
		return switch (selectedTab) {
			case GENERAL -> generalNavigation;
			case INTERFACE -> interfaceNavigation;
			case VOLUME -> volumeNavigation;
			case CONTRIBUTE -> contributeNavigation;
			case ABOUT -> aboutNavigation;
			case UPDATES, ANY -> generalNavigation;
		};
	}

	private Node pageFor(SelectedPreferencesTab selectedTab) {
		return switch (selectedTab) {
			case GENERAL -> generalPage;
			case INTERFACE -> interfacePage;
			case VOLUME -> volumePage;
			case CONTRIBUTE -> contributePage;
			case ABOUT -> aboutPage;
			case UPDATES, ANY -> generalPage;
		};
	}

	private void navigationChanged(Toggle oldToggle, Toggle newToggle) {
		if (newToggle == null) {
			if (oldToggle != null) {
				navigationGroup.selectToggle(oldToggle);
			}
			return;
		}
		SelectedPreferencesTab selectedTab = selectionFor(newToggle);
		if (selectedTabProperty.get() != selectedTab) {
			selectedTabProperty.set(selectedTab);
		} else {
			showOnly(pageFor(selectedTab));
		}
	}

	private SelectedPreferencesTab selectionFor(Toggle navigation) {
		if (navigation == interfaceNavigation) {
			return SelectedPreferencesTab.INTERFACE;
		} else if (navigation == volumeNavigation) {
			return SelectedPreferencesTab.VOLUME;
		} else if (navigation == contributeNavigation) {
			return SelectedPreferencesTab.CONTRIBUTE;
		} else if (navigation == aboutNavigation) {
			return SelectedPreferencesTab.ABOUT;
		} else {
			return SelectedPreferencesTab.GENERAL;
		}
	}

	private void showOnly(Node selectedPage) {
		for (Node page : new Node[]{generalPage, interfacePage, volumePage, contributePage, aboutPage}) {
			boolean selected = page == selectedPage;
			page.setVisible(selected);
			page.setManaged(selected);
		}
	}

	private void windowWillAppear(@SuppressWarnings("unused") WindowEvent windowEvent) {
		selectChosenPage();
	}

	public boolean isWindows() {
		return SystemUtils.IS_OS_WINDOWS;
	}

}
