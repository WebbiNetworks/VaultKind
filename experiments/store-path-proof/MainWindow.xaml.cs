using Microsoft.UI.Xaml;

namespace StorePathProof;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Windows.ApplicationModel.PackageVersion version = Windows.ApplicationModel.Package.Current.Id.Version;
        VersionText.Text = $"Installed Store version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 560));
        AppWindow.SetIcon("Assets/AppIcon.ico");
    }
}
