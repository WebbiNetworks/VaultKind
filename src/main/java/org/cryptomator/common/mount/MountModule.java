package org.cryptomator.common.mount;

import dagger.Module;
import dagger.Provides;
import org.cryptomator.common.ObservableUtil;
import org.cryptomator.common.settings.Settings;
import org.cryptomator.integrations.mount.MountService;

import javax.inject.Named;
import javax.inject.Singleton;
import javafx.beans.value.ObservableValue;
import java.util.List;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

@Module
public class MountModule {

	@Provides
	@Singleton
	static List<MountService> provideSupportedMountServices() {
		return MountService.get().toList();
	}

	@Provides
	@Singleton
	static ObservableValue<MountService> provideDefaultMountService(MountServiceSelector selector, Settings settings) {
		return ObservableUtil.mapWithDefault(settings.mountService, //
				_ -> selector.defaultMountService(), //
				selector::defaultMountService);
	}

	@Provides
	@Singleton
	@Named("usedMountServices")
	static Set<MountService> provideSetOfUsedMountServices() {
		return ConcurrentHashMap.newKeySet();
	}

}
