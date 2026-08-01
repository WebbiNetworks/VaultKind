/*******************************************************************************
 * Copyright (c) 2017 Skymatic UG (haftungsbeschränkt).
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the accompanying LICENSE file.
 *******************************************************************************/
package org.cryptomator.common.settings;

import com.google.common.base.CharMatcher;
import com.google.common.base.Strings;
import com.google.common.io.BaseEncoding;
import org.jetbrains.annotations.VisibleForTesting;

import javafx.beans.Observable;
import javafx.beans.binding.StringExpression;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.IntegerProperty;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.StringProperty;
import java.nio.file.Path;
import java.util.Objects;
import java.util.Random;

/**
 * The settings specific to a single vault.
 */
public class VaultSettings {

	static final boolean DEFAULT_UNLOCK_AFTER_STARTUP = false;
	static final boolean DEFAULT_REVEAL_AFTER_MOUNT = true;
	static final boolean DEFAULT_USES_READONLY_MODE = false;
	static final String DEFAULT_MOUNT_FLAGS = ""; // TODO: remove empty default mount flags and let this property be null if not used
	static final int DEFAULT_MAX_CLEARTEXT_FILENAME_LENGTH = -1;
	static final WhenUnlocked DEFAULT_ACTION_AFTER_UNLOCK = WhenUnlocked.ASK;
	static final boolean DEFAULT_AUTOLOCK_WHEN_IDLE = false;
	static final int DEFAULT_AUTOLOCK_IDLE_SECONDS = 30 * 60;
	static final int DEFAULT_PORT = 42427;

	private static final Random RNG = new Random();
	private final VaultSettingsData data;
	private final LegacyVaultSettingsProperties legacyProperties;

	public final String id;
	public final ObjectProperty<Path> path;
	public final StringProperty displayName;
	public final BooleanProperty unlockAfterStartup;
	public final BooleanProperty revealAfterMount;
	public final BooleanProperty usesReadOnlyMode;
	public final StringProperty mountFlags;
	public final IntegerProperty maxCleartextFilenameLength;
	public final ObjectProperty<WhenUnlocked> actionAfterUnlock;
	public final BooleanProperty autoLockWhenIdle;
	public final IntegerProperty autoLockIdleSeconds;
	public final ObjectProperty<Path> mountPoint;
	public final StringExpression mountName;
	public final StringProperty mountService;
	public final IntegerProperty port;
	public final StringProperty lastKnownKeyLoader;

	VaultSettings(VaultSettingsJson json) {
		this.data = new VaultSettingsData(json);
		this.legacyProperties = new LegacyVaultSettingsProperties(this, data);
		this.id = data.id();
		this.path = legacyProperties.path;
		this.displayName = legacyProperties.displayName;
		this.unlockAfterStartup = legacyProperties.unlockAfterStartup;
		this.revealAfterMount = legacyProperties.revealAfterMount;
		this.usesReadOnlyMode = legacyProperties.usesReadOnlyMode;
		this.mountFlags = legacyProperties.mountFlags;
		this.maxCleartextFilenameLength = legacyProperties.maxCleartextFilenameLength;
		this.actionAfterUnlock = legacyProperties.actionAfterUnlock;
		this.autoLockWhenIdle = legacyProperties.autoLockWhenIdle;
		this.autoLockIdleSeconds = legacyProperties.autoLockIdleSeconds;
		this.mountPoint = legacyProperties.mountPoint;
		this.mountName = legacyProperties.mountName;
		this.mountService = legacyProperties.mountService;
		this.port = legacyProperties.port;
		this.lastKnownKeyLoader = legacyProperties.lastKnownKeyLoader;

		migrateLegacySettings(json);
	}

	@SuppressWarnings("deprecation")
	private void migrateLegacySettings(VaultSettingsJson json) {
		// implicit migration of 1.6.x legacy setting "customMountPath" / "winDriveLetter":
		if (json.useCustomMountPath && !Strings.isNullOrEmpty(json.customMountPath)) {
			this.mountPoint.set(Path.of(json.customMountPath));
		} else if (!Strings.isNullOrEmpty(json.winDriveLetter)) {
			this.mountPoint.set(Path.of(json.winDriveLetter + ":\\"));
		}
	}

	Observable[] observables() {
		return legacyProperties.observables();
	}

	public static VaultSettings withRandomId() {
		var defaults = new VaultSettingsJson();
		defaults.id = generateId();
		return new VaultSettings(defaults);
	}

	public String id() {
		return id;
	}

	public Path path() {
		return data.path();
	}

	public void setPath(Path value) {
		data.setPath(value);
	}

	public String displayName() {
		return data.displayName();
	}

	public void setDisplayName(String value) {
		data.setDisplayName(value);
	}

	public boolean usesReadOnlyMode() {
		return data.usesReadOnlyMode();
	}

	public void setUsesReadOnlyMode(boolean value) {
		data.setUsesReadOnlyMode(value);
	}

	public String mountFlags() {
		return data.mountFlags();
	}

	public void setMountFlags(String value) {
		data.setMountFlags(value);
	}

	public int maxCleartextFilenameLength() {
		return data.maxCleartextFilenameLength();
	}

	public void setMaxCleartextFilenameLength(int value) {
		data.setMaxCleartextFilenameLength(value);
	}

	public boolean unlockAfterStartup() {
		return data.unlockAfterStartup();
	}

	public void setUnlockAfterStartup(boolean value) {
		data.setUnlockAfterStartup(value);
	}

	public boolean revealAfterMount() {
		return data.revealAfterMount();
	}

	public void setRevealAfterMount(boolean value) {
		data.setRevealAfterMount(value);
	}

	public WhenUnlocked actionAfterUnlock() {
		return data.actionAfterUnlock();
	}

	public void setActionAfterUnlock(WhenUnlocked value) {
		data.setActionAfterUnlock(value);
	}

	public boolean autoLockWhenIdle() {
		return data.autoLockWhenIdle();
	}

	public void setAutoLockWhenIdle(boolean value) {
		data.setAutoLockWhenIdle(value);
	}

	public int autoLockIdleSeconds() {
		return data.autoLockIdleSeconds();
	}

	public void setAutoLockIdleSeconds(int value) {
		data.setAutoLockIdleSeconds(value);
	}

	public Path mountPoint() {
		return data.mountPoint();
	}

	public void setMountPoint(Path value) {
		data.setMountPoint(value);
	}

	public String mountName() {
		var name = data.displayName() == null || data.displayName().isEmpty() ? data.path().getFileName().toString() : data.displayName();
		return normalizeDisplayName(name);
	}

	public String mountService() {
		return data.mountService();
	}

	public void setMountService(String value) {
		data.setMountService(value);
	}

	public int port() {
		return data.port();
	}

	public void setPort(int value) {
		data.setPort(value);
	}

	public String lastKnownKeyLoader() {
		return data.lastKnownKeyLoader();
	}

	public void setLastKnownKeyLoader(String value) {
		data.setLastKnownKeyLoader(value);
	}

	private static String generateId() {
		byte[] randomBytes = new byte[9];
		RNG.nextBytes(randomBytes);
		return BaseEncoding.base64Url().encode(randomBytes);
	}

	VaultSettingsJson serialized() {
		var json = new VaultSettingsJson();
		json.id = id;
		json.path = data.path() == null ? null : data.path().toString();
		json.displayName = data.displayName();
		json.unlockAfterStartup = data.unlockAfterStartup();
		json.revealAfterMount = data.revealAfterMount();
		json.usesReadOnlyMode = data.usesReadOnlyMode();
		json.mountFlags = data.mountFlags();
		json.maxCleartextFilenameLength = data.maxCleartextFilenameLength();
		json.actionAfterUnlock = data.actionAfterUnlock();
		json.autoLockWhenIdle = data.autoLockWhenIdle();
		json.autoLockIdleSeconds = data.autoLockIdleSeconds();
		json.mountPoint = data.mountPoint() == null ? null : data.mountPoint().toString();
		json.mountService = data.mountService();
		json.port = data.port();
		json.lastKnownKeyLoader = data.lastKnownKeyLoader();
		return json;
	}

	@VisibleForTesting
	static String normalizeDisplayName(String original) {
		if (original.isBlank() || ".".equals(original) || "..".equals(original)) {
			return "_";
		}

		// replace whitespaces (tabs, linebreaks, ...) by simple space (0x20)
		var withoutFancyWhitespaces = CharMatcher.whitespace().collapseFrom(original, ' ');

		// replace control chars as well as chars that aren't allowed in file names on standard file systems by underscore
		return CharMatcher.anyOf("<>:\"/\\|?*").or(CharMatcher.javaIsoControl()).collapseFrom(withoutFancyWhitespaces, '_');
	}

	/* Hashcode/Equals */

	@Override
	public int hashCode() {
		return Objects.hash(id);
	}

	@Override
	public boolean equals(Object obj) {
		if (obj instanceof VaultSettings other && obj.getClass().equals(this.getClass())) {
			return Objects.equals(this.id, other.id);
		} else {
			return false;
		}
	}
}
