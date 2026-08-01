package org.cryptomator.launcher;

import dagger.BindsInstance;
import dagger.Component;
import org.cryptomator.common.CommonsModule;
import org.cryptomator.common.vaults.VaultListModule;
import org.cryptomator.ui.fxapp.FxApplicationComponent;

import javax.inject.Named;
import javax.inject.Singleton;

@Singleton
@Component(modules = {CryptomatorModule.class, CommonsModule.class, VaultListModule.class})
public interface CryptomatorComponent {

	Cryptomator application();

	FxApplicationComponent.Builder fxAppComponentBuilder();

	@Component.Factory
	interface Factory {
		CryptomatorComponent create(@BindsInstance @Named("startupTime") long startupTime);
	}

}
