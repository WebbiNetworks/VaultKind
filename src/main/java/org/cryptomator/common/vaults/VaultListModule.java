package org.cryptomator.common.vaults;

import dagger.Module;
import dagger.Provides;
import org.cryptomator.common.settings.Settings;

import javax.inject.Singleton;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import java.util.List;

@Module
public class VaultListModule {

	@Provides
	@Singleton
	public ObservableList<Vault> provideVaultList() {
		return FXCollections.observableArrayList(Vault::observables);
	}

	@Provides
	@Singleton
	public List<Vault> provideVaultListView(ObservableList<Vault> vaults) {
		return vaults;
	}

	@Provides
	@Singleton
	public VaultListPersistence provideVaultListPersistence(ObservableList<Vault> vaults, Settings settings) {
		return new VaultListPersistence() {
			@Override
			public void initialize() {
				vaults.addListener(new VaultListChangeListener(settings.directories));
			}

			@Override
			public void vaultAdded(Vault vault) {
				// The observable-list listener persists GUI mutations in list order.
			}

			@Override
			public void vaultRemoved(Vault vault) {
				// The observable-list listener persists GUI mutations in list order.
			}
		};
	}

}
