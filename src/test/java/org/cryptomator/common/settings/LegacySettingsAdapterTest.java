package org.cryptomator.common.settings;

import org.cryptomator.common.Environment;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;

public class LegacySettingsAdapterTest {

	private Settings settings;
	private LegacySettingsAdapter inTest;

	@BeforeEach
	public void setup() {
		settings = Settings.create(Mockito.mock(SettingsProvider.class), Mockito.mock(Environment.class));
		inTest = new LegacySettingsAdapter(settings);
	}

	@Test
	public void configuredVaultAccessUsesValueOrientedSnapshot() {
		var vaultSettings = VaultSettings.withRandomId();

		inTest.addConfiguredVault(vaultSettings);
		var snapshot = inTest.configuredVaults();

		Assertions.assertEquals(1, snapshot.size());
		Assertions.assertSame(vaultSettings, snapshot.getFirst());
		Assertions.assertThrows(UnsupportedOperationException.class, () -> snapshot.add(VaultSettings.withRandomId()));

		inTest.removeConfiguredVault(vaultSettings);
		Assertions.assertTrue(inTest.configuredVaults().isEmpty());
	}

	@Test
	public void mountServiceAccessUsesNullablePlainValue() {
		Assertions.assertNull(inTest.selectedMountService());

		inTest.selectMountService("example.MountService");
		Assertions.assertEquals("example.MountService", inTest.selectedMountService());

		inTest.selectMountService(null);
		Assertions.assertNull(inTest.selectedMountService());
	}
}
