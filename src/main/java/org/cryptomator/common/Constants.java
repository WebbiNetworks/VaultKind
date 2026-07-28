package org.cryptomator.common;

import java.net.URI;

public interface Constants {

	String MASTERKEY_FILENAME = "masterkey.cryptomator";
	String MASTERKEY_SCHEME = "masterkeyfile";
	String MASTERKEY_BACKUP_SUFFIX = ".bkup";
	String VAULTCONFIG_FILENAME = "vault.cryptomator";
	String CRYPTOMATOR_FILENAME_EXT = ".cryptomator";
	String CRYPTOMATOR_FILENAME_GLOB = "*.cryptomator";
	URI DEFAULT_KEY_ID = URI.create(MASTERKEY_SCHEME + ":" + MASTERKEY_FILENAME);
	byte[] PEPPER = new byte[0];
	// Separator used to concatenate Hub username and device name in the filesystem owner identifier.
	String HUB_USER_DEVICE_SEPARATOR = "&";

}
