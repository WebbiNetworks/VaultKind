package org.cryptomator.nativeui;

import org.cryptomator.common.vaults.VaultListManager;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;

public class NativeBackendApplication {

	private static final Logger LOG = LoggerFactory.getLogger(NativeBackendApplication.class);
	private final NativeUiBridge bridge;
	@SuppressWarnings("unused")
	private final VaultListManager vaultListManager;
	private final NativeBackendTerminator terminator;

	@Inject
	public NativeBackendApplication(NativeUiBridge bridge, VaultListManager vaultListManager, NativeBackendTerminator terminator) {
		this.bridge = bridge;
		this.vaultListManager = vaultListManager; // Construction loads the authoritative configured-vault list.
		this.terminator = terminator;
	}

	public int run() {
		LOG.info("Starting VaultKind native backend");
		bridge.start();
		try {
			terminator.awaitShutdown();
			bridge.close();
			return 0;
		} catch (InterruptedException e) {
			Thread.currentThread().interrupt();
			return 0;
		}
	}
}
