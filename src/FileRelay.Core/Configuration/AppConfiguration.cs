using System;
using System.Collections.Generic;

namespace FileRelay.Core.Configuration;

/// <summary>
/// Root configuration container persisted to disk.
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>Gets or sets global options.</summary>
    public GlobalOptions Options { get; set; } = new();

    /// <summary>Collection of source directories that are monitored.</summary>
    public IList<SourceConfiguration> Sources { get; set; } = new List<SourceConfiguration>();

    /// <summary>Collection of credential definitions referenced by targets.</summary>
    public IList<CredentialReference> Credentials { get; set; } = new List<CredentialReference>();
}

/// <summary>
/// Defines application wide defaults.
/// </summary>
public sealed class GlobalOptions
{
    public int DefaultMaxParallelTransfers { get; set; } = Environment.ProcessorCount;

    public int DefaultRetryCount { get; set; } = 3;

    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public string LogDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FileRelay", "logs");

    public bool StartMinimized { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public bool EnableServiceMode { get; set; }

    public string ManagementEndpoint { get; set; } = "net.pipe://localhost/FileRelay";
}

/// <summary>
/// Provides metadata for domain credentials stored in the credential vault.
/// </summary>
public sealed class CredentialReference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>Encrypted payload containing the password.</summary>
    public string ProtectedSecret { get; set; } = string.Empty;

    /// <summary>Timestamp of the last credential update.</summary>
    public DateTimeOffset LastRotated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional rotation interval used to notify administrators.</summary>
    public TimeSpan? RotationInterval { get; set; }
}
