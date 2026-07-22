package org.cryptomator.ui.common;

import javafx.animation.Interpolator;
import javafx.animation.KeyFrame;
import javafx.animation.KeyValue;
import javafx.animation.Timeline;
import javafx.scene.Node;
import javafx.scene.Parent;
import javafx.scene.control.Button;
import javafx.scene.control.ButtonBase;
import javafx.scene.control.Hyperlink;
import javafx.util.Duration;

public final class MicroInteractionSupport {

	private static final String HOVER_ANIMATION_KEY = "vaultkind-hover-animation";
	private static final String PRESS_ANIMATION_KEY = "vaultkind-press-animation";
	private static final String INSTALLED_KEY = "vaultkind-micro-interaction-installed";

	private MicroInteractionSupport() {
	}

	public static void install(Node node) {
		if (node instanceof ButtonBase control && (control instanceof Button || control instanceof Hyperlink) && !control.getStyleClass().contains("add-vault-window-button") && !control.getProperties().containsKey(INSTALLED_KEY)) {
			control.getProperties().put(INSTALLED_KEY, true);
			control.getStyleClass().add(control instanceof Hyperlink ? "micro-link-interaction" : "micro-interaction");
			var lift = control.getStyleClass().contains("welcome-learn-more") ? -3.0 : control instanceof Hyperlink ? -1.5 : control.getStyleClass().contains("add-vault-choice") ? -3.0 : -2.0;
			control.hoverProperty().addListener((_, _, hovered) -> animateHover(control, hovered && !control.isDisabled() ? lift : 0.0));
			control.armedProperty().addListener((_, _, armed) -> animatePress(control, armed ? 0.985 : 1.0));
			control.disabledProperty().addListener((_, _, disabled) -> {
				if (disabled) {
					animateHover(control, 0.0);
					animatePress(control, 1.0);
				}
			});
		}
		if (node instanceof Parent parent) {
			parent.getChildrenUnmodifiable().forEach(MicroInteractionSupport::install);
		}
	}

	private static void animateHover(Node node, double targetY) {
		animate(node, HOVER_ANIMATION_KEY, 125, //
				new KeyValue(node.translateYProperty(), targetY, Interpolator.EASE_BOTH));
	}

	private static void animatePress(Node node, double targetScale) {
		animate(node, PRESS_ANIMATION_KEY, targetScale < 1.0 ? 65 : 115, //
				new KeyValue(node.scaleXProperty(), targetScale, Interpolator.EASE_BOTH), //
				new KeyValue(node.scaleYProperty(), targetScale, Interpolator.EASE_BOTH));
	}

	private static void animate(Node node, String key, double durationMillis, KeyValue... keyValues) {
		var properties = node.getProperties();
		var previous = properties.get(key);
		if (previous instanceof Timeline timeline) {
			timeline.stop();
		}
		var animation = new Timeline(new KeyFrame(Duration.millis(durationMillis), keyValues));
		properties.put(key, animation);
		animation.setOnFinished(_ -> properties.remove(key, animation));
		animation.play();
	}
}
