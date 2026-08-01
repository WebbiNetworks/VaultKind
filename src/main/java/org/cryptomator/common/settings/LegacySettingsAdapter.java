package org.cryptomator.common.settings;

import javax.inject.Inject;
import javax.inject.Singleton;
import java.util.List;

/**
 * Keeps JavaFX-backed legacy settings behind the neutral engine contract.
 */
@Singleton
public class LegacySettingsAdapter implements EngineSettings {

	private final Settings settings;

	@Inject
	public LegacySettingsAdapter(Settings settings) {
		this.settings = settings;
	}

	@Override
	public List<VaultSettings> configuredVaults() {
		return List.copyOf(settings.directories);
	}

	@Override
	public void addConfiguredVault(VaultSettings vaultSettings) {
		settings.directories.add(vaultSettings);
	}

	@Override
	public void removeConfiguredVault(VaultSettings vaultSettings) {
		settings.directories.remove(vaultSettings);
	}

	@Override
	public String selectedMountService() {
		return settings.mountService.get();
	}

	@Override
	public void selectMountService(String serviceId) {
		settings.mountService.set(serviceId);
	}
}
