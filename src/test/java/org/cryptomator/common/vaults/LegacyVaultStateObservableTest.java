package org.cryptomator.common.vaults;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.util.concurrent.atomic.AtomicReference;

public class LegacyVaultStateObservableTest {

	@Test
	public void observableEventUsesFrontendDispatcher() {
		var state = new VaultState(VaultState.Value.LOCKED);
		var pendingEvent = new AtomicReference<Runnable>();
		var observedState = new AtomicReference<VaultState.Value>();
		var observable = new LegacyVaultStateObservable(state, pendingEvent::set);
		observable.addListener((ignored, oldState, newState) -> observedState.set(newState));

		state.transition(VaultState.Value.LOCKED, VaultState.Value.PROCESSING);

		Assertions.assertNull(observedState.get());
		Assertions.assertNotNull(pendingEvent.get());
		pendingEvent.get().run();
		Assertions.assertEquals(VaultState.Value.PROCESSING, observedState.get());
	}

}
