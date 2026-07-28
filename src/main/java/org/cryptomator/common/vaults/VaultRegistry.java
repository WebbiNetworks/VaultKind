package org.cryptomator.common.vaults;

import java.util.List;
import java.util.Optional;

/**
 * Neutral access to the configured vault collection.
 *
 * Implementations own any UI-specific collection details. Callers receive stable
 * snapshots so the native backend never depends on JavaFX collection types.
 */
public interface VaultRegistry {

	List<Vault> snapshot();

	Optional<Vault> findById(String vaultId);

	boolean remove(Vault vault);

}
