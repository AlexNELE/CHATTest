using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Copy;
using FileRelay.Core.Credentials;
using FileRelay.Core.Queue;
using FileRelay.Core.Watchers;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace FileRelay.Tests;

public class CopyWorkerTests
{
    [Fact]
    public async Task LocalDestinationWithoutCredentialSucceeds()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"C:\\source\\file.txt", new MockFileData("content") }
        });

        var worker = CreateWorker(fileSystem, new CredentialStore(Array.Empty<CredentialReference>()));
        var source = new SourceConfiguration { Name = "Source", Path = @"C:\\source" };
        var target = new TargetConfiguration
        {
            Name = "Local",
            DestinationPath = @"C:\\dest",
            CredentialId = Guid.Empty,
            VerifyChecksum = false
        };

        var request = new CopyRequest(source, target, @"C:\\source\\file.txt", "file.txt");
        var result = await InvokeExecuteCopyAsync(worker, request).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.True(fileSystem.FileExists(@"C:\\dest\\file.txt"));
    }

    [Fact]
    public async Task RemoteUncWithoutCredentialFailsWithAuthError()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"C:\\source\\file.txt", new MockFileData("content") }
        });

        var worker = CreateWorker(fileSystem, new CredentialStore(Array.Empty<CredentialReference>()));
        var source = new SourceConfiguration { Name = "Source", Path = @"C:\\source" };
        var target = new TargetConfiguration
        {
            Name = "Remote",
            DestinationPath = @"\\\\remotehost\\share",
            CredentialId = Guid.Empty
        };

        var request = new CopyRequest(source, target, @"C:\\source\\file.txt", "file.txt");
        var result = await InvokeExecuteCopyAsync(worker, request).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Equal("AuthError", result.Status);
    }

    [Fact]
    public void RequiresNetworkCredentialTreatsRegisteredLocalIpAsLocal()
    {
        var type = typeof(CopyWorker);
        var addressesField = type.GetField("LocalAddresses", BindingFlags.NonPublic | BindingFlags.Static)!;
        var lazy = (Lazy<HashSet<IPAddress>>)addressesField.GetValue(null)!;
        var addresses = lazy.Value;
        var customAddress = IPAddress.Parse("192.0.2.55");
        addresses.Add(customAddress);

        var target = new TargetConfiguration
        {
            DestinationPath = $"\\\\{customAddress}\\share"
        };

        var requires = (bool)type.GetMethod("RequiresNetworkCredential", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { target })!;

        Assert.False(requires);
    }

    private static CopyWorker CreateWorker(IFileSystem fileSystem, CredentialStore credentialStore)
    {
        var queue = new CopyQueue();
        var lockDetector = new FileLockDetector(fileSystem);
        var options = new GlobalOptions();
        return new CopyWorker(queue, credentialStore, fileSystem, lockDetector, NullLogger<CopyWorker>.Instance, options);
    }

    private static Task<TransferResult> InvokeExecuteCopyAsync(CopyWorker worker, CopyRequest request)
    {
        var method = typeof(CopyWorker).GetMethod("ExecuteCopyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task<TransferResult>)method.Invoke(worker, new object[] { request, CancellationToken.None })!;
    }
}
