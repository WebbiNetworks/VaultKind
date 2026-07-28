/*******************************************************************************
 * Copyright (c) 2016, 2017 Sebastian Stenzel and others.
 * All rights reserved.
 * This program and the accompanying materials are made available under the terms of the accompanying LICENSE file.
 *
 * Contributors:
 *     Sebastian Stenzel - initial API and implementation
 *******************************************************************************/
package org.cryptomator.common.vaults;

import org.apache.commons.lang3.SystemUtils;
import org.cryptomator.common.recovery.BackupRestorer;
import org.cryptomator.common.settings.EngineSettings;
import org.cryptomator.common.settings.VaultSettings;
import org.cryptomator.cryptofs.CryptoFileSystemProvider;
import org.cryptomator.cryptofs.DirStructure;
import org.cryptomator.cryptofs.migration.Migrators;
import org.cryptomator.integrations.mount.MountService;
import org.cryptomator.ui.keyloading.masterkeyfile.MasterkeyFileLoadingStrategy;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javax.inject.Singleton;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.file.AccessDeniedException;
import java.nio.file.Files;
import java.nio.file.NoSuchFileException;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.Collection;
import java.util.List;
import java.util.Optional;
import java.util.ResourceBundle;

import static org.cryptomator.common.Constants.MASTERKEY_FILENAME;
import static org.cryptomator.common.Constants.VAULTCONFIG_FILENAME;
import static org.cryptomator.common.vaults.VaultState.Value.ALL_MISSING;
import static org.cryptomator.common.vaults.VaultState.Value.ERROR;
import static org.cryptomator.common.vaults.VaultState.Value.LOCKED;
import static org.cryptomator.common.vaults.VaultState.Value.MISSING;
import static org.cryptomator.common.vaults.VaultState.Value.NEEDS_MIGRATION;
import static org.cryptomator.common.vaults.VaultState.Value.PROCESSING;
import static org.cryptomator.common.vaults.VaultState.Value.UNLOCKED;
import static org.cryptomator.common.vaults.VaultState.Value.VAULT_CONFIG_MISSING;
import static org.cryptomator.cryptofs.common.Constants.DATA_DIR_NAME;

@Singleton
public class VaultListManager implements VaultRegistry {

	private static final Logger LOG = LoggerFactory.getLogger(VaultListManager.class);

	private final AutoLocker autoLocker;
	private final List<MountService> mountServices;
	private final VaultComponent.Factory vaultComponentFactory;
	private final List<Vault> vaultList;
	private final VaultMutationDispatcher mutationDispatcher;
	private final VaultListPersistence vaultListPersistence;
	private final String defaultVaultName;

	@Inject
	public VaultListManager(List<Vault> vaultList, //
							AutoLocker autoLocker, //
							List<MountService> mountServices, //
							VaultComponent.Factory vaultComponentFactory, //
							VaultMutationDispatcher mutationDispatcher, //
							VaultListPersistence vaultListPersistence, //
							ResourceBundle resourceBundle, //
							EngineSettings settings) {
		this.vaultList = vaultList;
		this.autoLocker = autoLocker;
		this.mountServices = mountServices;
		this.vaultComponentFactory = vaultComponentFactory;
		this.mutationDispatcher = mutationDispatcher;
		this.vaultListPersistence = vaultListPersistence;
		this.defaultVaultName = resourceBundle.getString("defaults.vault.vaultName");

		addAll(settings.configuredVaults());
		vaultListPersistence.initialize();
		autoLocker.init();
	}

	public boolean isAlreadyAdded(Path vaultPath) {
		assert vaultPath.isAbsolute();
		assert vaultPath.normalize().equals(vaultPath);
		return vaultList.stream().anyMatch(v -> vaultPath.equals(v.getPath()));
	}

	/**
	 * Safe to call from any thread: the IO work runs on the calling thread and the
	 * configured dispatcher owns any thread-affinity requirement for list mutation.
	 */
	public Vault add(Path pathToVault) throws IOException {
		Path normalizedPathToVault = pathToVault.normalize().toAbsolutePath();
		assertIsVaultDirectory(normalizedPathToVault);

		return get(normalizedPathToVault).orElseGet(() -> {
			Vault newVault = create(newVaultSettings(normalizedPathToVault));
			mutationDispatcher.dispatch(() -> addVault(newVault));
			return newVault;
		});
	}

	@Override
	public List<Vault> snapshot() {
		return List.copyOf(vaultList);
	}

	@Override
	public Optional<Vault> findById(String vaultId) {
		if (vaultId == null || vaultId.isBlank()) {
			return Optional.empty();
		}
		return vaultList.stream().filter(vault -> vaultId.equals(vault.getId())).findFirst();
	}

	@Override
	public boolean remove(Vault vault) {
		if (vaultList.remove(vault)) {
			vaultListPersistence.vaultRemoved(vault);
			return true;
		}
		return false;
	}

	public static void assertIsVaultDirectory(Path pathToVault) throws IOException {
		if (CryptoFileSystemProvider.checkDirStructureForVault(pathToVault, VAULTCONFIG_FILENAME, MASTERKEY_FILENAME) == DirStructure.UNRELATED) {
			checkDataDir(pathToVault);
			checkConfigFile(pathToVault);
			//if vault is legacy _and_ not readable, just say unsupported
			throw new NotAVaultDirectoryException(pathToVault, NotAVaultDirectoryException.Reason.UNSUPPORTED_STRUCTURE);
		}
	}

	static void checkDataDir(Path pathToVault) throws NotAVaultDirectoryException {
		Path dataDir = pathToVault.resolve(DATA_DIR_NAME);
		if (!Files.exists(dataDir)) {
			throw new NotAVaultDirectoryException(pathToVault, NotAVaultDirectoryException.Reason.MISSING_DATA_DIR);
		}
		if (!Files.isDirectory(dataDir)) {
			throw new NotAVaultDirectoryException(pathToVault, NotAVaultDirectoryException.Reason.DATA_NOT_A_DIRECTORY);
		}
	}

	static void checkConfigFile(Path pathToVault) throws NotAVaultDirectoryException {
		Path vaultConfig = pathToVault.resolve(VAULTCONFIG_FILENAME);

		try (var ch = Files.newByteChannel(vaultConfig, StandardOpenOption.READ)) {
			ch.read(ByteBuffer.allocate(1));
		} catch (AccessDeniedException e) {
			throw new NotAVaultDirectoryException(pathToVault, NotAVaultDirectoryException.Reason.VAULT_CONFIG_ACCESS_DENIED);
		} catch (NoSuchFileException e) {
			throw new NotAVaultDirectoryException(pathToVault, NotAVaultDirectoryException.Reason.MISSING_VAULT_CONFIG);
		} catch (IOException e) {
			LOG.warn("Failed to read vault config: {}", e.getMessage());
			throw new NotAVaultDirectoryException(pathToVault, NotAVaultDirectoryException.Reason.UNSUPPORTED_STRUCTURE);
		}
	}

	private VaultSettings newVaultSettings(Path path) {
		VaultSettings vaultSettings = VaultSettings.withRandomId();
		vaultSettings.path.set(path);
		if (path.getFileName() != null) {
			vaultSettings.displayName.set(path.getFileName().toString());
		} else {
			vaultSettings.displayName.set(defaultVaultName);
		}

		//due to https://github.com/cryptomator/cryptomator/issues/2880#issuecomment-1680313498
		var nameOfWinfspLocalMounter = "org.cryptomator.frontend.fuse.mount.WinFspMountProvider";
		if (SystemUtils.IS_OS_WINDOWS //
				&& vaultSettings.path.get().toString().contains("Dropbox") //
				&& mountServices.stream().anyMatch(s -> s.getClass().getName().equals(nameOfWinfspLocalMounter))) {
			vaultSettings.mountService.setValue(nameOfWinfspLocalMounter);
		}

		return vaultSettings;
	}

	private void addAll(Collection<VaultSettings> vaultSettings) {
		Collection<Vault> vaults = vaultSettings.stream().map(this::create).toList();
		vaultList.addAll(vaults);
	}

	public Optional<Vault> get(Path vaultPath) {
		assert vaultPath.isAbsolute();
		assert vaultPath.normalize().equals(vaultPath);
		return vaultList.stream() //
				.filter(v -> vaultPath.equals(v.getPath())) //
				.findAny();
	}

	public void addVault(Vault vault) {
		Path path = vault.getPath().normalize().toAbsolutePath();
		if (!isAlreadyAdded(path)) {
			vaultList.add(vault);
			vaultListPersistence.vaultAdded(vault);
		}
	}

	private Vault create(VaultSettings vaultSettings) {
		var wrapper = new VaultConfigCache(vaultSettings);
		try {
			var vaultState = determineVaultState(vaultSettings.path.get());
			initializeLastKnownKeyLoaderIfPossible(vaultSettings, vaultState, wrapper);

			return vaultComponentFactory.create(vaultSettings, wrapper, vaultState, null).vault();
		} catch (IOException e) {
			LOG.warn("Failed to determine vault state for {}", vaultSettings.path.get(), e);
			return vaultComponentFactory.create(vaultSettings, wrapper, ERROR, e).vault();
		}
	}

	private void initializeLastKnownKeyLoaderIfPossible(VaultSettings vaultSettings, VaultState.Value vaultState, VaultConfigCache wrapper) throws IOException {
		if (vaultSettings.lastKnownKeyLoader.get() != null) {
			return;
		}

		switch (vaultState) {
			case LOCKED -> {
				wrapper.reloadConfig();
				vaultSettings.lastKnownKeyLoader.set(wrapper.get().getKeyId().getScheme());
			}
			case NEEDS_MIGRATION -> {
				//for legacy reasons: pre v8 vault do not have a config, but they are in the NEEDS_MIGRATION state
				vaultSettings.lastKnownKeyLoader.set(MasterkeyFileLoadingStrategy.SCHEME);
			}
			case VAULT_CONFIG_MISSING -> {
				//Nothing to do here, since there is no config to read
			}
			case MISSING, ALL_MISSING, ERROR, PROCESSING -> {
				// no config available or not safe to load
			}
			default -> {
				if (Files.exists(vaultSettings.path.get().resolve(VAULTCONFIG_FILENAME))) {
					try {
						wrapper.reloadConfig();
						vaultSettings.lastKnownKeyLoader.set(wrapper.get().getKeyId().getScheme());
					} catch (IOException e) {
						LOG.debug("Unable to load config for {}", vaultSettings.path.get(), e);
					}
				}
			}
		}
	}

	public static VaultState.Value redetermineVaultState(Vault vault) {
		VaultState.Value previous = vault.getState();

		if (previous.equals(UNLOCKED) || previous.equals(PROCESSING)) {
			return previous;
		}

		try {
			VaultState.Value determined = determineVaultState(vault.getPath());

			if (determined == LOCKED) {
				vault.getVaultConfigCache().reloadConfig();
			}

			vault.setState(determined);
			return determined;
		} catch (IOException e) {
			LOG.warn("Failed to (re)determine vault state for {}", vault.getPath(), e);
			vault.setLastKnownException(e);
			vault.setState(ERROR);
			return ERROR;
		}
	}

	public static VaultState.Value determineVaultState(Path pathToVault) throws IOException {
		if (!Files.exists(pathToVault)) {
			return MISSING;
		}

		VaultState.Value structureResult = checkDirStructure(pathToVault);

		if (structureResult == LOCKED || structureResult == NEEDS_MIGRATION) {
			return structureResult;
		}

		Path pathToVaultConfig = pathToVault.resolve(VAULTCONFIG_FILENAME);
		Path pathToMasterkey = pathToVault.resolve(MASTERKEY_FILENAME);

		if (!Files.exists(pathToVaultConfig)) {
			BackupRestorer.restoreIfBackupPresent(pathToVault, VAULTCONFIG_FILENAME);
		}
		if (!Files.exists(pathToMasterkey)) {
			BackupRestorer.restoreIfBackupPresent(pathToVault, MASTERKEY_FILENAME);
		}

		boolean hasConfig = Files.exists(pathToVaultConfig);

		if (!hasConfig && !Files.exists(pathToMasterkey)) {
			return ALL_MISSING;
		}
		if (!hasConfig) {
			return VAULT_CONFIG_MISSING;
		}

		return checkDirStructure(pathToVault);
	}

	private static VaultState.Value checkDirStructure(Path pathToVault) throws IOException {
		return switch (CryptoFileSystemProvider.checkDirStructureForVault(pathToVault, VAULTCONFIG_FILENAME, MASTERKEY_FILENAME)) {
			case VAULT -> LOCKED;
			case UNRELATED -> MISSING;
			case MAYBE_LEGACY -> Migrators.get().needsMigration(pathToVault, VAULTCONFIG_FILENAME, MASTERKEY_FILENAME) ? NEEDS_MIGRATION : MISSING;
		};
	}

}
