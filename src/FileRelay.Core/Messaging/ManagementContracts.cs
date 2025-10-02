using FileRelay.Core.Configuration;

namespace FileRelay.Core.Messaging;

/// <summary>
/// Represents a snapshot of the running system used by the UI.
/// </summary>
public sealed class RuntimeStatus
{
    public IList<SourceStatus> Sources { get; set; } = new List<SourceStatus>();

    public long PendingQueueItems { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SourceStatus
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public bool IsRunning { get; set; }

    public DateTimeOffset? LastActivityUtc { get; set; }

    public int TargetCount { get; set; }
}

/// <summary>
/// Request contract for configuration updates from the UI.
/// </summary>
public sealed class ConfigurationUpdate
{
    public AppConfiguration Configuration { get; set; } = new();
}
