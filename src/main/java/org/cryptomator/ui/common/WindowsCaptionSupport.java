package org.cryptomator.ui.common;

import org.apache.commons.lang3.SystemUtils;

import java.lang.foreign.Arena;
import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.SymbolLookup;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;

/** Applies VaultKind's dark appearance to the genuine Windows caption. */
public final class WindowsCaptionSupport {

	private static final int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
	private static final int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
	private static final Linker LINKER = Linker.nativeLinker();
	private static final SymbolLookup USER32 = SystemUtils.IS_OS_WINDOWS ? SymbolLookup.libraryLookup("user32", Arena.global()) : null;
	private static final SymbolLookup DWMAPI = SystemUtils.IS_OS_WINDOWS ? SymbolLookup.libraryLookup("dwmapi", Arena.global()) : null;
	private static final MethodHandle GET_FOREGROUND_WINDOW = downcall("GetForegroundWindow", FunctionDescriptor.of(ValueLayout.ADDRESS));
	private static final MethodHandle DWM_SET_WINDOW_ATTRIBUTE = downcall(DWMAPI, "DwmSetWindowAttribute", FunctionDescriptor.of(ValueLayout.JAVA_INT, ValueLayout.ADDRESS, ValueLayout.JAVA_INT, ValueLayout.ADDRESS, ValueLayout.JAVA_INT));

	private WindowsCaptionSupport() {
	}

	public static boolean applyDarkTitleBar() {
		if (!SystemUtils.IS_OS_WINDOWS || GET_FOREGROUND_WINDOW == null || DWM_SET_WINDOW_ATTRIBUTE == null) {
			return false;
		}
		try (Arena arena = Arena.ofConfined()) {
			MemorySegment windowHandle = (MemorySegment) GET_FOREGROUND_WINDOW.invokeExact();
			if (windowHandle.equals(MemorySegment.NULL)) {
				return false;
			}
			MemorySegment enabled = arena.allocate(ValueLayout.JAVA_INT);
			enabled.set(ValueLayout.JAVA_INT, 0, 1);
			int result = (int) DWM_SET_WINDOW_ATTRIBUTE.invokeExact(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE, enabled, (int) ValueLayout.JAVA_INT.byteSize());
			if (result != 0) {
				result = (int) DWM_SET_WINDOW_ATTRIBUTE.invokeExact(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, enabled, (int) ValueLayout.JAVA_INT.byteSize());
			}
			return result == 0;
		} catch (Throwable e) {
			return false;
		}
	}

	private static MethodHandle downcall(String name, FunctionDescriptor descriptor) {
		return downcall(USER32, name, descriptor);
	}

	private static MethodHandle downcall(SymbolLookup lookup, String name, FunctionDescriptor descriptor) {
		if (lookup == null) {
			return null;
		}
		return lookup.find(name).map(symbol -> LINKER.downcallHandle(symbol, descriptor)).orElse(null);
	}
}
