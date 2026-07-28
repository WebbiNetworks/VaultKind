package org.cryptomator.common.mount;

import org.cryptomator.common.settings.EngineSettings;
import org.cryptomator.common.settings.VaultSettings;
import org.cryptomator.integrations.mount.MountService;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.Mockito.when;

class MountServiceSelectorTest {

	private final EngineSettings settings = Mockito.mock(EngineSettings.class);
	private final MountService service = Mockito.mock(MountService.class);

	@Test
	void usesConfiguredDefaultWhenAvailable() {
		when(settings.selectedMountService()).thenReturn(service.getClass().getName());
		var selector = new MountServiceSelector(List.of(service), settings);

		assertSame(service, selector.defaultMountService());
	}

	@Test
	void unknownDefaultFallsBackToFirstAvailableService() {
		when(settings.selectedMountService()).thenReturn("missing.Provider");
		var selector = new MountServiceSelector(List.of(service), settings);

		assertSame(service, selector.defaultMountService());
	}

	@Test
	void perVaultChoiceOverridesGlobalDefault() {
		when(settings.selectedMountService()).thenReturn("missing.Provider");
		var vaultSettings = VaultSettings.withRandomId();
		vaultSettings.setMountService(service.getClass().getName());
		var selector = new MountServiceSelector(List.of(service), settings);

		assertSame(service, selector.forVault(vaultSettings));
	}

	@Test
	void requiresAtLeastOneMountService() {
		assertThrows(IllegalStateException.class, () -> new MountServiceSelector(List.of(), settings));
	}
}
