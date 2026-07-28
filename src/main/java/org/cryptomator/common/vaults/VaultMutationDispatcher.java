package org.cryptomator.common.vaults;

/**
 * Dispatches mutations that may require a UI-owned thread in the legacy app.
 */
@FunctionalInterface
public interface VaultMutationDispatcher {

	void dispatch(Runnable mutation);

}
