package org.cryptomator.common.vaults;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

public class VaultStateTest {

	@Test
	public void transitionChangesExpectedStateAndNotifiesListener() {
		var state = new VaultState(VaultState.Value.LOCKED);
		var changes = new ArrayList<List<VaultState.Value>>();
		state.addListener((oldState, newState) -> changes.add(List.of(oldState, newState)));

		Assertions.assertTrue(state.transition(VaultState.Value.LOCKED, VaultState.Value.PROCESSING));
		Assertions.assertEquals(VaultState.Value.PROCESSING, state.get());
		Assertions.assertEquals(List.of(List.of(VaultState.Value.LOCKED, VaultState.Value.PROCESSING)), changes);
	}

	@Test
	public void failedTransitionDoesNotNotifyListener() {
		var state = new VaultState(VaultState.Value.LOCKED);
		var notification = new CountDownLatch(1);
		state.addListener((oldState, newState) -> notification.countDown());

		Assertions.assertFalse(state.transition(VaultState.Value.UNLOCKED, VaultState.Value.PROCESSING));
		Assertions.assertEquals(VaultState.Value.LOCKED, state.get());
		Assertions.assertEquals(1L, notification.getCount());
	}

	@Test
	public void removedListenerIsNotNotified() {
		var state = new VaultState(VaultState.Value.LOCKED);
		var notification = new CountDownLatch(1);
		VaultState.Listener listener = (oldState, newState) -> notification.countDown();
		state.addListener(listener);
		state.removeListener(listener);

		state.set(VaultState.Value.UNLOCKED);
		Assertions.assertEquals(1L, notification.getCount());
	}

	@Test
	public void awaitStateIsReleasedByTransition() throws Exception {
		var state = new VaultState(VaultState.Value.LOCKED);
		var started = new CountDownLatch(1);
		var reached = new CountDownLatch(1);
		var waiter = Thread.startVirtualThread(() -> {
			started.countDown();
			try {
				if (state.awaitState(VaultState.Value.UNLOCKED, 1, TimeUnit.SECONDS)) {
					reached.countDown();
				}
			} catch (InterruptedException e) {
				Thread.currentThread().interrupt();
			}
		});

		Assertions.assertTrue(started.await(1, TimeUnit.SECONDS));
		state.transition(VaultState.Value.LOCKED, VaultState.Value.UNLOCKED);
		Assertions.assertTrue(reached.await(1, TimeUnit.SECONDS));
		waiter.join();
	}

}
