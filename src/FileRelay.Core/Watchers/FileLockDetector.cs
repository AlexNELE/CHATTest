using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace FileRelay.Core.Watchers;

/// <summary>
/// Utility responsible for waiting until a file is no longer locked by the producing process.
/// </summary>
public sealed class FileLockDetector
{
    private readonly IFileSystem _fileSystem;

    public FileLockDetector(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Waits until a file can be opened with exclusive access which indicates that the write operation finished.
    /// </summary>
    public async Task WaitForFileReadyAsync(string filePath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        var delay = TimeSpan.FromMilliseconds(250);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTimeOffset.UtcNow - start > timeout)
            {
                throw new TimeoutException($"File {filePath} is still locked after {timeout}.");
            }

            if (!_fileSystem.File.Exists(filePath))
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                using var stream = _fileSystem.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException)
            {
                // file is still locked - exponential backoff up to 2 seconds
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 2000));
            }
        }
    }
}
