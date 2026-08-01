package org.cryptomator.ui.recoverykey;

import org.cryptomator.ui.common.FxController;

import javax.inject.Inject;
import javafx.fxml.FXML;
import javafx.stage.Stage;

@RecoveryKeyScoped
public class RecoveryKeySuccessController implements FxController {

	private final Stage window;
	private Runnable embeddedDoneAction;

	@Inject
	public RecoveryKeySuccessController(@RecoveryKeyWindow Stage window) {
		this.window = window;
	}

	@FXML
	public void close() {
		if (embeddedDoneAction != null) {
			embeddedDoneAction.run();
		} else {
			window.close();
		}
	}

	public void prepareEmbedded(Runnable doneAction) {
		embeddedDoneAction = doneAction;
	}

	public void cleanup() {
		embeddedDoneAction = null;
	}

}
