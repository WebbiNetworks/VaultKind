/*******************************************************************************
 * Copyright (c) 2017 Skymatic UG (haftungsbeschränkt).
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the accompanying LICENSE file.
 *******************************************************************************/
package org.cryptomator.common.vaults;

import dagger.Module;
import dagger.Provides;
import org.cryptomator.cryptofs.CryptoFileSystem;

import java.util.concurrent.atomic.AtomicReference;

@Module
public class VaultModule {

	@Provides
	@PerVault
	public AtomicReference<CryptoFileSystem> provideCryptoFileSystemReference() {
		return new AtomicReference<>();
	}

}
