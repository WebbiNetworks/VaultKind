package org.cryptomator.common.settings;

import org.junit.jupiter.api.Test;

import java.nio.file.Path;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class VaultSettingsDataTest {

	@Test
	void plainChangesReachLegacyPropertiesAndSerialization() {
		var settings = VaultSettings.withRandomId();
		settings.setPath(Path.of("updated"));
		settings.setDisplayName("Updated vault");
		settings.setUsesReadOnlyMode(true);
		settings.setMountService("provider");

		assertEquals(Path.of("updated"), settings.path.get());
		assertEquals("Updated vault", settings.displayName.get());
		assertTrue(settings.usesReadOnlyMode.get());
		assertEquals("provider", settings.mountService.get());
		assertEquals("updated", settings.serialized().path);
		assertEquals("Updated vault", settings.serialized().displayName);
	}

	@Test
	void legacyPropertyChangesReachPlainValuesAndSerialization() {
		var settings = VaultSettings.withRandomId();
		settings.path.set(Path.of("legacy"));
		settings.displayName.set("Legacy vault");
		settings.autoLockWhenIdle.set(true);
		settings.mountService.set(null);

		assertEquals(Path.of("legacy"), settings.path());
		assertEquals("Legacy vault", settings.displayName());
		assertTrue(settings.autoLockWhenIdle());
		assertNull(settings.mountService());
		assertEquals("legacy", settings.serialized().path);
		assertTrue(settings.serialized().autoLockWhenIdle);
	}

	@Test
	void unchangedValuesDoNotEmitDuplicateLegacyInvalidations() {
		var settings = VaultSettings.withRandomId();
		var changes = new int[1];
		settings.usesReadOnlyMode.addListener((_, _, _) -> changes[0]++);

		settings.setUsesReadOnlyMode(false);
		assertEquals(0, changes[0]);
		settings.setUsesReadOnlyMode(true);
		assertEquals(1, changes[0]);
		assertFalse(settings.serialized().unlockAfterStartup);
	}
}
