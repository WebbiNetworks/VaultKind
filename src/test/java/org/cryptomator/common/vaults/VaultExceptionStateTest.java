package org.cryptomator.common.vaults;

import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;

class VaultExceptionStateTest {

	@Test
	void neutralAndLegacyChangesStaySynchronizedThroughDispatcher() {
		var initial = new IOException("initial");
		var state = new VaultExceptionState(initial);
		var pending = new AtomicReference<Runnable>();
		var legacy = new LegacyVaultExceptionProperty(state, pending::set);

		assertSame(initial, state.get());
		assertSame(initial, legacy.property().get());

		var updated = new IOException("updated");
		state.set(updated);
		assertSame(initial, legacy.property().get());
		pending.get().run();
		assertSame(updated, legacy.property().get());

		legacy.property().set(null);
		assertNull(state.get());
	}

	@Test
	void unchangedValueDoesNotNotifyListeners() {
		var error = new IOException("same");
		var state = new VaultExceptionState(error);
		var changes = new int[1];
		state.addListener(value -> changes[0]++);

		state.set(error);
		assertEquals(0, changes[0]);
	}
}
