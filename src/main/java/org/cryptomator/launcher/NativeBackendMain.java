package org.cryptomator.launcher;

import org.cryptomator.common.SubstitutingProperties;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Locale;

/**
 * Dedicated headless entry point for the native Windows frontend.
 */
public final class NativeBackendMain {

	private static final Logger LOG;

	static {
		var adminProps = AdminPropertiesFactory.create();
		var lazyProcessedProps = new SubstitutingProperties(adminProps, System.getenv(), EventualLogger.INSTANCE);
		System.setProperties(lazyProcessedProps);
		LOG = LoggerFactory.getLogger(NativeBackendMain.class);
	}

	private NativeBackendMain() {
	}

	public static void main(String[] args) {
		Locale.setDefault(Locale.ENGLISH);
		System.setProperty("vaultkind.nativeBackend", "true");
		int exitCode = DaggerNativeBackendComponent.create().application().run();
		LOG.info("Native backend exit {}", exitCode);
		System.exit(exitCode);
	}
}
