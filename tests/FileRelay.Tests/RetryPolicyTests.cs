using System;
using System.Linq;
using FileRelay.Core.Copy;
using Xunit;

namespace FileRelay.Tests;

public class RetryPolicyTests
{
    [Fact]
    public void ProducesExponentialSequence()
    {
        var policy = new RetryPolicy(3, TimeSpan.FromSeconds(1));
        var delays = policy.GetDelays().Take(3).ToArray();

        Assert.Equal(TimeSpan.FromSeconds(1), delays[0]);
        Assert.True(delays[1] > delays[0]);
        Assert.True(delays[2] > delays[1]);
    }
}
