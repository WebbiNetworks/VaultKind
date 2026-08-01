package org.cryptomator.common.vaults;

import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleObjectProperty;

import javax.inject.Inject;

/** JavaFX compatibility facade for the inherited GUI's vault error property. */
@PerVault
public class LegacyVaultExceptionProperty {

	private final VaultExceptionState state;
	private final ObjectProperty<Exception> property;

	@Inject
	LegacyVaultExceptionProperty(VaultExceptionState state, VaultMutationDispatcher dispatcher) {
		this.state = state;
		this.property = new SimpleObjectProperty<>(state.get());
		property.addListener((_, _, value) -> state.set(value));
		state.addListener(value -> dispatcher.dispatch(() -> property.set(value)));
	}

	public ObjectProperty<Exception> property() {
		return property;
	}
}
