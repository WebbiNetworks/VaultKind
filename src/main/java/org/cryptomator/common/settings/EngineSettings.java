package org.cryptomator.common.settings;

import java.util.List;

/**
 * Value-oriented settings access used by the headless vault engine.
 */
public interface EngineSettings {

	List<VaultSettings> configuredVaults();

	void addConfiguredVault(VaultSettings vaultSettings);

	void removeConfiguredVault(VaultSettings vaultSettings);

	String selectedMountService();

	void selectMountService(String serviceId);
}
