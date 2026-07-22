package org.cryptomator.common.settings;

import com.fasterxml.jackson.annotation.JsonEnumDefaultValue;
import com.fasterxml.jackson.annotation.JsonFormat;
@JsonFormat(shape = JsonFormat.Shape.STRING)
public enum UiTheme {
	@JsonEnumDefaultValue LIGHT("preferences.interface.theme.light"), //
	DARK("preferences.interface.theme.dark"), //
	AUTOMATIC("preferences.interface.theme.automatic");

	private final String displayName;

	UiTheme(String displayName) {
		this.displayName = displayName;
	}

	public String getDisplayName() {
		return displayName;
	}

}
