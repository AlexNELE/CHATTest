using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Copy;
using FileRelay.Core.Credentials;
using FileRelay.Core.Messaging;
using FileRelay.Core.Queue;
using FileRelay.Core.Watchers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace FileRelay.ServiceHost;

public sealed class Worker : BackgroundService
{
    private readonly ConfigurationService _configurationService;
    private readonly CopyQueue _queue;
    private readonly FileLockDetector _lockDetector;
    private readonly IFileSystem _fileSystem;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Worker> _logger;
    private readonly WatcherCoordinator _coordinator;

    private CredentialStore? _credentialStore;
    private CopyWorker? _copyWorker;
    private NamedPipeManagementServer? _managementServer;
    private Task? _copyTask;
    private readonly GlobalOptions _options = new();
    private string _currentPipeName = "FileRelay";

    public Worker(ConfigurationService configurationService, CopyQueue queue, FileLockDetector lockDetector, IFileSystem fileSystem, ILoggerFactory loggerFactory, ILogger<Worker> logger, WatcherCoordinator coordinator)
    {
        _configurationService = configurationService;
        _queue = queue;
        _lockDetector = lockDetector;
        _fileSystem = fileSystem;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _coordinator = coordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuration = await _configurationService.LoadAsync().ConfigureAwait(false);
        await ApplyConfigurationInternalAsync(configuration).ConfigureAwait(false);

        _copyWorker = new CopyWorker(_queue, _credentialStore!, _fileSystem, _lockDetector, _loggerFactory.CreateLogger<CopyWorker>(), _options);
        _copyTask = _copyWorker.RunAsync(stoppingToken);

        var pipeName = ResolvePipeName(_options.ManagementEndpoint);
        _currentPipeName = pipeName;
        _managementServer = new NamedPipeManagementServer(pipeName, _coordinator, _queue, _configurationService.GetCurrent, ApplyConfigurationAsync, _loggerFactory.CreateLogger<NamedPipeManagementServer>());

        _logger.LogInformation("FileRelay worker started with {SourceCount} sources", configuration.Sources.Count);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker cancellation requested");
        }
    }

    private async Task ApplyConfigurationAsync(AppConfiguration configuration)
    {
        await _configurationService.SaveAsync(configuration).ConfigureAwait(false);
        await ApplyConfigurationInternalAsync(configuration).ConfigureAwait(false);
    }

    private async Task ApplyConfigurationInternalAsync(AppConfiguration configuration)
    {
        UpdateOptions(configuration.Options ?? new GlobalOptions());
        if (_credentialStore == null)
        {
            _credentialStore = new CredentialStore(configuration.Credentials);
        }
        else
        {
            _credentialStore.Reset(configuration.Credentials);
        }
        await _coordinator.InitializeAsync(configuration.Sources).ConfigureAwait(false);

        var pipeName = ResolvePipeName(_options.ManagementEndpoint);
        if (_managementServer != null && !string.Equals(pipeName, _currentPipeName, StringComparison.OrdinalIgnoreCase))
        {
            await _managementServer.DisposeAsync().ConfigureAwait(false);
            _currentPipeName = pipeName;
            _managementServer = new NamedPipeManagementServer(pipeName, _coordinator, _queue, _configurationService.GetCurrent, ApplyConfigurationAsync, _loggerFactory.CreateLogger<NamedPipeManagementServer>());
        }
    }

    private void UpdateOptions(GlobalOptions source)
    {
        _options.DefaultMaxParallelTransfers = source.DefaultMaxParallelTransfers;
        _options.DefaultRetryCount = source.DefaultRetryCount;
        _options.InitialRetryDelay = source.InitialRetryDelay;
        _options.LogDirectory = source.LogDirectory;
        _options.StartMinimized = source.StartMinimized;
        _options.StartWithWindows = source.StartWithWindows;
        _options.EnableServiceMode = source.EnableServiceMode;
        _options.ManagementEndpoint = source.ManagementEndpoint;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        _queue.Complete();
        if (_copyTask != null)
        {
            try
            {
                await _copyTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore cancellation during shutdown
            }
        }

        if (_managementServer != null)
        {
            await _managementServer.DisposeAsync().ConfigureAwait(false);
        }

        _coordinator.Dispose();
    }

    private static string ResolvePipeName(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "FileRelay";
        }

        if (endpoint.StartsWith("net.pipe://", StringComparison.OrdinalIgnoreCase))
        {
            var segments = endpoint.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.LastOrDefault() ?? "FileRelay";
        }

        return endpoint;
    }
}
