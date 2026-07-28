package org.cryptomator.common.vaults;

import org.cryptomator.integrations.mount.UnmountFailedException;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javax.inject.Singleton;
import java.io.IOException;
import java.time.Instant;
import java.util.List;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

@Singleton
public class AutoLocker {

	private static final Logger LOG = LoggerFactory.getLogger(AutoLocker.class);

	private final ScheduledExecutorService scheduler;
	private final List<Vault> vaultList;
	private final VaultMutationDispatcher mutationDispatcher;

	@Inject
	public AutoLocker(ScheduledExecutorService scheduler, List<Vault> vaultList, VaultMutationDispatcher mutationDispatcher) {
		this.scheduler = scheduler;
		this.vaultList = vaultList;
		this.mutationDispatcher = mutationDispatcher;
	}

	public void init() {
		scheduler.scheduleAtFixedRate(this::tick, 0, 1, TimeUnit.MINUTES);
	}

	private void tick() {
		vaultList.stream() // all vaults
				.filter(Vault::isUnlocked) // unlocked vaults
				.filter(this::exceedsIdleTime) // idle vaults
				.forEach(this::autolock);
	}

	private void autolock(Vault vault) {
		try {
			vault.lock(false);
			mutationDispatcher.dispatch(() -> vault.setState(VaultState.Value.LOCKED));
			LOG.info("Autolocked {} after idle timeout", vault.getDisplayName());
		} catch (UnmountFailedException | IOException e) {
			LOG.error("Autolocking failed.", e);
		}
	}

	private boolean exceedsIdleTime(Vault vault) {
		assert vault.isUnlocked();
		if (vault.isAutoLockWhenIdle()) {
			int maxIdleSeconds = vault.getAutoLockIdleSeconds();
			var deadline = vault.getStats().getLastActivity().plusSeconds(maxIdleSeconds);
			return deadline.isBefore(Instant.now());
		} else {
			return false;
		}
	}


}
