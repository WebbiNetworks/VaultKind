package org.cryptomator.common.vaults;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class VaultListSnapshotMapperTest {

	private VaultListSnapshotMapper mapper;

	@BeforeEach
	void setUp() {
		mapper = new VaultListSnapshotMapper();
	}

	@Test
	void mapsOnlySafeSummaryFields() {
		Vault vault = Mockito.mock(Vault.class);
		when(vault.getId()).thenReturn("vault-1");
		when(vault.getDisplayName()).thenReturn("Personal");
		when(vault.getState()).thenReturn(VaultState.Value.LOCKED);
		when(vault.getDisplayablePath()).thenReturn("F:\\Vaults\\Personal");

		var result = mapper.map(List.of(vault));

		assertEquals(List.of(new VaultSummary("vault-1", "Personal", "locked", "F:\\Vaults\\Personal", null)), result);
		verify(vault).getId();
		verify(vault).getDisplayName();
		verify(vault).getState();
		verify(vault).getDisplayablePath();
		verify(vault).getMountPoint();
	}

	@Test
	void mapsEveryLifecycleStateUsingStableWireNames() {
		for (VaultState.Value state : VaultState.Value.values()) {
			Vault vault = Mockito.mock(Vault.class);
			when(vault.getId()).thenReturn("vault-1");
			when(vault.getDisplayName()).thenReturn("Personal");
			when(vault.getState()).thenReturn(state);
			when(vault.getDisplayablePath()).thenReturn("F:\\Vaults\\Personal");

			var result = mapper.map(List.of(vault));

			assertEquals(state.name().toLowerCase(), result.getFirst().state());
		}
	}
}
