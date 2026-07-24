using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using VaultKind_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VaultKind_Windows;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private RectInt32 restoredBounds;
    private bool maximizeOnFirstActivation;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        RestoreWindowPlacement();
        AppWindow.Changed += TrackWindowPlacement;
        AppWindow.Closing += SaveWindowPlacement;
        Activated += ApplyInitialPresenterState;
        Activated += EnsureContentKeyboardFocus;

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    private void EnsureContentKeyboardFocus(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (RootFrame.Content is MainPage page)
            {
                page.EnsureKeyboardEntryPoint();
            }
        });
    }

    private void RestoreWindowPlacement()
    {
        if (!AppPreferencesStore.Load().RememberWindowPlacement)
        {
            AppWindow.Resize(new SizeInt32(1200, 800));
            restoredBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, 1200, 800);
            return;
        }

        WindowPlacement? saved = WindowPlacementStore.Load();
        if (saved is null)
        {
            AppWindow.Resize(new SizeInt32(1200, 800));
            restoredBounds = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, 1200, 800);
            return;
        }

        restoredBounds = WindowPlacementStore.MakeVisible(saved);
        AppWindow.MoveAndResize(restoredBounds);
        maximizeOnFirstActivation = saved.Maximized;
    }

    private void ApplyInitialPresenterState(object sender, WindowActivatedEventArgs args)
    {
        if (!maximizeOnFirstActivation || args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        maximizeOnFirstActivation = false;
        Activated -= ApplyInitialPresenterState;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private void TrackWindowPlacement(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            restoredBounds = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
        }
    }

    private void SaveWindowPlacement(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!AppPreferencesStore.Load().RememberWindowPlacement)
        {
            return;
        }

        bool maximized = sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        WindowPlacementStore.Save(restoredBounds, maximized);
    }
}
