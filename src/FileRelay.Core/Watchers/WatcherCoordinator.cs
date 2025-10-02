using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Abstractions;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Queue;
using Microsoft.Extensions.Logging;

namespace FileRelay.Core.Watchers;

/// <summary>
/// Central registry for all <see cref="SourceWatcher"/> instances.
/// </summary>
public sealed class WatcherCoordinator : IDisposable
{
    private readonly CopyQueue _queue;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IFileSystem _fileSystem;
    private readonly Dictionary<Guid, SourceWatcher> _watchers = new();

    public WatcherCoordinator(CopyQueue queue, ILoggerFactory loggerFactory, IFileSystem fileSystem)
    {
        _queue = queue;
        _loggerFactory = loggerFactory;
        _fileSystem = fileSystem;
    }

    public async Task InitializeAsync(IEnumerable<SourceConfiguration> sources)
    {
        var desired = sources.ToList();
        foreach (var source in desired)
        {
            await AddOrUpdateAsync(source).ConfigureAwait(false);
        }

        var toRemove = _watchers.Keys.Except(desired.Select(s => s.Id)).ToList();
        foreach (var id in toRemove)
        {
            Remove(id);
        }
    }

    public async Task AddOrUpdateAsync(SourceConfiguration configuration)
    {
        if (_watchers.TryGetValue(configuration.Id, out var existing))
        {
            existing.Dispose();
            _watchers.Remove(configuration.Id);
        }

        var watcher = new SourceWatcher(configuration, _queue, _loggerFactory.CreateLogger<SourceWatcher>(), _fileSystem);
        _watchers[configuration.Id] = watcher;
        await watcher.StartAsync().ConfigureAwait(false);
    }

    public void Pause(Guid sourceId)
    {
        if (_watchers.TryGetValue(sourceId, out var watcher))
        {
            watcher.Pause();
        }
    }

    public void Resume(Guid sourceId)
    {
        if (_watchers.TryGetValue(sourceId, out var watcher))
        {
            watcher.Resume();
        }
    }

    public void Remove(Guid sourceId)
    {
        if (_watchers.TryGetValue(sourceId, out var watcher))
        {
            watcher.Dispose();
            _watchers.Remove(sourceId);
        }
    }

    public IReadOnlyCollection<SourceConfiguration> ListConfigurations()
        => _watchers.Values.Select(w => w.Configuration).ToList();

    public void Dispose()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }
}
