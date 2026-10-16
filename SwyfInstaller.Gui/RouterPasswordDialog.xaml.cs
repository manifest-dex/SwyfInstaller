using System.Windows;

namespace SwyfInstaller.Gui;

public partial class RouterPasswordDialog : Window
{
    public string DashboardPassword => PasswordBox.Password ?? "";

    public RouterPasswordDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(DashboardPassword))
        {
            ErrorText.Text = "Enter the current dashboard password first.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }
}
