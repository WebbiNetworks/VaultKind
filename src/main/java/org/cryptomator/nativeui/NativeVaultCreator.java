package org.cryptomator.nativeui;

import org.cryptomator.common.vaults.VaultListManager;
import org.cryptomator.cryptofs.CryptoFileSystemProperties;
import org.cryptomator.cryptofs.CryptoFileSystemProvider;
import org.cryptomator.cryptolib.api.CryptorProvider;
import org.cryptomator.cryptolib.api.Masterkey;
import org.cryptomator.cryptolib.api.MasterkeyLoader;
import org.cryptomator.cryptolib.common.MasterkeyFileAccess;
import org.cryptomator.common.recovery.RecoveryKeyFactory;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import java.io.IOException;
import java.nio.CharBuffer;
import java.nio.channels.WritableByteChannel;
import java.nio.file.FileSystem;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.security.SecureRandom;
import java.util.Arrays;

import static java.nio.charset.StandardCharsets.US_ASCII;
import static org.cryptomator.common.Constants.DEFAULT_KEY_ID;
import static org.cryptomator.common.Constants.MASTERKEY_FILENAME;

public class NativeVaultCreator {

	private static final Logger LOG = LoggerFactory.getLogger(NativeVaultCreator.class);
	private static final int MAX_PASSWORD_CHARS = 4096;
	private static final int STANDARD_SHORTENING_THRESHOLD = 220;
	private static final int COMPATIBILITY_SHORTENING_THRESHOLD = 160;
	private static final String ACCESS_README = "{\\rtf1\\ansi\\uc0\\fs32\n"
			+ "{\\fs40\\b\\qc WELCOME TO YOUR VAULT}\\par\n"
			+ "{This virtual drive is the readable working view of your vault.}\\par\n"
			+ "{Add, open, edit, and organize files here just as you would in any other drive or folder. "
			+ "VaultKind stores the corresponding encrypted data inside the vault's separate storage folder.}\\par\n"
			+ "{When you lock the vault, this readable view closes. The encrypted storage folder remains safe to keep locally, back up, or synchronize through your cloud provider.}\\par\n"
			+ "{You may safely delete this welcome file.}\\par}";
	private static final String STORAGE_README = "{\\rtf1\\ansi\\uc0\\fs32\n"
			+ "{\\fs40\\b\\qc VAULTKIND ENCRYPTED STORAGE}\\par\n"
			+ "{This folder contains encrypted vault data. Do not add, edit, rename, or organize files here directly.}\\par\n"
			+ "{Open the vault in VaultKind and work through its readable virtual drive instead.}\\par}";

	private final SecureRandom csprng;
	private final MasterkeyFileAccess masterkeyFileAccess;
	private final RecoveryKeyFactory recoveryKeyFactory;
	private final VaultListManager vaultListManager;

	@Inject
	public NativeVaultCreator(SecureRandom csprng, MasterkeyFileAccess masterkeyFileAccess, RecoveryKeyFactory recoveryKeyFactory, VaultListManager vaultListManager) {
		this.csprng = csprng;
		this.masterkeyFileAccess = masterkeyFileAccess;
		this.recoveryKeyFactory = recoveryKeyFactory;
		this.vaultListManager = vaultListManager;
	}

	public NativeCreateResult create(String suppliedPath, char[] password, boolean createRecoveryKey, boolean useShortNames) {
		if (suppliedPath == null || suppliedPath.isBlank() || password == null || password.length < 8 || password.length > MAX_PASSWORD_CHARS) {
			clear(password);
			return NativeCreateResult.error("invalid_request");
		}

		Path path;
		try {
			path = Path.of(suppliedPath).normalize().toAbsolutePath();
		} catch (RuntimeException e) {
			clear(password);
			return NativeCreateResult.error("invalid_path");
		}

		if (Files.exists(path)) {
			clear(password);
			return NativeCreateResult.error("location_exists");
		}

		String stage = "folder";
		try {
			Files.createDirectory(path);
			stage = "masterkey";
			Path masterkeyFilePath = path.resolve(MASTERKEY_FILENAME);
			String recoveryKey;
			try (Masterkey masterkey = Masterkey.generate(csprng)) {
				masterkeyFileAccess.persist(masterkey, masterkeyFilePath, CharBuffer.wrap(password));
				stage = "recovery_key";
				recoveryKey = createRecoveryKey ? recoveryKeyFactory.createRecoveryKey(masterkey) : null;

				stage = "vault_structure";
				MasterkeyLoader loader = ignored -> masterkey.copy();
				CryptoFileSystemProperties fsProps = CryptoFileSystemProperties.cryptoFileSystemProperties()
						.withCipherCombo(CryptorProvider.Scheme.SIV_GCM)
						.withKeyLoader(loader)
						.withShorteningThreshold(useShortNames ? COMPATIBILITY_SHORTENING_THRESHOLD : STANDARD_SHORTENING_THRESHOLD)
						.build();
				CryptoFileSystemProvider.initialize(path, fsProps, DEFAULT_KEY_ID);

				try (FileSystem fs = CryptoFileSystemProvider.newFileSystem(path, fsProps);
					 WritableByteChannel channel = Files.newByteChannel(fs.getPath("/", "welcome.rtf"), StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE)) {
					channel.write(US_ASCII.encode(ACCESS_README));
				}
			}

			stage = "storage_readme";
			try (WritableByteChannel channel = Files.newByteChannel(path.resolve("VAULTKIND_README.rtf"), StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE)) {
				channel.write(US_ASCII.encode(STORAGE_README));
			}

			stage = "registration";
			var vault = vaultListManager.add(path);
			LOG.info("Created native VaultKind vault at {}", path);
			return NativeCreateResult.success(vault.getId(), recoveryKey);
		} catch (IOException | RuntimeException e) {
			LOG.warn("Native vault creation failed during {} at {}", stage, path, e);
			return NativeCreateResult.error("create_failed_" + stage);
		} finally {
			clear(password);
		}
	}

	public NativeCreateResult connect(String suppliedPath) {
		if (suppliedPath == null || suppliedPath.isBlank()) {
			return NativeCreateResult.error("invalid_path");
		}

		try {
			Path path = Path.of(suppliedPath).normalize().toAbsolutePath();
			if (!Files.isDirectory(path)) {
				return NativeCreateResult.error("location_unavailable");
			}
			if (vaultListManager.isAlreadyAdded(path)) {
				return NativeCreateResult.error("already_connected");
			}

			var vault = vaultListManager.add(path);
			LOG.info("Connected existing VaultKind vault at {}", path);
			return NativeCreateResult.success(vault.getId(), null);
		} catch (org.cryptomator.common.vaults.NotAVaultDirectoryException e) {
			return NativeCreateResult.error("not_a_vault");
		} catch (IOException | RuntimeException e) {
			LOG.warn("Unable to connect existing vault at {}", suppliedPath, e);
			return NativeCreateResult.error("connect_failed");
		}
	}

	private static void clear(char[] password) {
		if (password != null) {
			Arrays.fill(password, '\0');
		}
	}

	public record NativeCreateResult(boolean ok, String error, String state, String vaultId, String recoveryKey) {
		static NativeCreateResult success(String vaultId, String recoveryKey) {
			return new NativeCreateResult(true, null, "created", vaultId, recoveryKey);
		}

		static NativeCreateResult error(String error) {
			return new NativeCreateResult(false, error, null, null, null);
		}
	}
}
