package org.cryptomator.common.settings;

import javafx.beans.Observable;
import javafx.beans.binding.Bindings;
import javafx.beans.binding.StringExpression;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.IntegerProperty;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleIntegerProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.StringProperty;

import java.nio.file.Path;

/** JavaFX property facade retained for the inherited GUI. */
final class LegacyVaultSettingsProperties {

	final ObjectProperty<Path> path;
	final StringProperty displayName;
	final BooleanProperty unlockAfterStartup;
	final BooleanProperty revealAfterMount;
	final BooleanProperty usesReadOnlyMode;
	final StringProperty mountFlags;
	final IntegerProperty maxCleartextFilenameLength;
	final ObjectProperty<WhenUnlocked> actionAfterUnlock;
	final BooleanProperty autoLockWhenIdle;
	final IntegerProperty autoLockIdleSeconds;
	final ObjectProperty<Path> mountPoint;
	final StringExpression mountName;
	final StringProperty mountService;
	final IntegerProperty port;
	final StringProperty lastKnownKeyLoader;

	LegacyVaultSettingsProperties(VaultSettings owner, VaultSettingsData data) {
		path = new SimpleObjectProperty<>(owner, "path", data.path());
		displayName = new SimpleStringProperty(owner, "displayName", data.displayName());
		unlockAfterStartup = new SimpleBooleanProperty(owner, "unlockAfterStartup", data.unlockAfterStartup());
		revealAfterMount = new SimpleBooleanProperty(owner, "revealAfterMount", data.revealAfterMount());
		usesReadOnlyMode = new SimpleBooleanProperty(owner, "usesReadOnlyMode", data.usesReadOnlyMode());
		mountFlags = new SimpleStringProperty(owner, "mountFlags", data.mountFlags());
		maxCleartextFilenameLength = new SimpleIntegerProperty(owner, "maxCleartextFilenameLength", data.maxCleartextFilenameLength());
		actionAfterUnlock = new SimpleObjectProperty<>(owner, "actionAfterUnlock", data.actionAfterUnlock());
		autoLockWhenIdle = new SimpleBooleanProperty(owner, "autoLockWhenIdle", data.autoLockWhenIdle());
		autoLockIdleSeconds = new SimpleIntegerProperty(owner, "autoLockIdleSeconds", data.autoLockIdleSeconds());
		mountPoint = new SimpleObjectProperty<>(owner, "mountPoint", data.mountPoint());
		mountService = new SimpleStringProperty(owner, "mountService", data.mountService());
		port = new SimpleIntegerProperty(owner, "port", data.port());
		lastKnownKeyLoader = new SimpleStringProperty(owner, "lastKnownKeyLoader", data.lastKnownKeyLoader());
		mountName = StringExpression.stringExpression(Bindings.createStringBinding(() -> {
			var name = displayName.isEmpty().get() ? path.get().getFileName().toString() : displayName.get();
			return VaultSettings.normalizeDisplayName(name);
		}, displayName, path));

		path.addListener((_, _, value) -> data.setPath(value));
		displayName.addListener((_, _, value) -> data.setDisplayName(value));
		unlockAfterStartup.addListener((_, _, value) -> data.setUnlockAfterStartup(value));
		revealAfterMount.addListener((_, _, value) -> data.setRevealAfterMount(value));
		usesReadOnlyMode.addListener((_, _, value) -> data.setUsesReadOnlyMode(value));
		mountFlags.addListener((_, _, value) -> data.setMountFlags(value));
		maxCleartextFilenameLength.addListener((_, _, value) -> data.setMaxCleartextFilenameLength(value.intValue()));
		actionAfterUnlock.addListener((_, _, value) -> data.setActionAfterUnlock(value));
		autoLockWhenIdle.addListener((_, _, value) -> data.setAutoLockWhenIdle(value));
		autoLockIdleSeconds.addListener((_, _, value) -> data.setAutoLockIdleSeconds(value.intValue()));
		mountPoint.addListener((_, _, value) -> data.setMountPoint(value));
		mountService.addListener((_, _, value) -> data.setMountService(value));
		port.addListener((_, _, value) -> data.setPort(value.intValue()));
		lastKnownKeyLoader.addListener((_, _, value) -> data.setLastKnownKeyLoader(value));
		data.addListener(field -> refresh(field, data));
	}

	private void refresh(VaultSettingsData.Field field, VaultSettingsData data) {
		switch (field) {
			case PATH -> path.set(data.path());
			case DISPLAY_NAME -> displayName.set(data.displayName());
			case UNLOCK_AFTER_STARTUP -> unlockAfterStartup.set(data.unlockAfterStartup());
			case REVEAL_AFTER_MOUNT -> revealAfterMount.set(data.revealAfterMount());
			case USES_READ_ONLY_MODE -> usesReadOnlyMode.set(data.usesReadOnlyMode());
			case MOUNT_FLAGS -> mountFlags.set(data.mountFlags());
			case MAX_CLEARTEXT_FILENAME_LENGTH -> maxCleartextFilenameLength.set(data.maxCleartextFilenameLength());
			case ACTION_AFTER_UNLOCK -> actionAfterUnlock.set(data.actionAfterUnlock());
			case AUTO_LOCK_WHEN_IDLE -> autoLockWhenIdle.set(data.autoLockWhenIdle());
			case AUTO_LOCK_IDLE_SECONDS -> autoLockIdleSeconds.set(data.autoLockIdleSeconds());
			case MOUNT_POINT -> mountPoint.set(data.mountPoint());
			case MOUNT_SERVICE -> mountService.set(data.mountService());
			case PORT -> port.set(data.port());
			case LAST_KNOWN_KEY_LOADER -> lastKnownKeyLoader.set(data.lastKnownKeyLoader());
		}
	}

	Observable[] observables() {
		return new Observable[]{actionAfterUnlock, autoLockIdleSeconds, autoLockWhenIdle, displayName, maxCleartextFilenameLength, mountFlags, mountPoint, path, revealAfterMount, unlockAfterStartup, usesReadOnlyMode, port, mountService, lastKnownKeyLoader};
	}
}
