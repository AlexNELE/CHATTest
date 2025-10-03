using System.Windows;

namespace FileRelay.UI.Windows;

public partial class ManageCredentialsWindow : Window
{
    public ManageCredentialsWindow()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
