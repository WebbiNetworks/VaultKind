package org.cryptomator.ui.error;

import org.cryptomator.common.Environment;
import org.cryptomator.common.ErrorCode;
import org.cryptomator.common.Nullable;
import org.cryptomator.ui.common.FxController;
import org.cryptomator.ui.common.VaultKindUrls;

import javax.inject.Inject;
import javax.inject.Named;
import javafx.application.Application;
import javafx.application.Platform;
import javafx.beans.binding.BooleanExpression;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.ReadOnlyStringProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.beans.property.SimpleStringProperty;
import javafx.beans.property.StringProperty;
import javafx.fxml.FXML;
import javafx.scene.Scene;
import javafx.scene.input.Clipboard;
import javafx.scene.input.ClipboardContent;
import javafx.stage.Stage;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.Locale;
import java.util.ResourceBundle;

public class ErrorController implements FxController {

	private static final String SEARCH_URL_FORMAT = VaultKindUrls.GITHUB_ISSUES + "?q=%s";
	private static final String REPORT_URL_FORMAT = VaultKindUrls.GITHUB_ISSUES + "/new?title=Error+%s&body=%s";
	private static final String SEARCH_ERRORCODE_DELIM = " OR ";
	private static final String REPORT_BODY_TEMPLATE = """
			<!-- 💚 Thank you for reporting this error. -->
			OS: %s / %s
			App: %s / %s
			
			<!-- ✏ Please describe what happened as accurately as possible. -->
			Description:
				
			<!-- 📋 Please also copy and paste the details from the error window. -->
			Details:
			
			<!-- ❗ If the description or the detail text is missing, the discussion will be deleted. -->
			""";

	private final Application application;
	private final String stackTrace;
	private final ErrorCode errorCode;
	private final Scene previousScene;
	private final Stage window;
	private final Environment environment;
	private final ResourceBundle resourceBundle;

	private final BooleanProperty copiedDetails = new SimpleBooleanProperty();
	private final ObjectProperty<ErrorDiscussion> matchingErrorDiscussion = new SimpleObjectProperty<>();
	private final BooleanExpression errorSolutionFound = matchingErrorDiscussion.isNotNull();
	private final BooleanProperty isLoadingHttpResponse = new SimpleBooleanProperty();
	private final BooleanProperty askedForLookupDatabasePermission = new SimpleBooleanProperty();
	private final StringProperty localGuidanceTitle = new SimpleStringProperty();
	private final StringProperty localGuidanceBody = new SimpleStringProperty();
	private final boolean formerSceneWasResizable;

	@Inject
	ErrorController(Application application, @Named("stackTrace") String stackTrace, ErrorCode errorCode, @Nullable Scene previousScene, Stage window, Environment environment, ExecutorService executorService, ResourceBundle resourceBundle) {
		this.application = application;
		this.stackTrace = stackTrace;
		this.errorCode = errorCode;
		this.previousScene = previousScene;
		this.window = window;
		this.environment = environment;
		this.resourceBundle = resourceBundle;
		this.formerSceneWasResizable = window.isResizable();
	}

	@FXML
	public void back() {
		if (previousScene != null) {
			window.setScene(previousScene);
			window.setResizable(formerSceneWasResizable);
		}
	}

	@FXML
	public void close() {
		window.close();
	}

	@FXML
	public void showSolution() {
		if (matchingErrorDiscussion.isNotNull().get()) {
			var discussion = matchingErrorDiscussion.get();
			if (discussion != null) {
				application.getHostServices().showDocument(discussion.url);
			}
		}
	}

	@FXML
	public void searchError() {
		var searchTerm = URLEncoder.encode(getErrorCode().replace(ErrorCode.DELIM, SEARCH_ERRORCODE_DELIM), StandardCharsets.UTF_8);
		application.getHostServices().showDocument(SEARCH_URL_FORMAT.formatted(searchTerm));
	}

	@FXML
	public void reportError() {
		var title = URLEncoder.encode(getErrorCode(), StandardCharsets.UTF_8);
		var enhancedTemplate = String.format(REPORT_BODY_TEMPLATE, //
				System.getProperty("os.name"), //
				System.getProperty("os.version"), //
				environment.getAppVersion(), //
				environment.getBuildNumber().orElse("undefined"));
		var body = URLEncoder.encode(enhancedTemplate, StandardCharsets.UTF_8);
		application.getHostServices().showDocument(REPORT_URL_FORMAT.formatted(title, body));
	}

	@FXML
	public void copyDetails() {
		ClipboardContent clipboardContent = new ClipboardContent();
		clipboardContent.putString(getDetailText());
		Clipboard.getSystemClipboard().setContent(clipboardContent);

		copiedDetails.set(true);
		CompletableFuture.delayedExecutor(2, TimeUnit.SECONDS, Platform::runLater).execute(() -> copiedDetails.set(false));
	}

	@FXML
	public void dismiss() {
		askedForLookupDatabasePermission.set(true);
	}

	@FXML
	public void lookUpSolution() {
		askedForLookupDatabasePermission.set(true);
		String details = (getErrorCode() + " " + stackTrace).toLowerCase(Locale.ROOT);
		if (details.contains("bts9:kt9r:kt9r") || details.contains("nosuchelementexception") || details.contains("mounter") || details.contains("mount")) {
			setLocalGuidance("error.local.mount.title", "error.local.mount.body");
		} else if (details.contains("accessdenied") || details.contains("in use") || details.contains("busy") || details.contains("filesystemexception")) {
			setLocalGuidance("error.local.busy.title", "error.local.busy.body");
		} else if (details.contains("nosuchfile") || details.contains("not found") || details.contains("missing")) {
			setLocalGuidance("error.local.missing.title", "error.local.missing.body");
		} else {
			setLocalGuidance("error.local.generic.title", "error.local.generic.body");
		}
	}

	private void setLocalGuidance(String titleKey, String bodyKey) {
		localGuidanceTitle.set(resourceBundle.getString(titleKey));
		localGuidanceBody.set(resourceBundle.getString(bodyKey).formatted(getErrorCode()));
	}

	/**
	 * Checks if an ErrorDiscussion object's title contains the error code's method code.
	 *
	 * @param errorDiscussion The ErrorDiscussion object to be checked.
	 * @return A boolean value indicating if the ErrorDiscussion object's title contains the error code's method code:
	 * - true if the title contains the method code,
	 * - false otherwise.
	 */
	public boolean containsMethodCode(ErrorDiscussion errorDiscussion) {
		return errorDiscussion.title.contains(" " + errorCode.methodCode());
	}

	/**
	 * Compares two ErrorDiscussion objects based on their upvote counts and returns the result.
	 *
	 * @param ed1 The first ErrorDiscussion object.
	 * @param ed2 The second ErrorDiscussion object.
	 * @return An integer indicating which ErrorDiscussion object has a higher upvote count:
	 * - A positive value if ed2 has a higher upvote count than ed1,
	 * - A negative value if ed1 has a higher upvote count than ed2,
	 * - Or 0 if both upvote counts are equal.
	 */
	public int compareUpvoteCount(ErrorDiscussion ed1, ErrorDiscussion ed2) {
		return Integer.compare(ed2.upvoteCount, ed1.upvoteCount);
	}

	/**
	 * Compares two ErrorDiscussion objects based on their answered status and returns the result.
	 *
	 * @param ed1 The first ErrorDiscussion object.
	 * @param ed2 The second ErrorDiscussion object.
	 * @return An integer indicating the answered status of the ErrorDiscussion objects:
	 * - A negative value (-1) if ed1 is considered answered and ed2 is considered unanswered,
	 * - A positive value (1) if ed1 is considered unanswered and ed2 is considered answered,
	 * - Or 0 if both ErrorDiscussion objects are considered answered or unanswered.
	 */
	public int compareIsAnswered(ErrorDiscussion ed1, ErrorDiscussion ed2) {
		if (ed1.answer != null && ed2.answer == null) {
			return -1;
		} else if (ed1.answer == null && ed2.answer != null) {
			return 1;
		} else {
			return 0;
		}
	}

	/**
	 * Compares two ErrorDiscussion objects based on the presence of the full error code in their titles and returns the result.
	 *
	 * @param ed1 The first ErrorDiscussion object.
	 * @param ed2 The second ErrorDiscussion object.
	 * @return An integer indicating the comparison result based on the presence of the full error code in the titles:
	 * - A negative value (-1) if ed1 contains the full error code in the title and ed2 does not have a match,
	 * - A positive value (1) if ed1 does not have a match and ed2 contains the full error code in the title,
	 * - Or 0 if both ErrorDiscussion objects either contain the full error code or do not have a match in the titles.
	 */
	public int compareByFullErrorCode(ErrorDiscussion ed1, ErrorDiscussion ed2) {
		if (ed1.title.contains(getErrorCode()) && !ed2.title.contains(getErrorCode())) {
			return -1;
		} else if (!ed1.title.contains(getErrorCode()) && ed2.title.contains(getErrorCode())) {
			return 1;
		} else {
			return 0;
		}
	}

	/**
	 * Compares two ErrorDiscussion objects based on the presence of the root cause code in their titles and returns the result.
	 *
	 * @param ed1 The first ErrorDiscussion object.
	 * @param ed2 The second ErrorDiscussion object.
	 * @return An integer indicating the comparison result based on the presence of the root cause code in the titles:
	 * - A negative value (-1) if ed1 contains the root cause code in the title and ed2 does not have a match,
	 * - A positive value (1) if ed1 does not have a match and ed2 contains the root cause code in the title,
	 * - Or 0 if both ErrorDiscussion objects either contain the root cause code or do not have a match in the titles.
	 */
	public int compareByRootCauseCode(ErrorDiscussion ed1, ErrorDiscussion ed2) {
		String value = " " + errorCode.methodCode() + ErrorCode.DELIM + errorCode.rootCauseCode();
		if (ed1.title.contains(value) && !ed2.title.contains(value)) {
			return -1;
		} else if (!ed1.title.contains(value) && ed2.title.contains(value)) {
			return 1;
		} else {
			return 0;
		}
	}

	/* Getter/Setter */
	public boolean isPreviousScenePresent() {
		return previousScene != null;
	}

	public String getStackTrace() {
		return stackTrace;
	}

	public String getErrorCode() {
		return errorCode.toString();
	}

	public String getDetailText() {
		return "```\nError Code " + getErrorCode() + "\n" + getStackTrace() + "\n```";
	}

	public BooleanProperty copiedDetailsProperty() {
		return copiedDetails;
	}

	public boolean getCopiedDetails() {
		return copiedDetails.get();
	}

	public BooleanExpression errorSolutionFoundProperty() {
		return errorSolutionFound;
	}

	public boolean getErrorSolutionFound() {
		return errorSolutionFound.get();
	}

	public BooleanProperty isLoadingHttpResponseProperty() {
		return isLoadingHttpResponse;
	}

	public boolean getIsLoadingHttpResponse() {
		return isLoadingHttpResponse.get();
	}

	public BooleanProperty askedForLookupDatabasePermissionProperty() {
		return askedForLookupDatabasePermission;
	}

	public boolean getAskedForLookupDatabasePermission() {
		return askedForLookupDatabasePermission.get();
	}

	public ReadOnlyStringProperty localGuidanceTitleProperty() {
		return localGuidanceTitle;
	}

	public String getLocalGuidanceTitle() {
		return localGuidanceTitle.get();
	}

	public ReadOnlyStringProperty localGuidanceBodyProperty() {
		return localGuidanceBody;
	}

	public String getLocalGuidanceBody() {
		return localGuidanceBody.get();
	}

}
