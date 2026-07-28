package org.cryptomator.common.vaults;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class LegacyVaultObservablesTest {

	@Test
	void derivedBindingsFollowNeutralState() {
		var state = new VaultState(VaultState.Value.LOCKED);
		var observableState = new LegacyVaultStateObservable(state, Runnable::run);
		var observables = new LegacyVaultObservables(observableState, () -> null);

		assertTrue(observables.locked().get());
		assertFalse(observables.unlocked().get());
		state.transition(VaultState.Value.LOCKED, VaultState.Value.UNLOCKED);
		assertFalse(observables.locked().get());
		assertTrue(observables.unlocked().get());
	}

	@Test
	void missingConfigBindingCoversBothMissingStates() {
		var state = new VaultState(VaultState.Value.VAULT_CONFIG_MISSING);
		var observableState = new LegacyVaultStateObservable(state, Runnable::run);
		var observables = new LegacyVaultObservables(observableState, () -> null);

		assertTrue(observables.missingVaultConfig().get());
		state.set(VaultState.Value.ALL_MISSING);
		assertTrue(observables.missingVaultConfig().get());
		state.set(VaultState.Value.MISSING);
		assertFalse(observables.missingVaultConfig().get());
	}
}
