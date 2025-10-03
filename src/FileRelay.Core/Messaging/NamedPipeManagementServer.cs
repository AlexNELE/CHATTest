using System;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Queue;
using FileRelay.Core.Watchers;
using Microsoft.Extensions.Logging;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FileRelay.Core.Messaging;

/// <summary>
/// Lightweight JSON-over-named-pipes management endpoint.
/// </summary>
public sealed class NamedPipeManagementServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly WatcherCoordinator _coordinator;
    private readonly CopyQueue _queue;
    private readonly Func<AppConfiguration> _configurationAccessor;
    private readonly Func<AppConfiguration, Task> _configurationUpdater;
    private readonly ILogger<NamedPipeManagementServer> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenerTask;

    public NamedPipeManagementServer(string pipeName, WatcherCoordinator coordinator, CopyQueue queue, Func<AppConfiguration> configurationAccessor, Func<AppConfiguration, Task> configurationUpdater, ILogger<NamedPipeManagementServer> logger)
    {
        _pipeName = pipeName;
        _coordinator = coordinator;
        _queue = queue;
        _configurationAccessor = configurationAccessor;
        _configurationUpdater = configurationUpdater;
        _logger = logger;
        _listenerTask = Task.Run(() => ListenAsync(_cts.Token), _cts.Token);
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = CreatePipeServer();
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8, false, leaveOpen: true);
                using var writer = new StreamWriter(server, Encoding.UTF8, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };

                try
                {
                    var requestJson = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (requestJson is null)
                    {
                        continue;
                    }

                    var document = JsonDocument.Parse(requestJson);
                    if (!document.RootElement.TryGetProperty("command", out var commandElement))
                    {
                        await writer.WriteLineAsync("{\"error\":\"Missing command\"}").ConfigureAwait(false);
                        continue;
                    }

                    var command = commandElement.GetString();
                    switch (command)
                    {
                        case "get-status":
                            var status = BuildStatus();
                            await writer.WriteLineAsync(JsonSerializer.Serialize(status)).ConfigureAwait(false);
                            break;
                        case "get-configuration":
                            await writer.WriteLineAsync(JsonSerializer.Serialize(_configurationAccessor())).ConfigureAwait(false);
                            break;
                        case "apply-configuration":
                            if (document.RootElement.TryGetProperty("payload", out var payload))
                            {
                                var config = payload.Deserialize<AppConfiguration>();
                                if (config != null)
                                {
                                    await _configurationUpdater(config).ConfigureAwait(false);
                                    await writer.WriteLineAsync("{\"status\":\"ok\"}").ConfigureAwait(false);
                                }
                                else
                                {
                                    await writer.WriteLineAsync("{\"error\":\"Invalid configuration\"}").ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                await writer.WriteLineAsync("{\"error\":\"Missing payload\"}").ConfigureAwait(false);
                            }
                            break;
                        default:
                            await writer.WriteLineAsync($"{{\"error\":\"Unknown command {command}\"}}").ConfigureAwait(false);
                            break;
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "Pipe connection for {PipeName} closed while processing request", _pipeName);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Management server error");
            }
        }
    }

    private RuntimeStatus BuildStatus()
    {
        var status = new RuntimeStatus
        {
            PendingQueueItems = _queue.Count,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        foreach (var source in _coordinator.ListConfigurations())
        {
            status.Sources.Add(new SourceStatus
            {
                Id = source.Id,
                Name = source.Name,
                Path = source.Path,
                Enabled = source.Enabled,
                IsRunning = source.Enabled,
                TargetCount = source.Targets.Count
            });
        }

        return status;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await _listenerTask.ConfigureAwait(false);
        _cts.Dispose();
    }

    private NamedPipeServerStream CreatePipeServer()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
                pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    PipeAccessRights.FullControl, AccessControlType.Allow));
                pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    PipeAccessRights.FullControl, AccessControlType.Allow));

                return NamedPipeServerStreamAcl.Create(_pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                    0, 0, pipeSecurity);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is System.IO.IOException)
            {
                _logger.LogWarning(ex, "Falling back to default pipe security for {PipeName}", _pipeName);
            }
        }

        return new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    }
}
