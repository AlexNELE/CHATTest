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
        DialogResult = true;
        Close();
    }
}
