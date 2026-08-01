package org.cryptomator.ui.mainwindow;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class DiagnosticCatalogTest {

	@Test
	void containsInitialCuratedCases() {
		assertEquals(13, DiagnosticCatalog.all().size());
	}

	@Test
	void exactKnownErrorProducesStrongMatch() {
		var match = DiagnosticCatalog.findBestMatch("BTS9:KT9R:KT9R").orElseThrow();
		assertEquals("VK-2005", match.diagnosticCase().id());
		assertEquals(DiagnosticCase.Confidence.STRONG, match.confidence());
	}

	@Test
	void symptomProducesRelevantMatch() {
		var match = DiagnosticCatalog.findBestMatch("my vault folder moved").orElseThrow();
		assertEquals("VK-1002", match.diagnosticCase().id());
	}

	@Test
	void unknownDescriptionDoesNotPretendToKnow() {
		assertTrue(DiagnosticCatalog.findBestMatch("purple elephants dancing").isEmpty());
	}
}
