using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Messaging;
using FileRelay.Core.Queue;
using FileRelay.Core.Watchers;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FileRelay.Tests;

public sealed class ManagementServerTests : IAsyncLifetime
{
    private readonly string _tempDirectory;

    public ManagementServerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FileRelayTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ApplyConfigurationPersistsToDisk()
    {
        var queue = new CopyQueue();
        var coordinator = new WatcherCoordinator(queue, NullLoggerFactory.Instance, new FileSystem());
        var configurationPath = Path.Combine(_tempDirectory, "config.json");
        var configurationService = new ConfigurationService(configurationPath, NullLogger<ConfigurationService>.Instance);
        var pipeName = $"FileRelayTest-{Guid.NewGuid():N}";

        await using var server = new NamedPipeManagementServer(
            pipeName,
            coordinator,
            queue,
            configurationService.GetCurrent,
            configurationService.SaveAsync,
            NullLogger<NamedPipeManagementServer>.Instance);

        try
        {
            var configuration = new AppConfiguration();

            var response = await SendApplyConfigurationAsync(pipeName, configuration, CancellationToken.None).ConfigureAwait(false);

            Assert.Contains("\"ok\"", response, StringComparison.OrdinalIgnoreCase);

            await AssertFileExistsAsync(configurationPath).ConfigureAwait(false);
        }
        finally
        {
            coordinator.Dispose();
        }
    }

    private static async Task<string?> SendApplyConfigurationAsync(string pipeName, AppConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(2000, cancellationToken).ConfigureAwait(false);

        await using var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

        var payload = new
        {
            command = "apply-configuration",
            payload = configuration
        };

        var json = JsonSerializer.Serialize(payload);
        await writer.WriteLineAsync(json).ConfigureAwait(false);

        return await reader.ReadLineAsync().ConfigureAwait(false);
    }

    private static async Task AssertFileExistsAsync(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new Xunit.Sdk.XunitException($"Expected configuration file '{path}' to exist.");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
        }

        return Task.CompletedTask;
    }
}
