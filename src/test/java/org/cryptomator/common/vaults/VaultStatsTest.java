package org.cryptomator.common.vaults;

import org.cryptomator.cryptofs.CryptoFileSystem;
import org.cryptomator.cryptofs.CryptoFileSystemStats;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;

import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class VaultStatsTest {

	@Test
	void unlockedVaultSamplesNeutralStatsAndStopsSchedulerWhenLocked() {
		var cryptoFileSystem = Mockito.mock(CryptoFileSystem.class);
		var cryptoStats = Mockito.mock(CryptoFileSystemStats.class);
		var scheduler = Mockito.mock(ScheduledExecutorService.class);
		var scheduledUpdate = Mockito.mock(ScheduledFuture.class);
		var state = new VaultState(VaultState.Value.LOCKED);
		when(cryptoFileSystem.getStats()).thenReturn(cryptoStats);
		when(cryptoStats.pollBytesRead()).thenReturn(12L);
		when(cryptoStats.pollBytesWritten()).thenReturn(8L);
		when(cryptoStats.pollBytesEncrypted()).thenReturn(6L);
		when(cryptoStats.pollBytesDecrypted()).thenReturn(10L);
		when(cryptoStats.pollChunkCacheAccesses()).thenReturn(4L);
		when(cryptoStats.pollChunkCacheHits()).thenReturn(3L);
		when(cryptoStats.pollAmountOfAccessesRead()).thenReturn(2L);
		when(cryptoStats.pollAmountOfAccessesWritten()).thenReturn(1L);
		when(cryptoStats.pollAmountOfAccesses()).thenReturn(3L);
		when(cryptoStats.pollTotalAmountOfAccesses()).thenReturn(9L);
		when(scheduler.scheduleAtFixedRate(any(Runnable.class), eq(1L), eq(1L), eq(TimeUnit.SECONDS))).thenReturn(scheduledUpdate);
		var inTest = new VaultStats(new AtomicReference<>(cryptoFileSystem), state, scheduler);

		state.transition(VaultState.Value.LOCKED, VaultState.Value.UNLOCKED);

		var snapshot = inTest.snapshot();
		assertEquals(12L, snapshot.bytesPerSecondRead());
		assertEquals(8L, snapshot.bytesPerSecondWritten());
		assertEquals(0.75, snapshot.cacheHitRate());
		assertEquals(3L, snapshot.filesAccessed());
		assertNotNull(snapshot.lastActivity());
		verify(scheduler).scheduleAtFixedRate(any(Runnable.class), eq(1L), eq(1L), eq(TimeUnit.SECONDS));

		state.transition(VaultState.Value.UNLOCKED, VaultState.Value.LOCKED);
		verify(scheduledUpdate).cancel(false);
	}

	@Test
	void nativeSnapshotIsEmptyWithoutMountedFileSystem() {
		var inTest = new VaultStats(new AtomicReference<>(), new VaultState(VaultState.Value.LOCKED), Mockito.mock(ScheduledExecutorService.class));

		assertEquals(new VaultStats.NativeSnapshot(0, 0, 0, 0, 0.0, 0, 0, 0, 0, 0), inTest.nativeSnapshot());
	}
}
