package org.cryptomator.nativeui;

import javax.inject.Inject;
import javax.inject.Singleton;
import java.util.concurrent.CountDownLatch;

@Singleton
public class NativeBackendTerminator {

	private final CountDownLatch shutdownRequested = new CountDownLatch(1);

	@Inject
	public NativeBackendTerminator() {
	}

	void requestShutdown() {
		shutdownRequested.countDown();
	}

	void awaitShutdown() throws InterruptedException {
		shutdownRequested.await();
	}
}
