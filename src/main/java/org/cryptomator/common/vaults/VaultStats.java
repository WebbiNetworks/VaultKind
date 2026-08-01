package org.cryptomator.common.vaults;

import org.cryptomator.cryptofs.CryptoFileSystem;
import org.cryptomator.cryptofs.CryptoFileSystemStats;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import java.time.Instant;
import java.util.Objects;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/** UI-neutral sampling and snapshot service for one unlocked vault. */
@PerVault
public class VaultStats {

	private static final Logger LOG = LoggerFactory.getLogger(VaultStats.class);

	@FunctionalInterface
	public interface Listener {
		void changed(Snapshot snapshot);
	}

	private final AtomicReference<CryptoFileSystem> fs;
	private final ScheduledExecutorService scheduler;
	private final AtomicReference<Snapshot> current = new AtomicReference<>(Snapshot.empty());
	private final AtomicReference<ScheduledFuture<?>> updater = new AtomicReference<>();
	private final CopyOnWriteArrayList<Listener> listeners = new CopyOnWriteArrayList<>();

	@Inject
	VaultStats(AtomicReference<CryptoFileSystem> fs, VaultState state, ScheduledExecutorService scheduler) {
		this.fs = fs;
		this.scheduler = scheduler;
		state.addListener(this::vaultStateChanged);
	}

	private void vaultStateChanged(VaultState.Value oldState, VaultState.Value newState) {
		if (newState == VaultState.Value.UNLOCKED) {
			start();
		} else {
			stop();
		}
	}

	private void start() {
		LOG.debug("start recording stats");
		stop();
		current.updateAndGet(snapshot -> snapshot.withLastActivity(Instant.now()));
		updateSafely();
		updater.set(scheduler.scheduleAtFixedRate(this::updateSafely, 1, 1, TimeUnit.SECONDS));
	}

	private void stop() {
		var previous = updater.getAndSet(null);
		if (previous != null) {
			LOG.debug("stop recording stats");
			previous.cancel(false);
		}
	}

	private void updateSafely() {
		try {
			updateSnapshot();
		} catch (RuntimeException e) {
			LOG.error("Error while updating vault statistics.", e);
		}
	}

	synchronized Snapshot updateSnapshot() {
		var fileSystem = fs.get();
		var next = fileSystem == null ? Snapshot.empty() : sample(fileSystem.getStats(), current.get().lastActivity());
		current.set(next);
		listeners.forEach(listener -> listener.changed(next));
		return next;
	}

	private Snapshot sample(CryptoFileSystemStats stats, Instant previousActivity) {
		long cacheAccesses = stats.pollChunkCacheAccesses();
		long cacheHits = stats.pollChunkCacheHits();
		long filesRead = stats.pollAmountOfAccessesRead();
		long filesWritten = stats.pollAmountOfAccessesWritten();
		var lastActivity = filesRead + filesWritten > 0 ? Instant.now() : previousActivity;
		return new Snapshot(
				stats.pollBytesRead(),
				stats.pollBytesWritten(),
				stats.pollBytesEncrypted(),
				stats.pollBytesDecrypted(),
				cacheAccesses == 0 ? 0.0 : cacheHits / (double) cacheAccesses,
				stats.pollTotalBytesRead(),
				stats.pollTotalBytesWritten(),
				stats.pollTotalBytesEncrypted(),
				stats.pollTotalBytesDecrypted(),
				filesRead,
				filesWritten,
				stats.pollAmountOfAccesses(),
				stats.pollTotalAmountOfAccesses(),
				lastActivity);
	}

	public void addListener(Listener listener) {
		listeners.add(Objects.requireNonNull(listener));
	}

	public Snapshot snapshot() {
		return current.get();
	}

	/** Returns the latest transport-safe sample for the native Windows frontend. */
	public NativeSnapshot nativeSnapshot() {
		var snapshot = current.get();
		return new NativeSnapshot(
				snapshot.bytesPerSecondRead(),
				snapshot.bytesPerSecondWritten(),
				snapshot.bytesPerSecondDecrypted(),
				snapshot.bytesPerSecondEncrypted(),
				snapshot.cacheHitRate(),
				snapshot.totalBytesRead(),
				snapshot.totalBytesWritten(),
				snapshot.totalBytesDecrypted(),
				snapshot.totalBytesEncrypted(),
				snapshot.totalFilesAccessed());
	}

	public Instant getLastActivity() {
		return current.get().lastActivity();
	}

	public record Snapshot(long bytesPerSecondRead, long bytesPerSecondWritten, long bytesPerSecondEncrypted, long bytesPerSecondDecrypted, double cacheHitRate, long totalBytesRead, long totalBytesWritten, long totalBytesEncrypted, long totalBytesDecrypted, long filesRead, long filesWritten, long filesAccessed, long totalFilesAccessed, Instant lastActivity) {
		private static Snapshot empty() {
			return new Snapshot(0, 0, 0, 0, 0.0, 0, 0, 0, 0, 0, 0, 0, 0, null);
		}

		private Snapshot withLastActivity(Instant value) {
			return new Snapshot(bytesPerSecondRead, bytesPerSecondWritten, bytesPerSecondEncrypted, bytesPerSecondDecrypted, cacheHitRate, totalBytesRead, totalBytesWritten, totalBytesEncrypted, totalBytesDecrypted, filesRead, filesWritten, filesAccessed, totalFilesAccessed, value);
		}
	}

	public record NativeSnapshot(long bytesPerSecondRead, long bytesPerSecondWritten, long bytesPerSecondDecrypted, long bytesPerSecondEncrypted, double cacheHitRate, long totalBytesRead, long totalBytesWritten, long totalBytesDecrypted, long totalBytesEncrypted, long totalFilesAccessed) {
	}
}
