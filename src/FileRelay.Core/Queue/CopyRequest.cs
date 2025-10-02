using FileRelay.Core.Configuration;

namespace FileRelay.Core.Queue;

/// <summary>
/// Represents a scheduled copy action for a specific file and target.
/// </summary>
public sealed class CopyRequest
{
    public CopyRequest(SourceConfiguration source, TargetConfiguration target, string sourceFile, string destinationFile)
    {
        Source = source;
        Target = target;
        SourceFile = sourceFile;
        DestinationFile = destinationFile;
        EnqueuedAt = DateTimeOffset.UtcNow;
    }

    public SourceConfiguration Source { get; }

    public TargetConfiguration Target { get; }

    public string SourceFile { get; }

    public string DestinationFile { get; }

    public DateTimeOffset EnqueuedAt { get; }

    public int Attempt { get; set; }
}
