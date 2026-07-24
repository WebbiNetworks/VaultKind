package org.cryptomator.nativeui;

import org.cryptomator.common.vaults.Vault;
import org.cryptomator.common.vaults.VaultListSnapshotMapper;
import org.cryptomator.common.vaults.VaultSummary;

import javax.inject.Inject;
import javafx.application.Platform;
import javafx.collections.ObservableList;
import java.io.IOException;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

public class VaultListSnapshotProvider {

	private final ObservableList<Vault> vaults;
	private final VaultListSnapshotMapper mapper;

	@Inject
	public VaultListSnapshotProvider(ObservableList<Vault> vaults, VaultListSnapshotMapper mapper) {
		this.vaults = vaults;
		this.mapper = mapper;
	}

	public List<VaultSummary> get() throws IOException {
		if (Boolean.getBoolean("vaultkind.nativeBackend")) {
			return mapper.map(List.copyOf(vaults));
		}
		if (Platform.isFxApplicationThread()) {
			return mapper.map(vaults);
		}

		var result = new CompletableFuture<List<VaultSummary>>();
		try {
			Platform.runLater(() -> {
				try {
					result.complete(mapper.map(vaults));
				} catch (RuntimeException e) {
					result.completeExceptionally(e);
				}
			});
		} catch (IllegalStateException e) {
			// The native-backend process intentionally does not start the JavaFX toolkit.
			// Its configured vault list is fully constructed before the bridge begins serving.
			return mapper.map(List.copyOf(vaults));
		}
		try {
			return result.get(2, TimeUnit.SECONDS);
		} catch (InterruptedException e) {
			Thread.currentThread().interrupt();
			throw new IOException("Interrupted while reading the vault list", e);
		} catch (ExecutionException | TimeoutException e) {
			throw new IOException("Unable to read the vault list safely", e);
		}
	}
}
