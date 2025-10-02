using System;

namespace FileRelay.Core.Configuration;

/// <summary>
/// Represents a destination path and options for copy operations.
/// </summary>
public sealed class TargetConfiguration
{
    /// <summary>Internal identifier for referencing credentials.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Friendly name of the destination used in UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>UNC or mapped path to copy files to.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>Optional folder template appended to <see cref="DestinationPath"/>.</summary>
    public string? SubfolderTemplate { get; set; }

    /// <summary>Defines how conflicts should be handled.</summary>
    public ConflictMode ConflictMode { get; set; } = ConflictMode.Replace;

    /// <summary>Controls the maximum concurrency for this target.</summary>
    public int? MaxParallelTransfers { get; set; }

    /// <summary>Identifier of the credentials associated with this target.</summary>
    public Guid CredentialId { get; set; }

    /// <summary>Number of retries for this target (overrides global value).</summary>
    public int? MaxRetries { get; set; }

    /// <summary>Optional rate limit (files per minute) for this target.</summary>
    public int? MaxFilesPerMinute { get; set; }

    /// <summary>True if checksum verification is enabled for this target.</summary>
    public bool VerifyChecksum { get; set; } = true;
}

/// <summary>
/// Defines how file conflicts are handled.
/// </summary>
public enum ConflictMode
{
    /// <summary>Existing files are replaced.</summary>
    Replace,

    /// <summary>Existing files are preserved and new file is renamed with timestamp suffix.</summary>
    Version,

    /// <summary>Existing files block the transfer and the file is skipped.</summary>
    Ignore
}
