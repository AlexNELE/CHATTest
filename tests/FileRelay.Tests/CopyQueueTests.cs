using System.Threading;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Queue;
using Xunit;

namespace FileRelay.Tests;

public class CopyQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeueMaintainsOrder()
    {
        var queue = new CopyQueue();
        var source = new SourceConfiguration { Name = "Test", Path = "C:/Temp" };
        var target = new TargetConfiguration { Name = "Target", DestinationPath = @"\\server\share", CredentialId = Guid.NewGuid() };

        await queue.EnqueueAsync(new CopyRequest(source, target, "file1.txt", "file1.txt"), CancellationToken.None);
        await queue.EnqueueAsync(new CopyRequest(source, target, "file2.txt", "file2.txt"), CancellationToken.None);

        var enumerator = queue.DequeueAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("file1.txt", enumerator.Current.SourceFile);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("file2.txt", enumerator.Current.SourceFile);
    }
}
