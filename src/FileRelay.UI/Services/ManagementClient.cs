using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Messaging;

namespace FileRelay.UI.Services;

public interface IManagementClient
{
    Task<RuntimeStatus?> GetStatusAsync(CancellationToken cancellationToken);

    Task<AppConfiguration?> GetConfigurationAsync(CancellationToken cancellationToken);

    Task<bool> ApplyConfigurationAsync(AppConfiguration configuration, CancellationToken cancellationToken);
}

/// <summary>
/// Named pipe client used by the UI to communicate with the service host.
/// </summary>
public sealed class ManagementClient : IManagementClient
{
    private readonly string _pipeName;

    public ManagementClient(string endpoint)
    {
        _pipeName = ResolvePipeName(endpoint);
    }

    public async Task<RuntimeStatus?> GetStatusAsync(CancellationToken cancellationToken)
    {
        return await SendAndReceiveAsync<RuntimeStatus>(new { command = "get-status" }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AppConfiguration?> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        return await SendAndReceiveAsync<AppConfiguration>(new { command = "get-configuration" }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ApplyConfigurationAsync(AppConfiguration configuration, CancellationToken cancellationToken)
    {
        var payload = new { command = "apply-configuration", payload = configuration };
        var response = await SendAsync(payload, cancellationToken).ConfigureAwait(false);
        return response?.Contains("\"ok\"", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private async Task<string?> SendAsync(object request, CancellationToken cancellationToken)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(2000, cancellationToken).ConfigureAwait(false);
            await using var writer = new StreamWriter(pipe, Encoding.UTF8, bufferSize: 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var json = JsonSerializer.Serialize(request);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
            return await reader.ReadLineAsync().ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<T?> SendAndReceiveAsync<T>(object request, CancellationToken cancellationToken)
    {
        var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(response);
        }
        catch
        {
            return default;
        }
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
