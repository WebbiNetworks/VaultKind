namespace VaultKind_Windows.Services;

public enum KeyboardNavigationCommand
{
    None,
    Previous,
    Next,
    First,
    Last
}

public static class KeyboardNavigationPolicy
{
    public static int ResolveNextIndex(int currentIndex, int itemCount, KeyboardNavigationCommand command)
    {
        if (currentIndex < 0 || currentIndex >= itemCount || itemCount <= 0)
        {
            return -1;
        }

        return command switch
        {
            KeyboardNavigationCommand.Previous => (currentIndex + itemCount - 1) % itemCount,
            KeyboardNavigationCommand.Next => (currentIndex + 1) % itemCount,
            KeyboardNavigationCommand.First => 0,
            KeyboardNavigationCommand.Last => itemCount - 1,
            _ => -1
        };
    }
}
