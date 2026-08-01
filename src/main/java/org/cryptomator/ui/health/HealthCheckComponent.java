package org.cryptomator.ui.health;

import dagger.BindsInstance;
import dagger.Lazy;
import dagger.Subcomponent;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.cryptofs.VaultConfig;
import org.cryptomator.ui.common.FxmlFile;
import org.cryptomator.ui.common.FxmlScene;

import javax.inject.Named;
import javafx.scene.Scene;
import javafx.stage.Stage;
import org.cryptomator.cryptolib.api.Masterkey;
import java.util.concurrent.atomic.AtomicReference;

@HealthCheckScoped
@Subcomponent(modules = {HealthCheckModule.class})
public interface HealthCheckComponent {

	@HealthCheckWindow
	Stage window();

	@FxmlScene(FxmlFile.HEALTH_START)
	Lazy<Scene> startScene();

	StartController startController();

	AtomicReference<Masterkey> masterkeyRef();

	default Scene prepareEmbedded(Runnable closeAction, Runnable readyAction) {
		startController().prepareEmbedded(closeAction, readyAction);
		return startScene().get();
	}

	default void cleanupEmbedded() {
		startController().cleanup();
		var key = masterkeyRef().getAndSet(null);
		if (key != null) {
			key.destroy();
		}
	}

	default Stage showHealthCheckWindow() {
		Stage stage = window();
		stage.setScene(startScene().get());
		stage.setMinWidth(420);
		stage.setMinHeight(300);
		stage.show();
		return stage;
	}

	@Subcomponent.Builder
	interface Builder {

		@BindsInstance
		Builder vault(@HealthCheckWindow Vault vault);

		@BindsInstance
		Builder owner(@Named("healthCheckOwner") Stage owner);

		HealthCheckComponent build();
	}

}
