using Wpf.Ui.Controls;
using SwyfInstaller.Gui.ViewModels;

namespace SwyfInstaller.Gui;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    private void LogBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        LogBox.ScrollToEnd();
    }
}
