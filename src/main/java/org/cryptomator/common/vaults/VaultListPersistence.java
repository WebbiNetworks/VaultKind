package org.cryptomator.common.vaults;

/**
 * Keeps configured-vault mutations synchronized with persistent settings.
 */
public interface VaultListPersistence {

	void initialize();

	void vaultAdded(Vault vault);

	void vaultRemoved(Vault vault);

}
