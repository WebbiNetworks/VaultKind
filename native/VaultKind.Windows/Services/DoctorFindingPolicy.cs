namespace VaultKind_Windows.Services;

internal static class DoctorFindingPolicy
{
    internal static bool IsCritical(string kind, string? assistantCaseId) =>
        kind == "attention"
        && assistantCaseId is "VK-1003" or "VK-3002";
}
