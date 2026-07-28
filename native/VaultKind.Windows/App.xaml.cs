using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Windowing;
using VaultKind_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VaultKind_Windows;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private readonly JavaVaultEngineHost engineHost = new();
    private bool shutdownStarted;
    private bool shutdownCompleted;

    internal Window? MainWindow => _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        StartupTiming.Mark("App constructor entered");
        RequestedTheme = string.Equals(AppPreferencesStore.Load().AppearanceMode, "light", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;
        StartupTiming.Mark("Application preferences loaded");
        InitializeComponent();
        StartupTiming.Mark("Application XAML initialized");
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupTiming.Mark("OnLaunched entered");
        engineHost.StartIfNeeded();
        StartupTiming.Mark("Engine host start request returned");
        _window = new MainWindow();
        StartupTiming.Mark("Main window constructed");
        _window.AppWindow.Closing += OnMainWindowClosing;
        _window.Closed += OnMainWindowClosed;
        _window.Activate();
        StartupTiming.Mark("Main window activated");
    }

    private void OnMainWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (shutdownCompleted)
        {
            return;
        }

        args.Cancel = true;
        if (shutdownStarted)
        {
            return;
        }

        shutdownStarted = true;
        _ = CompleteShutdownAsync();
    }

    private async Task CompleteShutdownAsync()
    {
        await Task.Run(engineHost.Dispose);
        shutdownCompleted = true;
        _window?.Close();
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        _window = null;
    }
}
