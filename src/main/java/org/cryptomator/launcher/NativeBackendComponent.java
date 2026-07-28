package org.cryptomator.launcher;

import dagger.Component;
import org.cryptomator.common.CommonsModule;
import org.cryptomator.nativeui.NativeBackendApplication;

import javax.inject.Singleton;

@Singleton
@Component(modules = {NativeBackendModule.class, CommonsModule.class})
interface NativeBackendComponent {

	NativeBackendApplication application();

}
