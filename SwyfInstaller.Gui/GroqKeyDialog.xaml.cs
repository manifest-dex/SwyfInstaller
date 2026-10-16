using System.Windows;

namespace SwyfInstaller.Gui;

public partial class GroqKeyDialog : Window
{
    public string ApiKey => KeyBox.Password?.Trim() ?? "";
    public string DashboardPassword => string.IsNullOrWhiteSpace(DashboardBox.Password) ? "123456" : DashboardBox.Password;

    public GroqKeyDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => KeyBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorText.Text = "Paste a key starting with gsk_ first.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { }
        e.Handled = true;
    }
}
