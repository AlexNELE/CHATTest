using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FileRelay.Core.Configuration;

namespace FileRelay.UI.ViewModels;

public partial class TargetItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string destinationPath = string.Empty;

    [ObservableProperty]
    private string credentialName = string.Empty;

    [ObservableProperty]
    private Guid credentialId;

    [ObservableProperty]
    private ConflictMode conflictMode;

    [ObservableProperty]
    private bool verifyChecksum = true;

    [ObservableProperty]
    private string? subfolderTemplate;

    [ObservableProperty]
    private int? maxParallelTransfers;

    [ObservableProperty]
    private int? maxRetries;

    [ObservableProperty]
    private int? maxFilesPerMinute;

    public TargetConfiguration ToConfiguration() => new()
    {
        Id = Id == Guid.Empty ? Guid.NewGuid() : Id,
        Name = Name,
        DestinationPath = DestinationPath,
        CredentialId = CredentialId,
        ConflictMode = ConflictMode,
        VerifyChecksum = VerifyChecksum,
        SubfolderTemplate = SubfolderTemplate,
        MaxParallelTransfers = MaxParallelTransfers,
        MaxRetries = MaxRetries,
        MaxFilesPerMinute = MaxFilesPerMinute
    };

    public static TargetItemViewModel FromConfiguration(TargetConfiguration configuration, string credentialName)
        => new()
        {
            Id = configuration.Id,
            Name = configuration.Name,
            DestinationPath = configuration.DestinationPath,
            CredentialId = configuration.CredentialId,
            CredentialName = credentialName,
            ConflictMode = configuration.ConflictMode,
            VerifyChecksum = configuration.VerifyChecksum,
            SubfolderTemplate = configuration.SubfolderTemplate,
            MaxParallelTransfers = configuration.MaxParallelTransfers,
            MaxRetries = configuration.MaxRetries,
            MaxFilesPerMinute = configuration.MaxFilesPerMinute
        };
}
