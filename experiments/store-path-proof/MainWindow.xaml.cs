using Microsoft.UI.Xaml;

namespace StorePathProof;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 560));
        AppWindow.SetIcon("Assets/AppIcon.ico");
    }
}
