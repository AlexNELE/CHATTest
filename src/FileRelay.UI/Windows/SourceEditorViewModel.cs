using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileRelay.Core.Configuration;
using FileRelay.UI.ViewModels;

namespace FileRelay.UI.Windows;

public partial class SourceEditorViewModel : ObservableObject
{
    public SourceEditorViewModel(SourceItemViewModel source, ObservableCollection<CredentialReference> credentials)
    {
        Source = source;
        Credentials = credentials;
        SelectedTarget = Source.Targets.FirstOrDefault();
    }

    public SourceItemViewModel Source { get; }

    public ObservableCollection<CredentialReference> Credentials { get; }

    [ObservableProperty]
    private TargetItemViewModel? selectedTarget;

    public bool TryValidateTargets(out string? errorMessage)
    {
        foreach (var target in Source.Targets)
        {
            if (RequiresCredentials(target) && target.CredentialId == Guid.Empty)
            {
                var targetName = string.IsNullOrWhiteSpace(target.Name) ? target.DestinationPath : target.Name;
                errorMessage = $"Für das Ziel \"{targetName}\" werden Credentials benötigt, da der Pfad \"{target.DestinationPath}\" auf einen anderen Computer zeigt.";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    private static bool RequiresCredentials(TargetItemViewModel target)
    {
        var path = target.DestinationPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsUnc)
        {
            if (IsLocalHost(uri.Host))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool IsLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

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
