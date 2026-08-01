package org.cryptomator.ui.addvaultwizard;

import dagger.Binds;
import dagger.Module;
import dagger.Provides;
import dagger.multibindings.IntoMap;
import org.apache.commons.lang3.SystemUtils;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.ui.changepassword.NewPasswordController;
import org.cryptomator.ui.changepassword.PasswordStrengthUtil;
import org.cryptomator.ui.common.DefaultSceneFactory;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.common.FxControllerKey;
import org.cryptomator.ui.common.FxmlFile;
import org.cryptomator.ui.common.FxmlLoaderFactory;
import org.cryptomator.ui.common.FxmlScene;
import org.cryptomator.ui.common.StageFactory;
import org.cryptomator.ui.controls.FontAwesome5Icon;
import org.cryptomator.ui.controls.FontAwesome5IconView;
import org.cryptomator.ui.fxapp.PrimaryStage;
import org.cryptomator.ui.recoverykey.RecoveryKeyDisplayController;

import javax.inject.Named;
import javax.inject.Provider;
import javafx.beans.property.IntegerProperty;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleIntegerProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.StringProperty;
import javafx.geometry.Pos;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.layout.HeaderBar;
import javafx.scene.layout.HeaderButtonType;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.Region;
import javafx.scene.layout.VBox;
import javafx.scene.paint.Color;
import javafx.stage.Modality;
import javafx.stage.Stage;
import javafx.stage.StageStyle;
import java.nio.file.Path;
import java.util.Map;
import java.util.ResourceBundle;

@Module
public abstract class AddVaultModule {

	@Provides
	@AddVaultWizardWindow
	@AddVaultWizardScoped
	static FxmlLoaderFactory provideFxmlLoaderFactory(Map<Class<? extends FxController>, Provider<FxController>> factories, DefaultSceneFactory sceneFactory, ResourceBundle resourceBundle) {
		return new FxmlLoaderFactory(factories, sceneFactory, resourceBundle);
	}

	@Provides
	@AddVaultWizardWindow
	@AddVaultWizardScoped
	static Stage provideStage(StageFactory factory, @PrimaryStage Stage primaryStage) {
		Stage stage = factory.create();
		if (SystemUtils.IS_OS_WINDOWS) {
			stage.initStyle(StageStyle.EXTENDED);
			HeaderBar.setPrefButtonHeight(stage, 0);
		}
		stage.setResizable(true);
		stage.setMinWidth(760);
		stage.setMinHeight(520);
		stage.initModality(Modality.WINDOW_MODAL);
		stage.initOwner(primaryStage);
		return stage;
	}

	@Provides
	@AddVaultWizardScoped
	static ObjectProperty<Path> provideVaultPath() {
		return new SimpleObjectProperty<>();
	}

	@Provides
	@Named("vaultName")
	@AddVaultWizardScoped
	static StringProperty provideVaultName() {
		return new SimpleStringProperty("");
	}

	@Provides
	@Named("shorteningThreshold")
	@AddVaultWizardScoped
	static IntegerProperty provideShorteningThreshold() {
		return new SimpleIntegerProperty(CreateNewVaultExpertSettingsController.MAX_SHORTENING_THRESHOLD);
	}

	@Provides
	@AddVaultWizardWindow
	@AddVaultWizardScoped
	static ObjectProperty<Vault> provideVault() {
		return new SimpleObjectProperty<>();
	}

	@Provides
	@Named("recoveryKey")
	@AddVaultWizardScoped
	static StringProperty provideRecoveryKey() {
		return new SimpleStringProperty();
	}

	// ------------------
	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_START)
	@AddVaultWizardScoped
	static Scene provideAddVaultStartScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_START, window);
	}

	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_EXISTING)
	@AddVaultWizardScoped
	static Scene provideChooseExistingVaultScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_EXISTING, window);
	}

	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_NEW_NAME)
	@AddVaultWizardScoped
	static Scene provideCreateNewVaultNameScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_NEW_NAME, window);
	}

	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_NEW_LOCATION)
	@AddVaultWizardScoped
	static Scene provideCreateNewVaultLocationScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_NEW_LOCATION, window);
	}

	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_NEW_PASSWORD)
	@AddVaultWizardScoped
	static Scene provideCreateNewVaultPasswordScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_NEW_PASSWORD, window);
	}

	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_NEW_RECOVERYKEY)
	@AddVaultWizardScoped
	static Scene provideCreateNewVaultRecoveryKeyScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_NEW_RECOVERYKEY, window);
	}

	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_SUCCESS)
	@AddVaultWizardScoped
	static Scene provideCreateNewVaultSuccessScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_SUCCESS, window);
	}

	@Provides
	@FxmlScene(FxmlFile.ADDVAULT_NEW_EXPERT_SETTINGS)
	@AddVaultWizardScoped
	static Scene provideCreateNewVaultExpertSettingsScene(@AddVaultWizardWindow FxmlLoaderFactory fxmlLoaders, @AddVaultWizardWindow Stage window) {
		return createAddVaultScene(fxmlLoaders, FxmlFile.ADDVAULT_NEW_EXPERT_SETTINGS, window);
	}

	private static Scene createAddVaultScene(FxmlLoaderFactory fxmlLoaders, FxmlFile file, Stage window) {
		Scene scene = fxmlLoaders.createScene(file);
		scene.setFill(Color.web("#2B2F31"));
		if (SystemUtils.IS_OS_WINDOWS) {
			Parent content = scene.getRoot();
			Label title = new Label("VaultKind");
			HBox leading = new HBox(title);
			leading.setAlignment(Pos.CENTER_LEFT);
			leading.getStyleClass().add("app-header-title");
			Button minimize = createWindowButton("Minimize", "add-vault-window-icon-minimize", HeaderButtonType.ICONIFY, () -> window.setIconified(true));
			Button maximize = createWindowButton("Maximize or restore", "add-vault-window-icon-maximize", HeaderButtonType.MAXIMIZE, () -> window.setMaximized(!window.isMaximized()));
			Button close = createCloseButton(window);
			HBox controls = new HBox(minimize, maximize, close);
			controls.getStyleClass().add("add-vault-window-controls");
			HeaderBar header = new HeaderBar(leading, null, controls);
			header.getStyleClass().add("app-header-bar");
			VBox shell = new VBox(header, content);
			shell.getStyleClass().add("add-vault-shell");
			VBox.setVgrow(content, Priority.ALWAYS);
			scene.setRoot(shell);
		}
		return scene;
	}

	private static Button createWindowButton(String accessibleText, String iconStyleClass, HeaderButtonType buttonType, Runnable action) {
		Region icon = new Region();
		icon.getStyleClass().add(iconStyleClass);
		Button button = new Button();
		button.setGraphic(icon);
		button.setAccessibleText(accessibleText);
		button.getStyleClass().add("add-vault-window-button");
		button.setOnAction(_ -> action.run());
		HeaderBar.setButtonType(button, buttonType);
		return button;
	}

	private static Button createCloseButton(Stage window) {
		FontAwesome5IconView icon = new FontAwesome5IconView();
		icon.setGlyph(FontAwesome5Icon.TIMES);
		icon.setGlyphSize(13);
		icon.getStyleClass().add("add-vault-window-icon-close");
		Button button = new Button();
		button.setGraphic(icon);
		button.setAccessibleText("Close");
		button.getStyleClass().addAll("add-vault-window-button", "add-vault-window-button-close");
		button.setOnAction(_ -> window.close());
		HeaderBar.setButtonType(button, HeaderButtonType.CLOSE);
		return button;
	}

	// ------------------
	@Binds
	@IntoMap
	@FxControllerKey(AddVaultStartController.class)
	abstract FxController bindAddVaultStartController(AddVaultStartController controller);

	@Binds
	@IntoMap
	@FxControllerKey(ChooseExistingVaultController.class)
	abstract FxController bindChooseExistingVaultController(ChooseExistingVaultController controller);

	@Binds
	@IntoMap
	@FxControllerKey(CreateNewVaultNameController.class)
	abstract FxController bindCreateNewVaultNameController(CreateNewVaultNameController controller);

	@Binds
	@IntoMap
	@FxControllerKey(CreateNewVaultLocationController.class)
	abstract FxController bindCreateNewVaultLocationController(CreateNewVaultLocationController controller);

	@Binds
	@IntoMap
	@FxControllerKey(CreateNewVaultPasswordController.class)
	abstract FxController bindCreateNewVaultPasswordController(CreateNewVaultPasswordController controller);

	@Provides
	@IntoMap
	@FxControllerKey(NewPasswordController.class)
	static FxController provideNewPasswordController(ResourceBundle resourceBundle, PasswordStrengthUtil strengthRater) {
		return new NewPasswordController(resourceBundle, strengthRater);
	}

	@Binds
	@IntoMap
	@FxControllerKey(CreateNewVaultRecoveryKeyController.class)
	abstract FxController bindCreateNewVaultRecoveryKeyController(CreateNewVaultRecoveryKeyController controller);

	@Provides
	@IntoMap
	@FxControllerKey(RecoveryKeyDisplayController.class)
	static FxController provideRecoveryKeyDisplayController(@AddVaultWizardWindow Stage window, @Named("vaultName") StringProperty vaultName, @Named("recoveryKey") StringProperty recoveryKey, ResourceBundle localization) {
		return new RecoveryKeyDisplayController(window, vaultName.get(), recoveryKey.get(), localization);
	}

	@Binds
	@IntoMap
	@FxControllerKey(AddVaultSuccessController.class)
	abstract FxController bindAddVaultSuccessController(AddVaultSuccessController controller);

	@Binds
	@IntoMap
	@FxControllerKey(CreateNewVaultExpertSettingsController.class)
	abstract FxController bindCreateNewVaultExpertSettingsController(CreateNewVaultExpertSettingsController controller);

}
