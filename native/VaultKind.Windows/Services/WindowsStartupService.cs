using Microsoft.Win32;

namespace VaultKind_Windows.Services;

internal static class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VaultKind";

    internal static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            string? registeredCommand = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(registeredCommand);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static bool TrySetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            string? executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
