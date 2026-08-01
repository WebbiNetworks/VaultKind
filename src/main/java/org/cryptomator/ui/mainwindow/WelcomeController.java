package org.cryptomator.ui.mainwindow;

import org.cryptomator.common.vaults.Vault;
import org.cryptomator.ui.common.FxController;

import javax.inject.Inject;
import javafx.beans.binding.Bindings;
import javafx.beans.binding.BooleanBinding;
import javafx.beans.binding.IntegerBinding;
import javafx.collections.ObservableList;
import javafx.fxml.FXML;
import java.util.EnumSet;

import static org.cryptomator.common.vaults.VaultState.Value.ALL_MISSING;
import static org.cryptomator.common.vaults.VaultState.Value.ERROR;
import static org.cryptomator.common.vaults.VaultState.Value.MISSING;
import static org.cryptomator.common.vaults.VaultState.Value.NEEDS_MIGRATION;
import static org.cryptomator.common.vaults.VaultState.Value.VAULT_CONFIG_MISSING;

@MainWindowScoped
public class WelcomeController implements FxController {

	private final MainWindowNavigation navigation;
	private final BooleanBinding noVaultPresent;
	private final IntegerBinding totalVaultCount;
	private final IntegerBinding lockedVaultCount;
	private final IntegerBinding unlockedVaultCount;
	private final IntegerBinding attentionVaultCount;
	private final BooleanBinding allVaultsHealthy;

	@Inject
	public WelcomeController(ObservableList<Vault> vaults, MainWindowNavigation navigation) {
		this.navigation = navigation;
		this.noVaultPresent = Bindings.isEmpty(vaults);
		this.totalVaultCount = Bindings.size(vaults);
		this.lockedVaultCount = Bindings.createIntegerBinding(() -> (int) vaults.stream().filter(Vault::isLocked).count(), vaults);
		this.unlockedVaultCount = Bindings.createIntegerBinding(() -> (int) vaults.stream().filter(Vault::isUnlocked).count(), vaults);
		var attentionStates = EnumSet.of(NEEDS_MIGRATION, MISSING, VAULT_CONFIG_MISSING, ALL_MISSING, ERROR);
		this.attentionVaultCount = Bindings.createIntegerBinding(() -> (int) vaults.stream().filter(vault -> attentionStates.contains(vault.getState())).count(), vaults);
		this.allVaultsHealthy = attentionVaultCount.isEqualTo(0);
	}

	@FXML
	public void visitGettingStartedGuide() {
		navigation.showHowItWorks();
	}

	@FXML
	public void openVaultDoctor() {
		navigation.showVaultDoctor();
	}

	/* Getter/Setter */

	public BooleanBinding noVaultPresentProperty() {
		return noVaultPresent;
	}

	public boolean isNoVaultPresent() {
		return noVaultPresent.get();
	}

	public IntegerBinding totalVaultCountProperty() {
		return totalVaultCount;
	}

	public int getTotalVaultCount() {
		return totalVaultCount.get();
	}

	public IntegerBinding lockedVaultCountProperty() {
		return lockedVaultCount;
	}

	public int getLockedVaultCount() {
		return lockedVaultCount.get();
	}

	public IntegerBinding unlockedVaultCountProperty() {
		return unlockedVaultCount;
	}

	public int getUnlockedVaultCount() {
		return unlockedVaultCount.get();
	}

	public IntegerBinding attentionVaultCountProperty() {
		return attentionVaultCount;
	}

	public int getAttentionVaultCount() {
		return attentionVaultCount.get();
	}

	public BooleanBinding allVaultsHealthyProperty() {
		return allVaultsHealthy;
	}

	public boolean isAllVaultsHealthy() {
		return allVaultsHealthy.get();
	}

}
