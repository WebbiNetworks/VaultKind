package org.cryptomator.launcher;

import dagger.Module;
import dagger.Provides;

import javax.inject.Singleton;
import java.util.ResourceBundle;

@Module
class NativeBackendModule {

	@Provides
	@Singleton
	static ResourceBundle provideLocalization() {
		return ResourceBundle.getBundle("i18n.strings");
	}

}
