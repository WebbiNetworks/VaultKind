package org.cryptomator.updater;

import org.cryptomator.integrations.common.LocalizedDisplayName;
import org.cryptomator.integrations.update.UpdateMechanism;
import org.cryptomator.integrations.update.UpdateStep;
import org.cryptomator.ui.fxapp.FxApplicationScoped;
import org.cryptomator.ui.common.VaultKindUrls;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.inject.Inject;
import javafx.application.Application;
import javafx.application.Platform;
import java.net.http.HttpClient;

@FxApplicationScoped
@LocalizedDisplayName(bundle = "i18n.strings", key = "preferences.updates.visitDownloadPage")
public class FallbackUpdateMechanism implements UpdateMechanism<FallbackUpdateInfo> {

	private static final Logger LOG = LoggerFactory.getLogger(FallbackUpdateMechanism.class);
	private static final String DOWNLOADS_URI_TEMPLATE = VaultKindUrls.HOME;

	private final Application app;

	@Inject
	public FallbackUpdateMechanism(Application app) {
		this.app = app;
	}

	@Override
	public FallbackUpdateInfo checkForUpdate(String currentVersion, HttpClient httpClient) {
		LOG.debug("VaultKind update checks are disabled until a signed VaultKind release channel is available.");
		return null;
	}

	@Override
	public UpdateStep firstStep(FallbackUpdateInfo updateInfo) {
		return UpdateStep.of("Go to download page", this::openDownloadPage); // TODO localize
	}

	private UpdateStep openDownloadPage() {
		Platform.runLater(() -> {
			app.getHostServices().showDocument(DOWNLOADS_URI_TEMPLATE);
		});
		return UpdateStep.RETRY; // allow running this "update mechanism" as many times as the user wants
	}

}
