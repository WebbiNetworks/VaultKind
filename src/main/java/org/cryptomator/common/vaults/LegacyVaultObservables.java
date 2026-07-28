package org.cryptomator.common.vaults;

import javafx.beans.binding.Bindings;
import javafx.beans.binding.BooleanBinding;
import javafx.beans.binding.ObjectBinding;
import org.cryptomator.integrations.mount.Mountpoint;

import java.util.function.Supplier;

/** State-derived JavaFX bindings retained solely for the inherited GUI. */
final class LegacyVaultObservables {

	private final BooleanBinding locked;
	private final BooleanBinding processing;
	private final BooleanBinding unlocked;
	private final BooleanBinding missing;
	private final BooleanBinding needsMigration;
	private final BooleanBinding unknownError;
	private final BooleanBinding missingVaultConfig;
	private final ObjectBinding<Mountpoint> mountPoint;

	LegacyVaultObservables(LegacyVaultStateObservable state, Supplier<Mountpoint> mountPointSupplier) {
		locked = stateBinding(state, VaultState.Value.LOCKED);
		processing = stateBinding(state, VaultState.Value.PROCESSING);
		unlocked = stateBinding(state, VaultState.Value.UNLOCKED);
		missing = stateBinding(state, VaultState.Value.MISSING);
		needsMigration = stateBinding(state, VaultState.Value.NEEDS_MIGRATION);
		unknownError = stateBinding(state, VaultState.Value.ERROR);
		missingVaultConfig = Bindings.createBooleanBinding(() -> state.get() == VaultState.Value.VAULT_CONFIG_MISSING || state.get() == VaultState.Value.ALL_MISSING, state);
		mountPoint = Bindings.createObjectBinding(mountPointSupplier::get, state);
	}

	private static BooleanBinding stateBinding(LegacyVaultStateObservable state, VaultState.Value expected) {
		return Bindings.createBooleanBinding(() -> state.get() == expected, state);
	}

	BooleanBinding locked() { return locked; }
	BooleanBinding processing() { return processing; }
	BooleanBinding unlocked() { return unlocked; }
	BooleanBinding missing() { return missing; }
	BooleanBinding needsMigration() { return needsMigration; }
	BooleanBinding unknownError() { return unknownError; }
	BooleanBinding missingVaultConfig() { return missingVaultConfig; }
	ObjectBinding<Mountpoint> mountPoint() { return mountPoint; }
}
