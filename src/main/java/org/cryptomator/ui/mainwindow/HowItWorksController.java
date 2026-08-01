package org.cryptomator.ui.mainwindow;

import org.cryptomator.common.settings.Settings;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.controls.FontAwesome5Icon;
import org.cryptomator.ui.controls.FontAwesome5IconView;

import javax.inject.Inject;
import javafx.application.Platform;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.ReadOnlyBooleanProperty;
import javafx.beans.property.ReadOnlyStringProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.StringProperty;
import javafx.fxml.FXML;
import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.ScrollPane;
import javafx.scene.control.TextField;
import javafx.scene.Node;
import javafx.scene.layout.VBox;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.ResourceBundle;

@MainWindowScoped
public class HowItWorksController implements FxController {
	private static final double CHAPTER_TOP_MARGIN = 24.0;
	private static final String TOPIC_HOW = "how-vaultkind-works";
	private static final String TOPIC_CREATE = "creating-your-first-vault";
	private static final String TOPIC_RECOVERY = "recovery-keys";
	private static final String TOPIC_CLOUD = "cloud-storage";
	private static final String TOPIC_VIRTUAL_DRIVE = "virtual-drives";
	private static final String TOPIC_SECURITY = "security-tips";
	private static final String TOPIC_FAQ = "faq";

	private final MainWindowNavigation navigation;
	private final VaultListController vaultListController;
	private final Settings settings;
	private final StringProperty progressText = new SimpleStringProperty();
	private final BooleanProperty resetProgressDisabled = new SimpleBooleanProperty(true);
	private final StringProperty assistantAnswerTitle = new SimpleStringProperty();
	private final StringProperty assistantAnswerBody = new SimpleStringProperty();
	private final BooleanProperty assistantAnswerVisible = new SimpleBooleanProperty(false);
	private final BooleanProperty assistantStructuredResultVisible = new SimpleBooleanProperty(false);
	private final BooleanProperty assistantMode = new SimpleBooleanProperty(false);
	private final BooleanProperty assistantBrowseVisible = new SimpleBooleanProperty(false);
	private final StringProperty assistantBrowseTitle = new SimpleStringProperty();
	private final StringProperty assistantCaseLabel = new SimpleStringProperty();
	private final StringProperty assistantConfidence = new SimpleStringProperty();
	private final StringProperty assistantCause = new SimpleStringProperty();
	private final StringProperty assistantChecks = new SimpleStringProperty();
	private final StringProperty assistantFix = new SimpleStringProperty();
	private final StringProperty assistantEvidence = new SimpleStringProperty();
	private final ResourceBundle resourceBundle;
	private DiagnosticCase.Category activeDiagnosticCategory;
	private Button activeDiagnosticCategoryButton;
	@FXML
	private ScrollPane contentScroll;
	@FXML
	private TextField topicSearch;
	@FXML
	private Node noSearchResults;
	@FXML
	private Button howChapterButton;
	@FXML
	private Button createChapterButton;
	@FXML
	private Button recoveryChapterButton;
	@FXML
	private Button cloudChapterButton;
	@FXML
	private Button virtualDriveChapterButton;
	@FXML
	private Button securityChapterButton;
	@FXML
	private Button faqChapterButton;
	@FXML
	private Button assistantChapterButton;
	@FXML
	private Button assistantAllButton;
	@FXML
	private Button assistantStartupButton;
	@FXML
	private Button assistantVaultButton;
	@FXML
	private Button assistantFilesystemButton;
	@FXML
	private Button assistantRecoveryButton;
	@FXML
	private TextField assistantSearch;
	@FXML
	private VBox assistantCaseResults;
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
	@FXML
	private Node assistantChapter;

	@Inject
	HowItWorksController(MainWindowNavigation navigation, VaultListController vaultListController, Settings settings, ResourceBundle resourceBundle) {
		this.navigation = navigation;
		this.vaultListController = vaultListController;
		this.settings = settings;
		this.resourceBundle = resourceBundle;
		assistantMode.bindBidirectional(navigation.assistantModeProperty());
	}

	@FXML
	public void initialize() {
		topicSearch.textProperty().addListener((_, _, query) -> filterTopics(query));
		settings.learningCenterCompletedTopics.addListener((javafx.collections.SetChangeListener<String>) _ -> updateProgressDisplay());
		updateProgressDisplay();
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
		showChapter(TOPIC_HOW, howChapterButton, howChapter);
	}

	@FXML
	public void showCreateChapter() {
		showChapter(TOPIC_CREATE, createChapterButton, createChapter);
	}

	@FXML
	public void showRecoveryChapter() {
		showChapter(TOPIC_RECOVERY, recoveryChapterButton, recoveryChapter);
	}

	@FXML
	public void showCloudChapter() {
		showChapter(TOPIC_CLOUD, cloudChapterButton, cloudChapter);
	}

	@FXML
	public void showVirtualDriveChapter() {
		showChapter(TOPIC_VIRTUAL_DRIVE, virtualDriveChapterButton, virtualDriveChapter);
	}

	@FXML
	public void showSecurityChapter() {
		showChapter(TOPIC_SECURITY, securityChapterButton, securityChapter);
	}

	@FXML
	public void showFaqChapter() {
		showChapter(TOPIC_FAQ, faqChapterButton, faqChapter);
	}

	@FXML
	public void showAssistantChapter() {
		assistantMode.set(true);
		chapterButtons().forEach(button -> button.getStyleClass().remove("learn-chapter-button-active"));
		assistantChapterButton.getStyleClass().add("learn-chapter-button-active");
		if (!assistantAnswerVisible.get()) {
			showAllDiagnostics();
		}
		Platform.runLater(() -> contentScroll.setVvalue(0));
	}

	@FXML
	public void returnToLearningCenter() {
		showChapter(null, howChapterButton, howChapter);
	}

	@FXML
	public void showAllDiagnostics() {
		showDiagnosticList(null, assistantAllButton);
	}

	@FXML
	public void showStartupDiagnostics() {
		showDiagnosticList(DiagnosticCase.Category.STARTUP, assistantStartupButton);
	}

	@FXML
	public void showVaultDiagnostics() {
		showDiagnosticList(DiagnosticCase.Category.VAULT, assistantVaultButton);
	}

	@FXML
	public void showFilesystemDiagnostics() {
		showDiagnosticList(DiagnosticCase.Category.FILESYSTEM, assistantFilesystemButton);
	}

	@FXML
	public void showRecoveryDiagnostics() {
		showDiagnosticList(DiagnosticCase.Category.RECOVERY, assistantRecoveryButton);
	}

	private void showDiagnosticList(DiagnosticCase.Category category, Button selectedButton) {
		activeDiagnosticCategory = category;
		activeDiagnosticCategoryButton = selectedButton;
		assistantCategoryButtons().forEach(button -> button.getStyleClass().remove("learn-chapter-button-active"));
		selectedButton.getStyleClass().add("learn-chapter-button-active");
		List<DiagnosticCase> diagnosticCases = category == null ? DiagnosticCatalog.all() : DiagnosticCatalog.byCategory(category);
		String categoryName = category == null ? resourceBundle.getString("diagnostics.browse.all") : resourceBundle.getString("diagnostics.category." + category.name().toLowerCase(Locale.ROOT));
		assistantBrowseTitle.set(resourceBundle.getString("diagnostics.browse.title").formatted(categoryName));
		assistantCaseResults.getChildren().clear();
		diagnosticCases.forEach(diagnosticCase -> {
			Button caseButton = new Button(diagnosticCase.id() + "  —  " + resourceBundle.getString(diagnosticCase.titleKey()));
			caseButton.setMaxWidth(Double.MAX_VALUE);
			caseButton.setAlignment(Pos.CENTER_LEFT);
			caseButton.setWrapText(true);
			caseButton.getStyleClass().add("diagnostic-case-button");
			caseButton.setOnAction(_ -> showDiagnostic(diagnosticCase.match(diagnosticCase.id())));
			assistantCaseResults.getChildren().add(caseButton);
		});
		assistantAnswerVisible.set(false);
		assistantBrowseVisible.set(true);
	}

	@FXML
	public void returnToDiagnosticList() {
		showDiagnosticList(activeDiagnosticCategory, activeDiagnosticCategoryButton == null ? assistantAllButton : activeDiagnosticCategoryButton);
	}

	@FXML
	public void showUnlockHelp() {
		showDiagnostic(DiagnosticCatalog.byId("VK-1001").match("cannot unlock"));
	}

	@FXML
	public void showMissingVaultHelp() {
		showDiagnostic(DiagnosticCatalog.byId("VK-1002").match("vault missing"));
	}

	@FXML
	public void showCloudHelp() {
		showDiagnostic(DiagnosticCatalog.byId("VK-2004").match("synchronization problem"));
	}

	@FXML
	public void showDriveHelp() {
		showDiagnostic(DiagnosticCatalog.byId("VK-2005").match("virtual drive missing"));
	}

	@FXML
	public void findAssistantHelp() {
		String query = assistantSearch.getText() == null ? "" : assistantSearch.getText().strip().toLowerCase(Locale.ROOT);
		if (query.isEmpty()) {
			showAssistantAnswer("howItWorks.assistant.search.empty.title", "howItWorks.assistant.search.empty.body");
			return;
		}

		var bestMatch = DiagnosticCatalog.findBestMatch(query);
		if (bestMatch.isEmpty()) {
			showAssistantAnswer("howItWorks.assistant.search.none.title", "howItWorks.assistant.search.none.body");
		} else {
			showDiagnostic(bestMatch.get());
		}
	}

	private void showDiagnostic(DiagnosticCase.DiagnosticMatch match) {
		assistantCategoryButtons().forEach(button -> button.getStyleClass().remove("learn-chapter-button-active"));
		DiagnosticCase diagnosticCase = match.diagnosticCase();
		assistantAnswerTitle.set(resourceBundle.getString(diagnosticCase.titleKey()));
		assistantAnswerBody.set("");
		assistantCaseLabel.set(diagnosticCase.id() + "  •  " + resourceBundle.getString("diagnostics.category." + diagnosticCase.category().name().toLowerCase(Locale.ROOT)));
		assistantConfidence.set(resourceBundle.getString("diagnostics.confidence." + match.confidence().name().toLowerCase(Locale.ROOT)));
		assistantCause.set(resourceBundle.getString(diagnosticCase.causeKey()));
		assistantChecks.set(resourceBundle.getString(diagnosticCase.checksKey()));
		assistantFix.set(resourceBundle.getString(diagnosticCase.fixKey()));
		assistantEvidence.set(match.matchedTerms().isEmpty() ? resourceBundle.getString("diagnostics.evidence.selected") : resourceBundle.getString("diagnostics.evidence.matched").formatted(String.join(", ", match.matchedTerms())));
		assistantStructuredResultVisible.set(true);
		assistantBrowseVisible.set(false);
		assistantAnswerVisible.set(true);
	}

	private void showAssistantAnswer(String titleKey, String bodyKey) {
		assistantAnswerTitle.set(resourceBundle.getString(titleKey));
		assistantAnswerBody.set(resourceBundle.getString(bodyKey));
		assistantStructuredResultVisible.set(false);
		assistantBrowseVisible.set(false);
		assistantAnswerVisible.set(true);
	}

	private void showChapter(String topicId, Button selectedButton, Node chapter) {
		assistantMode.set(false);
		if (topicId != null) {
			settings.learningCenterCompletedTopics.add(topicId);
		}
		chapterButtons().forEach(button -> button.getStyleClass().remove("learn-chapter-button-active"));
		if (!selectedButton.getStyleClass().contains("learn-chapter-button-active")) {
			selectedButton.getStyleClass().add("learn-chapter-button-active");
		}
		chapterNodes().forEach(node -> node.getStyleClass().remove("learn-content-card-active"));
		if (!chapter.getStyleClass().contains("learn-content-card-active")) {
			chapter.getStyleClass().add("learn-content-card-active");
		}
		scrollTo(chapter);
	}

	@FXML
	public void resetLearningProgress() {
		settings.learningCenterCompletedTopics.clear();
	}

	private void updateProgressDisplay() {
		Map<String, Button> buttonsByTopic = buttonsByTopic();
		buttonsByTopic.forEach((topicId, button) -> updateCompletionIcon(button, settings.learningCenterCompletedTopics.contains(topicId)));
		long completed = buttonsByTopic.keySet().stream().filter(settings.learningCenterCompletedTopics::contains).count();
		progressText.set("%d of %d topics viewed".formatted(completed, buttonsByTopic.size()));
		resetProgressDisabled.set(completed == 0);
	}

	private void updateCompletionIcon(Button button, boolean complete) {
		FontAwesome5IconView icon;
		if (button.getGraphic() instanceof FontAwesome5IconView existingIcon) {
			icon = existingIcon;
		} else {
			icon = new FontAwesome5IconView();
			icon.setGlyph(FontAwesome5Icon.CHECK);
			icon.setGlyphSize(13);
			icon.getStyleClass().add("learning-complete-icon");
			button.setGraphic(icon);
			button.setGraphicTextGap(8);
		}
		icon.setOpacity(complete ? 1.0 : 0.0);
		button.setAccessibleHelp(complete ? "Viewed topic" : "Not yet viewed");
	}

	private void filterTopics(String query) {
		String normalizedQuery = query == null ? "" : query.strip().toLowerCase(Locale.ROOT);
		long visibleTopics = chapterButtons().stream().filter(button -> {
			boolean matches = normalizedQuery.isEmpty() || button.getText().toLowerCase(Locale.ROOT).contains(normalizedQuery);
			button.setVisible(matches);
			button.setManaged(matches);
			return matches;
		}).count();
		noSearchResults.setVisible(visibleTopics == 0);
		noSearchResults.setManaged(visibleTopics == 0);
	}

	private List<Button> chapterButtons() {
		return List.of(howChapterButton, createChapterButton, recoveryChapterButton, cloudChapterButton, virtualDriveChapterButton, securityChapterButton, faqChapterButton, assistantChapterButton);
	}

	private List<Button> assistantCategoryButtons() {
		return List.of(assistantAllButton, assistantStartupButton, assistantVaultButton, assistantFilesystemButton, assistantRecoveryButton);
	}

	private Map<String, Button> buttonsByTopic() {
		return Map.of(TOPIC_HOW, howChapterButton, TOPIC_CREATE, createChapterButton, TOPIC_RECOVERY, recoveryChapterButton, TOPIC_CLOUD, cloudChapterButton, TOPIC_VIRTUAL_DRIVE, virtualDriveChapterButton, TOPIC_SECURITY, securityChapterButton, TOPIC_FAQ, faqChapterButton);
	}

	private List<Node> chapterNodes() {
		return List.of(howChapter, createChapter, recoveryChapter, cloudChapter, virtualDriveChapter, securityChapter, faqChapter, assistantChapter);
	}

	private void scrollTo(Node chapter) {
		Platform.runLater(() -> {
			Node content = contentScroll.getContent();
			double scrollableHeight = content.getBoundsInLocal().getHeight() - contentScroll.getViewportBounds().getHeight();
			if (scrollableHeight > 0) {
				double targetY = chapter.getBoundsInParent().getMinY() - CHAPTER_TOP_MARGIN;
				contentScroll.setVvalue(Math.clamp(targetY / scrollableHeight, 0.0, 1.0));
			}
		});
	}

	public ReadOnlyStringProperty progressTextProperty() {
		return progressText;
	}

	public String getProgressText() {
		return progressText.get();
	}

	public ReadOnlyBooleanProperty resetProgressDisabledProperty() {
		return resetProgressDisabled;
	}

	public boolean isResetProgressDisabled() {
		return resetProgressDisabled.get();
	}

	public ReadOnlyStringProperty assistantAnswerTitleProperty() {
		return assistantAnswerTitle;
	}

	public String getAssistantAnswerTitle() {
		return assistantAnswerTitle.get();
	}

	public ReadOnlyStringProperty assistantAnswerBodyProperty() {
		return assistantAnswerBody;
	}

	public String getAssistantAnswerBody() {
		return assistantAnswerBody.get();
	}

	public ReadOnlyBooleanProperty assistantAnswerVisibleProperty() {
		return assistantAnswerVisible;
	}

	public boolean isAssistantAnswerVisible() {
		return assistantAnswerVisible.get();
	}

	public ReadOnlyBooleanProperty assistantStructuredResultVisibleProperty() {
		return assistantStructuredResultVisible;
	}

	public boolean isAssistantStructuredResultVisible() {
		return assistantStructuredResultVisible.get();
	}

	public ReadOnlyBooleanProperty assistantModeProperty() {
		return assistantMode;
	}

	public boolean isAssistantMode() {
		return assistantMode.get();
	}

	public ReadOnlyBooleanProperty assistantBrowseVisibleProperty() {
		return assistantBrowseVisible;
	}

	public boolean isAssistantBrowseVisible() {
		return assistantBrowseVisible.get();
	}

	public ReadOnlyStringProperty assistantBrowseTitleProperty() {
		return assistantBrowseTitle;
	}

	public String getAssistantBrowseTitle() {
		return assistantBrowseTitle.get();
	}

	public ReadOnlyStringProperty assistantCaseLabelProperty() {
		return assistantCaseLabel;
	}

	public String getAssistantCaseLabel() {
		return assistantCaseLabel.get();
	}

	public ReadOnlyStringProperty assistantConfidenceProperty() {
		return assistantConfidence;
	}

	public String getAssistantConfidence() {
		return assistantConfidence.get();
	}

	public ReadOnlyStringProperty assistantCauseProperty() {
		return assistantCause;
	}

	public String getAssistantCause() {
		return assistantCause.get();
	}

	public ReadOnlyStringProperty assistantChecksProperty() {
		return assistantChecks;
	}

	public String getAssistantChecks() {
		return assistantChecks.get();
	}

	public ReadOnlyStringProperty assistantFixProperty() {
		return assistantFix;
	}

	public String getAssistantFix() {
		return assistantFix.get();
	}

	public ReadOnlyStringProperty assistantEvidenceProperty() {
		return assistantEvidence;
	}

	public String getAssistantEvidence() {
		return assistantEvidence.get();
	}
}
