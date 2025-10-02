using CommunityToolkit.Mvvm.ComponentModel;

namespace FileRelay.UI.ViewModels;

public partial class LogEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTimeOffset timestamp;

    [ObservableProperty]
    private string level = string.Empty;

    [ObservableProperty]
    private string message = string.Empty;
}
