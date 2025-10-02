using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Queue;
using Microsoft.Extensions.Logging;

namespace FileRelay.Core.Watchers;

/// <summary>
/// Manages a <see cref="FileSystemWatcher"/> for a single source directory.
/// </summary>
public sealed class SourceWatcher : IDisposable
{
    private readonly SourceConfiguration _configuration;
    private readonly CopyQueue _queue;
    private readonly ILogger<SourceWatcher> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly CancellationTokenSource _cts = new();
    private FileSystemWatcher? _watcher;
    private readonly Channel<string> _eventChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });
    private readonly Task _processorTask;

    public SourceWatcher(SourceConfiguration configuration, CopyQueue queue, ILogger<SourceWatcher> logger, IFileSystem fileSystem)
    {
        _configuration = configuration;
        _queue = queue;
        _logger = logger;
        _fileSystem = fileSystem;
        _processorTask = Task.Run(() => ProcessEventsAsync(_cts.Token), _cts.Token);
    }

    public SourceConfiguration Configuration => _configuration;

    public Task StartAsync()
    {
        if (_watcher != null)
        {
            return Task.CompletedTask;
        }

        _fileSystem.Directory.CreateDirectory(_configuration.Path);
        var watcher = new FileSystemWatcher(_configuration.Path)
        {
            IncludeSubdirectories = _configuration.Recursive,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
        };

        watcher.Created += OnFileCreated;
        watcher.Changed += OnFileCreated;
        watcher.Renamed += OnFileRenamed;

        _watcher = watcher;
        _logger.LogInformation("Watcher started for {Path}", _configuration.Path);
        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
        }
    }

    public void Resume()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = true;
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.FullPath))
        {
            return;
        }

        _eventChannel.Writer.TryWrite(e.FullPath);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (string.IsNullOrEmpty(e.FullPath))
        {
            return;
        }

        _eventChannel.Writer.TryWrite(e.FullPath);
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var path in _eventChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                if (!ShouldProcess(path))
                {
                    continue;
                }

                foreach (var target in _configuration.Targets)
                {
                    var relativePath = Path.GetRelativePath(_configuration.Path, path);
                    var request = new CopyRequest(_configuration, target, path, relativePath);
                    await _queue.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue file {File}", path);
            }
        }
    }

    private bool ShouldProcess(string filePath)
    {
        if (!_configuration.Enabled)
        {
            return false;
        }

        if (_fileSystem.Directory.Exists(filePath))
        {
            return false;
        }

        var extension = Path.GetExtension(filePath);
        if (_configuration.IncludeFilters.Any())
        {
            if (!_configuration.IncludeFilters.Any(filter => Match(filter, filePath, extension)))
            {
                return false;
            }
        }

        if (_configuration.ExcludeFilters.Any(filter => Match(filter, filePath, extension)))
        {
            return false;
        }

        return true;
    }

    private static bool Match(string filter, string fullPath, string extension)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return false;
        }

        if (filter.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            var regex = new Regex(filter[6..], RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return regex.IsMatch(fullPath);
        }

        if (filter.StartsWith("*."))
        {
            return extension.Equals(filter[1..], StringComparison.OrdinalIgnoreCase);
        }

        return fullPath.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _watcher?.Dispose();
        _eventChannel.Writer.TryComplete();
        try
        {
            _processorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to shutdown watcher for {Path}", _configuration.Path);
        }
    }
}
