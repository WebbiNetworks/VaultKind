package org.cryptomator.common.vaults;

import javafx.beans.property.DoubleProperty;
import javafx.beans.property.LongProperty;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleDoubleProperty;
import javafx.beans.property.SimpleLongProperty;
import javafx.beans.property.SimpleObjectProperty;

import javax.inject.Inject;
import java.time.Instant;

/** JavaFX property facade retained for the inherited statistics screen. */
@PerVault
public class LegacyVaultStatsObservable {

	private final LongProperty bytesPerSecondRead = new SimpleLongProperty();
	private final LongProperty bytesPerSecondWritten = new SimpleLongProperty();
	private final LongProperty bytesPerSecondEncrypted = new SimpleLongProperty();
	private final LongProperty bytesPerSecondDecrypted = new SimpleLongProperty();
	private final DoubleProperty cacheHitRate = new SimpleDoubleProperty();
	private final LongProperty totalBytesRead = new SimpleLongProperty();
	private final LongProperty totalBytesWritten = new SimpleLongProperty();
	private final LongProperty totalBytesEncrypted = new SimpleLongProperty();
	private final LongProperty totalBytesDecrypted = new SimpleLongProperty();
	private final LongProperty filesRead = new SimpleLongProperty();
	private final LongProperty filesWritten = new SimpleLongProperty();
	private final LongProperty filesAccessed = new SimpleLongProperty();
	private final LongProperty totalFilesAccessed = new SimpleLongProperty();
	private final ObjectProperty<Instant> lastActivity = new SimpleObjectProperty<>();

	@Inject
	LegacyVaultStatsObservable(VaultStats stats, VaultMutationDispatcher dispatcher) {
		refresh(stats.snapshot());
		stats.addListener(snapshot -> dispatcher.dispatch(() -> refresh(snapshot)));
	}

	private void refresh(VaultStats.Snapshot snapshot) {
		bytesPerSecondRead.set(snapshot.bytesPerSecondRead());
		bytesPerSecondWritten.set(snapshot.bytesPerSecondWritten());
		bytesPerSecondEncrypted.set(snapshot.bytesPerSecondEncrypted());
		bytesPerSecondDecrypted.set(snapshot.bytesPerSecondDecrypted());
		cacheHitRate.set(snapshot.cacheHitRate());
		totalBytesRead.set(snapshot.totalBytesRead());
		totalBytesWritten.set(snapshot.totalBytesWritten());
		totalBytesEncrypted.set(snapshot.totalBytesEncrypted());
		totalBytesDecrypted.set(snapshot.totalBytesDecrypted());
		filesRead.set(snapshot.filesRead());
		filesWritten.set(snapshot.filesWritten());
		filesAccessed.set(snapshot.filesAccessed());
		totalFilesAccessed.set(snapshot.totalFilesAccessed());
		lastActivity.set(snapshot.lastActivity());
	}

	public LongProperty bytesPerSecondReadProperty() { return bytesPerSecondRead; }
	public LongProperty bytesPerSecondWrittenProperty() { return bytesPerSecondWritten; }
	public LongProperty bytesPerSecondEncryptedProperty() { return bytesPerSecondEncrypted; }
	public LongProperty bytesPerSecondDecryptedProperty() { return bytesPerSecondDecrypted; }
	public DoubleProperty cacheHitRateProperty() { return cacheHitRate; }
	public LongProperty totalBytesReadProperty() { return totalBytesRead; }
	public LongProperty totalBytesWrittenProperty() { return totalBytesWritten; }
	public LongProperty totalBytesEncryptedProperty() { return totalBytesEncrypted; }
	public LongProperty totalBytesDecryptedProperty() { return totalBytesDecrypted; }
	public LongProperty filesReadProperty() { return filesRead; }
	public LongProperty filesWrittenProperty() { return filesWritten; }
	public LongProperty filesAccessedProperty() { return filesAccessed; }
	public LongProperty totalFilesAccessedProperty() { return totalFilesAccessed; }
	public ObjectProperty<Instant> lastActivityProperty() { return lastActivity; }
}
