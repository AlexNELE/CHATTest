using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileRelay.Core.Configuration;
using FileRelay.UI.ViewModels;

namespace FileRelay.UI.Windows;

public partial class SourceEditorViewModel : ObservableObject
{
    public SourceEditorViewModel(SourceItemViewModel source, IEnumerable<CredentialReference> credentials)
    {
        Source = source;
        Credentials = new ObservableCollection<CredentialReference>(credentials);
        SelectedTarget = Source.Targets.FirstOrDefault();
    }

    public SourceItemViewModel Source { get; }

    public ObservableCollection<CredentialReference> Credentials { get; }

    [ObservableProperty]
    private TargetItemViewModel? selectedTarget;

    [RelayCommand]
    private void AddTarget()
    {
        var target = new TargetItemViewModel
        {
            Name = "Neues Ziel",
            ConflictMode = ConflictMode.Replace,
            VerifyChecksum = true
        };
        if (Credentials.Any())
        {
            var credential = Credentials.First();
            target.CredentialId = credential.Id;
            target.CredentialName = credential.DisplayName;
        }
        Source.Targets.Add(target);
        SelectedTarget = target;
    }

    [RelayCommand]
    private void RemoveTarget()
    {
        if (SelectedTarget == null)
        {
            return;
        }

        Source.Targets.Remove(SelectedTarget);
        SelectedTarget = Source.Targets.FirstOrDefault();
    }
}
