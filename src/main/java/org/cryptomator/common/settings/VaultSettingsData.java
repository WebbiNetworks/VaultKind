package org.cryptomator.common.settings;

import java.nio.file.Path;
import java.util.Objects;
import java.util.concurrent.CopyOnWriteArrayList;

/**
 * UI-neutral source of truth for one vault's persisted settings.
 */
final class VaultSettingsData {

	enum Field {
		PATH, DISPLAY_NAME, UNLOCK_AFTER_STARTUP, REVEAL_AFTER_MOUNT, USES_READ_ONLY_MODE, MOUNT_FLAGS,
		MAX_CLEARTEXT_FILENAME_LENGTH, ACTION_AFTER_UNLOCK, AUTO_LOCK_WHEN_IDLE, AUTO_LOCK_IDLE_SECONDS,
		MOUNT_POINT, MOUNT_SERVICE, PORT, LAST_KNOWN_KEY_LOADER
	}

	@FunctionalInterface
	interface Listener {
		void changed(Field field);
	}

	private final String id;
	private final CopyOnWriteArrayList<Listener> listeners = new CopyOnWriteArrayList<>();
	private Path path;
	private String displayName;
	private boolean unlockAfterStartup;
	private boolean revealAfterMount;
	private boolean usesReadOnlyMode;
	private String mountFlags;
	private int maxCleartextFilenameLength;
	private WhenUnlocked actionAfterUnlock;
	private boolean autoLockWhenIdle;
	private int autoLockIdleSeconds;
	private Path mountPoint;
	private String mountService;
	private int port;
	private String lastKnownKeyLoader;

	VaultSettingsData(VaultSettingsJson json) {
		this.id = json.id;
		this.path = json.path == null ? null : Path.of(json.path);
		this.displayName = json.displayName;
		this.unlockAfterStartup = json.unlockAfterStartup;
		this.revealAfterMount = json.revealAfterMount;
		this.usesReadOnlyMode = json.usesReadOnlyMode;
		this.mountFlags = json.mountFlags;
		this.maxCleartextFilenameLength = json.maxCleartextFilenameLength;
		this.actionAfterUnlock = json.actionAfterUnlock;
		this.autoLockWhenIdle = json.autoLockWhenIdle;
		this.autoLockIdleSeconds = json.autoLockIdleSeconds;
		this.mountPoint = json.mountPoint == null ? null : Path.of(json.mountPoint);
		this.mountService = json.mountService;
		this.port = json.port;
		this.lastKnownKeyLoader = json.lastKnownKeyLoader;
	}

	void addListener(Listener listener) {
		listeners.add(Objects.requireNonNull(listener));
	}

	private void changed(Field field) {
		listeners.forEach(listener -> listener.changed(field));
	}

	String id() { return id; }
	synchronized Path path() { return path; }
	void setPath(Path value) { synchronized (this) { if (Objects.equals(path, value)) return; path = value; } changed(Field.PATH); }
	synchronized String displayName() { return displayName; }
	void setDisplayName(String value) { synchronized (this) { if (Objects.equals(displayName, value)) return; displayName = value; } changed(Field.DISPLAY_NAME); }
	synchronized boolean unlockAfterStartup() { return unlockAfterStartup; }
	void setUnlockAfterStartup(boolean value) { synchronized (this) { if (unlockAfterStartup == value) return; unlockAfterStartup = value; } changed(Field.UNLOCK_AFTER_STARTUP); }
	synchronized boolean revealAfterMount() { return revealAfterMount; }
	void setRevealAfterMount(boolean value) { synchronized (this) { if (revealAfterMount == value) return; revealAfterMount = value; } changed(Field.REVEAL_AFTER_MOUNT); }
	synchronized boolean usesReadOnlyMode() { return usesReadOnlyMode; }
	void setUsesReadOnlyMode(boolean value) { synchronized (this) { if (usesReadOnlyMode == value) return; usesReadOnlyMode = value; } changed(Field.USES_READ_ONLY_MODE); }
	synchronized String mountFlags() { return mountFlags; }
	void setMountFlags(String value) { synchronized (this) { if (Objects.equals(mountFlags, value)) return; mountFlags = value; } changed(Field.MOUNT_FLAGS); }
	synchronized int maxCleartextFilenameLength() { return maxCleartextFilenameLength; }
	void setMaxCleartextFilenameLength(int value) { synchronized (this) { if (maxCleartextFilenameLength == value) return; maxCleartextFilenameLength = value; } changed(Field.MAX_CLEARTEXT_FILENAME_LENGTH); }
	synchronized WhenUnlocked actionAfterUnlock() { return actionAfterUnlock; }
	void setActionAfterUnlock(WhenUnlocked value) { synchronized (this) { if (actionAfterUnlock == value) return; actionAfterUnlock = value; } changed(Field.ACTION_AFTER_UNLOCK); }
	synchronized boolean autoLockWhenIdle() { return autoLockWhenIdle; }
	void setAutoLockWhenIdle(boolean value) { synchronized (this) { if (autoLockWhenIdle == value) return; autoLockWhenIdle = value; } changed(Field.AUTO_LOCK_WHEN_IDLE); }
	synchronized int autoLockIdleSeconds() { return autoLockIdleSeconds; }
	void setAutoLockIdleSeconds(int value) { synchronized (this) { if (autoLockIdleSeconds == value) return; autoLockIdleSeconds = value; } changed(Field.AUTO_LOCK_IDLE_SECONDS); }
	synchronized Path mountPoint() { return mountPoint; }
	void setMountPoint(Path value) { synchronized (this) { if (Objects.equals(mountPoint, value)) return; mountPoint = value; } changed(Field.MOUNT_POINT); }
	synchronized String mountService() { return mountService; }
	void setMountService(String value) { synchronized (this) { if (Objects.equals(mountService, value)) return; mountService = value; } changed(Field.MOUNT_SERVICE); }
	synchronized int port() { return port; }
	void setPort(int value) { synchronized (this) { if (port == value) return; port = value; } changed(Field.PORT); }
	synchronized String lastKnownKeyLoader() { return lastKnownKeyLoader; }
	void setLastKnownKeyLoader(String value) { synchronized (this) { if (Objects.equals(lastKnownKeyLoader, value)) return; lastKnownKeyLoader = value; } changed(Field.LAST_KNOWN_KEY_LOADER); }
}
