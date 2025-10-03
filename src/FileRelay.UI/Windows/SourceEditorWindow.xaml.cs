using System.Windows;

namespace FileRelay.UI.Windows;

public partial class SourceEditorWindow : Window
{
    public SourceEditorWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SourceEditorViewModel viewModel && !viewModel.TryValidateTargets(out var errorMessage))
        {
            MessageBox.Show(this, errorMessage, "Ungültige Konfiguration", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }
}
