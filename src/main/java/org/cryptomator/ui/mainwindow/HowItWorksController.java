package org.cryptomator.ui.mainwindow;

import org.cryptomator.ui.common.FxController;

import javax.inject.Inject;
import javafx.application.Platform;
import javafx.fxml.FXML;
import javafx.scene.control.ScrollPane;
import javafx.scene.Node;

@MainWindowScoped
public class HowItWorksController implements FxController {

	private final MainWindowNavigation navigation;
	private final VaultListController vaultListController;
	@FXML
	private ScrollPane contentScroll;
	@FXML
	private Node howChapter;
	@FXML
	private Node createChapter;
	@FXML
	private Node recoveryChapter;
	@FXML
	private Node cloudChapter;
	@FXML
	private Node virtualDriveChapter;
	@FXML
	private Node securityChapter;
	@FXML
	private Node faqChapter;

	@Inject
	HowItWorksController(MainWindowNavigation navigation, VaultListController vaultListController) {
		this.navigation = navigation;
		this.vaultListController = vaultListController;
	}

	@FXML
	public void initialize() {
		navigation.destinationProperty().addListener((_, _, destination) -> {
			if (destination == MainWindowNavigation.Destination.HOW_IT_WORKS) {
				Platform.runLater(() -> contentScroll.setVvalue(0));
			}
		});
	}

	@FXML
	public void showDashboard() {
		navigation.showHome();
	}

	@FXML
	public void addVault() {
		vaultListController.didClickAddVault();
	}

	@FXML
	public void showHowChapter() {
		scrollTo(howChapter);
	}

	@FXML
	public void showCreateChapter() {
		scrollTo(createChapter);
	}

	@FXML
	public void showRecoveryChapter() {
		scrollTo(recoveryChapter);
	}

	@FXML
	public void showCloudChapter() {
		scrollTo(cloudChapter);
	}

	@FXML
	public void showVirtualDriveChapter() {
		scrollTo(virtualDriveChapter);
	}

	@FXML
	public void showSecurityChapter() {
		scrollTo(securityChapter);
	}

	@FXML
	public void showFaqChapter() {
		scrollTo(faqChapter);
	}

	private void scrollTo(Node chapter) {
		Platform.runLater(() -> {
			Node content = contentScroll.getContent();
			double scrollableHeight = content.getBoundsInLocal().getHeight() - contentScroll.getViewportBounds().getHeight();
			if (scrollableHeight > 0) {
				contentScroll.setVvalue(Math.clamp(chapter.getBoundsInParent().getMinY() / scrollableHeight, 0.0, 1.0));
			}
		});
	}
}
