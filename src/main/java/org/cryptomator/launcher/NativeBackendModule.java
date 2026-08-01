package org.cryptomator.launcher;

import dagger.Module;
import dagger.Provides;
import org.cryptomator.common.settings.EngineSettings;
import org.cryptomator.common.vaults.Vault;
import org.cryptomator.common.vaults.VaultListPersistence;
import org.cryptomator.common.vaults.VaultMutationDispatcher;

import javax.inject.Singleton;
import java.util.List;
import java.util.ResourceBundle;
import java.util.concurrent.CopyOnWriteArrayList;

@Module
class NativeBackendModule {

	@Provides
	@Singleton
	static VaultMutationDispatcher provideVaultMutationDispatcher() {
		return Runnable::run;
	}

	@Provides
	@Singleton
	static List<Vault> provideVaultList() {
		return new CopyOnWriteArrayList<>();
	}

	@Provides
	@Singleton
	static VaultListPersistence provideVaultListPersistence(EngineSettings settings) {
		return new VaultListPersistence() {
			@Override
			public void initialize() {
				// Initial vault settings are loaded before persistence begins.
			}

			@Override
			public void vaultAdded(Vault vault) {
				settings.addConfiguredVault(vault.getVaultSettings());
			}

			@Override
			public void vaultRemoved(Vault vault) {
				settings.removeConfiguredVault(vault.getVaultSettings());
			}
		};
	}

	@Provides
	@Singleton
	static ResourceBundle provideLocalization() {
		return ResourceBundle.getBundle("i18n.strings");
	}

}
