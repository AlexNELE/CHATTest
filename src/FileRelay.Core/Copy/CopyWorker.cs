using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Credentials;
using FileRelay.Core.Queue;
using FileRelay.Core.Watchers;
using Microsoft.Extensions.Logging;

namespace FileRelay.Core.Copy;

/// <summary>
/// Processes queued copy requests using impersonation and verification.
/// </summary>
public sealed class CopyWorker
{
    private readonly CopyQueue _queue;
    private readonly CredentialStore _credentialStore;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CopyWorker> _logger;
    private readonly FileLockDetector _lockDetector;
    private readonly GlobalOptions _options;
    private readonly SemaphoreSlim _globalSemaphore;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _targetSemaphores = new();

    public CopyWorker(CopyQueue queue, CredentialStore credentialStore, IFileSystem fileSystem, FileLockDetector lockDetector, ILogger<CopyWorker> logger, GlobalOptions options)
    {
        _queue = queue;
        _credentialStore = credentialStore;
        _fileSystem = fileSystem;
        _logger = logger;
        _lockDetector = lockDetector;
        _options = options;
        _globalSemaphore = new SemaphoreSlim(Math.Max(1, options.DefaultMaxParallelTransfers));
    }

    /// <summary>
    /// Starts processing queue items until cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _queue.DequeueAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = ProcessAsync(request, cancellationToken);
        }
    }

    private async Task ProcessAsync(CopyRequest request, CancellationToken cancellationToken)
    {
        var targetSemaphore = _targetSemaphores.GetOrAdd(request.Target.Id, _ => new SemaphoreSlim(request.Target.MaxParallelTransfers ?? _options.DefaultMaxParallelTransfers));
        await _globalSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        await targetSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var policy = new RetryPolicy(request.Target.MaxRetries ?? _options.DefaultRetryCount, _options.InitialRetryDelay);
            var attempt = 0;
            foreach (var delay in policy.GetDelays())
            {
                attempt++;
                request.Attempt = attempt;
                var result = await ExecuteCopyAsync(request, cancellationToken).ConfigureAwait(false);
                if (result.Success)
                {
                    return;
                }

                if (result.Status == "AuthError")
                {
                    _logger.LogError(result.Exception, "Authentication failure while copying {Source} to {Target}", request.SourceFile, request.Target.DestinationPath);
                    return;
                }

                if (attempt >= policy.MaxAttempts)
                {
                    _logger.LogError(result.Exception, "Copy of {Source} to {Target} failed after {Attempts} attempts", request.SourceFile, request.Target.DestinationPath, attempt);
                    return;
                }

                _logger.LogWarning(result.Exception, "Copy attempt {Attempt} for {File} failed. Retrying in {Delay}", attempt, request.SourceFile, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Copy processing cancelled for {File}", request.SourceFile);
        }
        finally
        {
            targetSemaphore.Release();
            _globalSemaphore.Release();
        }
    }

    private async Task<TransferResult> ExecuteCopyAsync(CopyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _lockDetector.WaitForFileReadyAsync(request.SourceFile, TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return TransferResult.Failure(request, ex);
        }

        if (!_fileSystem.File.Exists(request.SourceFile))
        {
            return TransferResult.Skipped(request, "SourceMissing");
        }

        var requiresCredential = RequiresNetworkCredential(request.Target);
        if (requiresCredential && request.Target.CredentialId == Guid.Empty)
        {
            return TransferResult.AuthError(request, new InvalidOperationException("Credential missing"));
        }

        DomainCredential? credential = null;
        NetworkConnection? connection = null;

        try
        {
            if (requiresCredential)
            {
                credential = _credentialStore.TryGetDomainCredential(request.Target.CredentialId);
                if (credential is null)
                {
                    return TransferResult.AuthError(request, new InvalidOperationException("Credential missing"));
                }

                connection = new NetworkConnection(GetShareRoot(request.Target.DestinationPath), credential);
                connection.Connect();
            }

            var destinationPath = ResolveDestinationPath(request);
            var destinationDirectory = _fileSystem.Path.GetDirectoryName(destinationPath)!;
            _fileSystem.Directory.CreateDirectory(destinationDirectory);

            var finalPath = HandleConflict(destinationPath, request.Target.ConflictMode);
            var tempFile = _fileSystem.Path.Combine(destinationDirectory, $".{_fileSystem.Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");

            await CopyFileAsync(request.SourceFile, tempFile, cancellationToken).ConfigureAwait(false);


            var destinationPath = ResolveDestinationPath(request);
            var destinationDirectory = _fileSystem.Path.GetDirectoryName(destinationPath)!;
            _fileSystem.Directory.CreateDirectory(destinationDirectory);

            var finalPath = HandleConflict(destinationPath, request.Target.ConflictMode);
            var tempFile = _fileSystem.Path.Combine(destinationDirectory, $".{_fileSystem.Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp");

            await CopyFileAsync(request.SourceFile, tempFile, cancellationToken).ConfigureAwait(false);

            if (request.Target.VerifyChecksum)
            {
                await VerifyChecksumAsync(request.SourceFile, tempFile, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                VerifySize(request.SourceFile, tempFile);
            }

            FinalizeCopy(tempFile, finalPath, request.Target.ConflictMode);

            if (request.Source.DeleteAfterCopy)
            {
                DeleteSourceFile(request.SourceFile, request.Source.UseRecycleBin);
            }

            _logger.LogInformation("Copied {Source} to {Destination}", request.SourceFile, finalPath);
            return TransferResult.Ok(request);
        }
        catch (Exception ex)
        {
            return TransferResult.Failure(request, ex);
        }
        finally
        {
            connection?.Dispose();
            credential?.Dispose();
        }
    }

    private string ResolveDestinationPath(CopyRequest request)
    {
        var basePath = request.Target.DestinationPath;
        var subfolder = request.Target.SubfolderTemplate;
        if (!string.IsNullOrWhiteSpace(subfolder))
        {
            subfolder = subfolder.Replace("{date:yyyyMMdd}", DateTime.UtcNow.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
                                 .Replace("{sourceName}", request.Source.Name, StringComparison.OrdinalIgnoreCase);
            basePath = _fileSystem.Path.Combine(basePath, subfolder);
        }

        var relative = request.DestinationFile.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var destination = _fileSystem.Path.Combine(basePath, relative);
        var normalizedBase = _fileSystem.Path.GetFullPath(basePath);
        var normalizedDestination = _fileSystem.Path.GetFullPath(destination);
        if (!normalizedDestination.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"Destination path {normalizedDestination} is outside of allowed root {normalizedBase}");
        }

        return normalizedDestination;
    }

    private string HandleConflict(string destinationPath, ConflictMode conflictMode)
    {
        if (!_fileSystem.File.Exists(destinationPath))
        {
            return destinationPath;
        }

        return conflictMode switch
        {
            ConflictMode.Replace => destinationPath,
            ConflictMode.Ignore => throw new IOException($"Destination file {destinationPath} already exists"),
            ConflictMode.Version => VersionDestination(destinationPath),
            _ => destinationPath
        };
    }

    private string VersionDestination(string destinationPath)
    {
        var directory = _fileSystem.Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var fileName = _fileSystem.Path.GetFileNameWithoutExtension(destinationPath);
        var extension = _fileSystem.Path.GetExtension(destinationPath);
        var versionName = $"{fileName}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}{extension}";
        return _fileSystem.Path.Combine(directory, versionName);
    }

    private async Task CopyFileAsync(string sourceFile, string tempDestination, CancellationToken cancellationToken)
    {
        await using var source = _fileSystem.FileStream.Create(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = _fileSystem.FileStream.Create(tempDestination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
    }

    private void VerifySize(string sourceFile, string tempFile)
    {
        var sourceInfo = _fileSystem.FileInfo.FromFileName(sourceFile);
        var targetInfo = _fileSystem.FileInfo.FromFileName(tempFile);
        if (sourceInfo.Length != targetInfo.Length)
        {
            throw new IOException($"Size mismatch between {sourceFile} and {tempFile}");
        }
    }

    private async Task VerifyChecksumAsync(string sourceFile, string tempFile, CancellationToken cancellationToken)
    {
        using var sourceHash = SHA256.Create();
        await using (var sourceStream = _fileSystem.FileStream.Create(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await sourceHash.ComputeHashAsync(sourceStream, cancellationToken).ConfigureAwait(false);
        }

        using var destHash = SHA256.Create();
        await using (var destStream = _fileSystem.FileStream.Create(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await destHash.ComputeHashAsync(destStream, cancellationToken).ConfigureAwait(false);
        }

        if (!sourceHash.Hash!.SequenceEqual(destHash.Hash!))
        {
            throw new IOException($"Checksum mismatch for {tempFile}");
        }
    }

    private void FinalizeCopy(string tempFile, string finalPath, ConflictMode mode)
    {
        if (mode == ConflictMode.Replace && _fileSystem.File.Exists(finalPath))
        {
            _fileSystem.File.Delete(finalPath);
        }

        _fileSystem.File.Move(tempFile, finalPath, overwrite: mode == ConflictMode.Replace);
    }

    private void DeleteSourceFile(string sourceFile, bool useRecycleBin)
    {
        if (!useRecycleBin)
        {
            _fileSystem.File.Delete(sourceFile);
            return;
        }

        var recycleDir = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), "FileRelayRecycleBin");
        _fileSystem.Directory.CreateDirectory(recycleDir);
        var target = _fileSystem.Path.Combine(recycleDir, _fileSystem.Path.GetFileName(sourceFile));
        _fileSystem.File.Move(sourceFile, target, overwrite: true);
    }

    private static string GetShareRoot(string path)
    {
        if (!path.StartsWith(@"\\"))
        {
            return path;
        }

        var index = path.IndexOf('\\', 2);
        if (index < 0)
        {
            return path;
        }

        index = path.IndexOf('\\', index + 1);
        return index < 0 ? path : path[..index];
    }

    private static readonly Lazy<HashSet<string>> LocalHostNames = new(BuildLocalHostNames, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<HashSet<IPAddress>> LocalAddresses = new(BuildLocalAddresses, LazyThreadSafetyMode.ExecutionAndPublication);

    private static bool IsUncPath(string path) => path.StartsWith(@"\\");

    private static bool RequiresNetworkCredential(TargetConfiguration target)
    {
        if (!IsUncPath(target.DestinationPath))
        {
            return false;
        }

        var host = GetUncHost(target.DestinationPath);
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (IsLocalHost(host))
        {
            return false;
        }

        return true;
    }

    private static string? GetUncHost(string path)
    {
        if (!IsUncPath(path))
        {
            return null;
        }

        var trimmed = path.TrimStart('\\');
        var separatorIndex = trimmed.IndexOf('\\');
        return separatorIndex < 0 ? trimmed : trimmed[..separatorIndex];
    }

    private static bool IsLocalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        host = host.TrimEnd('.');

        if (host.Length > 2 && host[0] == '[' && host[^1] == ']')
        {
            host = host[1..^1];
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(host, out var address))
        {
            return IsLocalAddress(address);
        }

        if (LocalHostNames.Value.Contains(host))
        {
            return true;
        }

        try
        {
            var resolved = Dns.GetHostAddresses(host);
            return resolved.Any(IsLocalAddress);
        }
        catch (SocketException)
        {
        }
        catch (ArgumentException)
        {
        }

        return false;
    }

    private static bool IsLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        return LocalAddresses.Value.Contains(address);
    }

    private static HashSet<string> BuildLocalHostNames()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfNotEmpty(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value.TrimEnd('.'));
            }
        }

        AddIfNotEmpty(Environment.MachineName);

        try
        {
            AddIfNotEmpty(Dns.GetHostName());
        }
        catch (SocketException)
        {
        }
        catch (Exception)
        {
        }

        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            if (!string.IsNullOrWhiteSpace(properties.DomainName))
            {
                AddIfNotEmpty($"{Environment.MachineName}.{properties.DomainName}");
            }
        }
        catch (NetworkInformationException)
        {
        }
        catch (Exception)
        {
        }

        return result;
    }

    private static HashSet<IPAddress> BuildLocalAddresses()
    {
        var result = new HashSet<IPAddress>();

        try
        {
            var hostName = Dns.GetHostName();
            foreach (var address in Dns.GetHostAddresses(hostName))
            {
                if (address != null)
                {
                    result.Add(address);
                }
            }
        }
        catch (SocketException)
        {
        }
        catch (Exception)
        {
        }

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address != null)
                    {
                        result.Add(unicast.Address);
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
        }
        catch (Exception)
        {
        }

        result.Add(IPAddress.Loopback);
        result.Add(IPAddress.IPv6Loopback);

        return result;
    }
}
