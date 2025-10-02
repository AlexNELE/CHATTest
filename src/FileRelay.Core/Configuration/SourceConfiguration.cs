using System;
using System.Collections.Generic;

namespace FileRelay.Core.Configuration;

/// <summary>
/// Describes a monitored source directory including file filters and copy targets.
/// </summary>
public sealed class SourceConfiguration
{
    /// <summary>Internal identifier for cross references.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name used in the UI and logs.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute path of the directory to monitor.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Indicates whether the watcher is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Determines if sub-directories are monitored as well.</summary>
    public bool Recursive { get; set; } = true;

    /// <summary>Optional include filter expressions (supports wildcards and regex markers).</summary>
    public IList<string> IncludeFilters { get; set; } = new List<string>();

    /// <summary>Optional exclude filter expressions (supports wildcards and regex markers).</summary>
    public IList<string> ExcludeFilters { get; set; } = new List<string>();

    /// <summary>Collection of copy targets the detected files should be transferred to.</summary>
    public IList<TargetConfiguration> Targets { get; set; } = new List<TargetConfiguration>();

    /// <summary>Specifies whether files should be removed after successful copy.</summary>
    public bool DeleteAfterCopy { get; set; }

    /// <summary>Specifies whether files are moved to recycle bin when deletion is enabled.</summary>
    public bool UseRecycleBin { get; set; }

    /// <summary>Optional concurrency override for this source.</summary>
    public int? MaxParallelTransfers { get; set; }

    /// <summary>Optional rate limit (files per minute) for flood protection.</summary>
    public int? MaxFilesPerMinute { get; set; }
}
