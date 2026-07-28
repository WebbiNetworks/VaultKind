package org.cryptomator.common.vaults;

import javafx.beans.value.ObservableObjectValue;
import javafx.beans.value.ObservableValueBase;

import javax.inject.Inject;

/**
 * JavaFX compatibility adapter for the inherited GUI.
 *
 * The underlying state machine is UI-neutral. Event delivery is delegated to
 * the frontend-specific mutation dispatcher, which selects the JavaFX
 * application thread for the legacy GUI and direct delivery for the native
 * backend.
 */
@PerVault
public class LegacyVaultStateObservable extends ObservableValueBase<VaultState.Value> implements ObservableObjectValue<VaultState.Value> {

	private final VaultState state;

	@Inject
	public LegacyVaultStateObservable(VaultState state, VaultMutationDispatcher dispatcher) {
		this.state = state;
		state.addListener((oldState, newState) -> dispatcher.dispatch(this::fireValueChangedEvent));
	}

	@Override
	public VaultState.Value get() {
		return state.get();
	}

	@Override
	public VaultState.Value getValue() {
		return state.get();
	}

}
