using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileRelay.Core.Configuration;

namespace FileRelay.UI.ViewModels;

public partial class SourceItemViewModel : ObservableObject
{
    public SourceItemViewModel()
    {
        Targets = new ObservableCollection<TargetItemViewModel>();
    }

    [ObservableProperty]
    private Guid id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string path = string.Empty;

    [ObservableProperty]
    private bool enabled = true;

    [ObservableProperty]
    private bool recursive = true;

    [ObservableProperty]
    private bool deleteAfterCopy;

    [ObservableProperty]
    private bool useRecycleBin;

    [ObservableProperty]
    private DateTimeOffset? lastActivityUtc;

    [ObservableProperty]
    private int pendingTransfers;

    public ObservableCollection<TargetItemViewModel> Targets { get; }

    public SourceConfiguration ToConfiguration()
    {
        return new SourceConfiguration
        {
            Id = Id == Guid.Empty ? Guid.NewGuid() : Id,
            Name = Name,
            Path = Path,
            Enabled = Enabled,
            Recursive = Recursive,
            DeleteAfterCopy = DeleteAfterCopy,
            UseRecycleBin = UseRecycleBin,
            Targets = Targets.Select(t => t.ToConfiguration()).ToList()
        };
    }

    public static SourceItemViewModel FromConfiguration(SourceConfiguration configuration, DateTimeOffset? lastActivity, int queue)
    {
        var vm = new SourceItemViewModel
        {
            Id = configuration.Id,
            Name = configuration.Name,
            Path = configuration.Path,
            Enabled = configuration.Enabled,
            Recursive = configuration.Recursive,
            DeleteAfterCopy = configuration.DeleteAfterCopy,
            UseRecycleBin = configuration.UseRecycleBin,
            LastActivityUtc = lastActivity,
            PendingTransfers = queue
        };

        foreach (var target in configuration.Targets)
        {
            vm.Targets.Add(TargetItemViewModel.FromConfiguration(target, string.Empty));
        }

        return vm;
    }

    public SourceItemViewModel Clone()
    {
        var clone = new SourceItemViewModel
        {
            Id = Id,
            Name = Name,
            Path = Path,
            Enabled = Enabled,
            Recursive = Recursive,
            DeleteAfterCopy = DeleteAfterCopy,
            UseRecycleBin = UseRecycleBin,
            LastActivityUtc = LastActivityUtc,
            PendingTransfers = PendingTransfers
        };

        foreach (var target in Targets)
        {
            var copy = new TargetItemViewModel
            {
                Id = target.Id,
                Name = target.Name,
                DestinationPath = target.DestinationPath,
                CredentialId = target.CredentialId,
                CredentialName = target.CredentialName,
                ConflictMode = target.ConflictMode,
                VerifyChecksum = target.VerifyChecksum,
                SubfolderTemplate = target.SubfolderTemplate,
                MaxParallelTransfers = target.MaxParallelTransfers,
                MaxRetries = target.MaxRetries,
                MaxFilesPerMinute = target.MaxFilesPerMinute
            };
            clone.Targets.Add(copy);
        }

        return clone;
    }

    public void UpdateFrom(SourceItemViewModel other)
    {
        Name = other.Name;
        Path = other.Path;
        Enabled = other.Enabled;
        Recursive = other.Recursive;
        DeleteAfterCopy = other.DeleteAfterCopy;
        UseRecycleBin = other.UseRecycleBin;
        Targets.Clear();
        foreach (var target in other.Targets)
        {
            var copy = new TargetItemViewModel
            {
                Id = target.Id,
                Name = target.Name,
                DestinationPath = target.DestinationPath,
                CredentialId = target.CredentialId,
                CredentialName = target.CredentialName,
                ConflictMode = target.ConflictMode,
                VerifyChecksum = target.VerifyChecksum,
                SubfolderTemplate = target.SubfolderTemplate,
                MaxParallelTransfers = target.MaxParallelTransfers,
                MaxRetries = target.MaxRetries,
                MaxFilesPerMinute = target.MaxFilesPerMinute
            };
            Targets.Add(copy);
        }
    }
}
