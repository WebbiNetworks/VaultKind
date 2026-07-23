package org.cryptomator.ui.mainwindow;

import dagger.Binds;
import dagger.Lazy;
import dagger.Module;
import dagger.Provides;
import dagger.multibindings.IntoMap;
import org.apache.commons.lang3.SystemUtils;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.ui.addvaultwizard.AddVaultWizardComponent;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.common.FxControllerKey;
import org.cryptomator.ui.common.FxmlFile;
import org.cryptomator.ui.common.FxmlLoaderFactory;
import org.cryptomator.ui.common.FxmlScene;
import org.cryptomator.ui.common.StageFactory;
import org.cryptomator.ui.common.StageInitializer;
import org.cryptomator.ui.common.WindowsCaptionSupport;
import org.cryptomator.ui.error.ErrorComponent;
import org.cryptomator.ui.eventview.EventListCellController;
import org.cryptomator.ui.eventview.EventViewController;
import org.cryptomator.ui.eventview.EventViewWindow;
import org.cryptomator.ui.fxapp.FxApplicationTerminator;
import org.cryptomator.ui.fxapp.PrimaryStage;
import org.cryptomator.ui.migration.MigrationComponent;
import org.cryptomator.ui.preferences.AboutController;
import org.cryptomator.ui.preferences.GeneralPreferencesController;
import org.cryptomator.ui.preferences.InterfacePreferencesController;
import org.cryptomator.ui.preferences.PreferencesController;
import org.cryptomator.ui.preferences.PreferencesWindow;
import org.cryptomator.ui.preferences.SelectedPreferencesTab;
import org.cryptomator.ui.preferences.VolumePreferencesController;
import org.cryptomator.ui.recoverykey.RecoveryKeyComponent;
import org.cryptomator.ui.stats.VaultStatisticsComponent;
import org.cryptomator.ui.traymenu.TrayMenuComponent;
import org.cryptomator.ui.wrongfilealert.WrongFileAlertComponent;

import javax.inject.Named;
import javax.inject.Provider;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.scene.Scene;
import javafx.scene.paint.Color;
import javafx.application.Platform;
import javafx.stage.Modality;
import javafx.stage.Stage;
import java.util.Map;
import java.util.ResourceBundle;

@Module(subcomponents = {AddVaultWizardComponent.class, MigrationComponent.class, VaultStatisticsComponent.class, WrongFileAlertComponent.class, ErrorComponent.class, RecoveryKeyComponent.class})
abstract class MainWindowModule {

	@Provides
	@MainWindow
	@MainWindowScoped
	static Stage provideMainWindow(@PrimaryStage Stage stage, StageInitializer initializer, FxApplicationTerminator terminator, Lazy<TrayMenuComponent> trayMenu) {
		initializer.accept(stage);
		if (SystemUtils.IS_OS_WINDOWS) {
			stage.setOnShown(event -> Platform.runLater(WindowsCaptionSupport::applyDarkTitleBar));
		}
		stage.setTitle("VaultKind");
		stage.setMinWidth(1040);
		stage.setMinHeight(680);
		stage.setWidth(1200);
		stage.setHeight(800);
		stage.setOnCloseRequest(e -> {
			if (!trayMenu.get().isInitialized()) {
				terminator.terminate();
				e.consume();
			} else {
				stage.close();
			}
		});
		return stage;
	}

	@Provides
	@MainWindowScoped
	static ObjectProperty<Vault> provideSelectedVault() {
		return new SimpleObjectProperty<>();
	}

	@Provides
	@MainWindowScoped
	static ObjectProperty<SelectedPreferencesTab> provideSelectedPreferencesTab(MainWindowNavigation navigation) {
		return navigation.selectedPreferencesTabProperty();
	}

	@Provides
	@PreferencesWindow
	@MainWindowScoped
	static Stage provideEmbeddedPreferencesWindow(@MainWindow Stage stage) {
		return stage;
	}

	@Provides
	@MainWindow
	@MainWindowScoped
	static FxmlLoaderFactory provideFxmlLoaderFactory(Map<Class<? extends FxController>, Provider<FxController>> factories, MainWindowSceneFactory sceneFactory, ResourceBundle resourceBundle) {
		return new FxmlLoaderFactory(factories, sceneFactory, resourceBundle);
	}

	@Provides
	@EventViewWindow
	@MainWindowScoped
	static FxmlLoaderFactory provideEmbeddedEventFxmlLoaderFactory(@MainWindow FxmlLoaderFactory fxmlLoaderFactory) {
		return fxmlLoaderFactory;
	}

	@Provides
	@MainWindowScoped
	@Named("errorWindow")
	static Stage provideErrorStage(@MainWindow Stage window, StageFactory factory, ResourceBundle resourceBundle) {
		Stage stage = factory.create();
		stage.setTitle(resourceBundle.getString("main.vaultDetail.error.windowTitle"));
		stage.initModality(Modality.APPLICATION_MODAL);
		stage.initOwner(window);
		return stage;
	}

	@Provides
	@FxmlScene(FxmlFile.MAIN_WINDOW)
	@MainWindowScoped
	static Scene provideMainScene(@MainWindow FxmlLoaderFactory fxmlLoaders) {
		Scene scene = fxmlLoaders.createScene(FxmlFile.MAIN_WINDOW);
		scene.setFill(Color.web("#2B2F31"));
		return scene;
	}

	// ------------------

	@Binds
	@IntoMap
	@FxControllerKey(MainWindowController.class)
	abstract FxController bindMainWindowController(MainWindowController controller);

	@Binds
	@IntoMap
	@FxControllerKey(ActivityController.class)
	abstract FxController bindActivityController(ActivityController controller);

	@Binds
	@IntoMap
	@FxControllerKey(HowItWorksController.class)
	abstract FxController bindHowItWorksController(HowItWorksController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultListController.class)
	abstract FxController bindVaultListController(VaultListController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultListContextMenuController.class)
	abstract FxController bindVaultListContextMenuController(VaultListContextMenuController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultDetailController.class)
	abstract FxController bindVaultDetailController(VaultDetailController controller);

	@Binds
	@IntoMap
	@FxControllerKey(WelcomeController.class)
	abstract FxController bindWelcomeController(WelcomeController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultDetailLockedController.class)
	abstract FxController bindVaultDetailLockedController(VaultDetailLockedController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultDetailUnlockedController.class)
	abstract FxController bindVaultDetailUnlockedController(VaultDetailUnlockedController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultDetailMissingVaultController.class)
	abstract FxController bindVaultDetailMissingVaultController(VaultDetailMissingVaultController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultDetailNeedsMigrationController.class)
	abstract FxController bindVaultDetailNeedsMigrationController(VaultDetailNeedsMigrationController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultDetailUnknownErrorController.class)
	abstract FxController bindVaultDetailUnknownErrorController(VaultDetailUnknownErrorController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VaultListCellController.class)
	abstract FxController bindVaultListCellController(VaultListCellController controller);

	@Binds
	@IntoMap
	@FxControllerKey(PreferencesController.class)
	abstract FxController bindEmbeddedPreferencesController(PreferencesController controller);

	@Binds
	@IntoMap
	@FxControllerKey(GeneralPreferencesController.class)
	abstract FxController bindEmbeddedGeneralPreferencesController(GeneralPreferencesController controller);

	@Binds
	@IntoMap
	@FxControllerKey(InterfacePreferencesController.class)
	abstract FxController bindEmbeddedInterfacePreferencesController(InterfacePreferencesController controller);

	@Binds
	@IntoMap
	@FxControllerKey(VolumePreferencesController.class)
	abstract FxController bindEmbeddedVolumePreferencesController(VolumePreferencesController controller);

	@Binds
	@IntoMap
	@FxControllerKey(AboutController.class)
	abstract FxController bindEmbeddedAboutController(AboutController controller);

	@Binds
	@IntoMap
	@FxControllerKey(EventViewController.class)
	abstract FxController bindEmbeddedEventViewController(EventViewController controller);

	@Binds
	@IntoMap
	@FxControllerKey(EventListCellController.class)
	abstract FxController bindEmbeddedEventListCellController(EventListCellController controller);


}
