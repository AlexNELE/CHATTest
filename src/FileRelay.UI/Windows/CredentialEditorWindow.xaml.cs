using System.Windows;

namespace FileRelay.UI.Windows;

public partial class CredentialEditorWindow : Window
{
    public CredentialEditorWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CredentialEditorViewModel viewModel)
        {
            viewModel.SetPassword(PasswordBox.SecurePassword);
            if (!viewModel.Validate(out var error))
            {
                MessageBox.Show(error, "Ungültige Eingabe", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        DialogResult = true;
        Close();
    }
}
