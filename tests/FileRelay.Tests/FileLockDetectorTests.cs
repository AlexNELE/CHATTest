using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Watchers;
using Xunit;

namespace FileRelay.Tests;

public class FileLockDetectorTests
{
    [Fact]
    public async Task CompletesWhenLockReleased()
    {
        var fileSystem = new FileSystem();
        var detector = new FileLockDetector(fileSystem);
        var tempPath = Path.Combine(Path.GetTempPath(), $"locktest_{Guid.NewGuid():N}.tmp");

        await File.WriteAllTextAsync(tempPath, "test");

        using var stream = File.Open(tempPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var waitTask = detector.WaitForFileReadyAsync(tempPath, TimeSpan.FromSeconds(2), System.Threading.CancellationToken.None);
        await Task.Delay(200);
        stream.Dispose();

        await waitTask;
        File.Delete(tempPath);
    }
}
