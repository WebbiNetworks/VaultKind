package org.cryptomator.common.vaults;

import org.cryptomator.common.Nullable;

import javax.inject.Inject;
import javax.inject.Named;
import java.util.Objects;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.atomic.AtomicReference;

/** UI-neutral storage for the most recent vault error. */
@PerVault
public class VaultExceptionState {

	@FunctionalInterface
	public interface Listener {
		void changed(Exception value);
	}

	private final AtomicReference<Exception> value;
	private final CopyOnWriteArrayList<Listener> listeners = new CopyOnWriteArrayList<>();

	@Inject
	VaultExceptionState(@Named("lastKnownException") @Nullable Exception initialValue) {
		this.value = new AtomicReference<>(initialValue);
	}

	public Exception get() {
		return value.get();
	}

	public void set(Exception newValue) {
		var previous = value.getAndSet(newValue);
		if (!Objects.equals(previous, newValue)) {
			listeners.forEach(listener -> listener.changed(newValue));
		}
	}

	public void addListener(Listener listener) {
		listeners.add(Objects.requireNonNull(listener));
	}
}
