using System;
using System.Collections.Generic;

namespace FileRelay.Core.Copy;

/// <summary>
/// Describes retry semantics for copy operations.
/// </summary>
public sealed class RetryPolicy
{
    public RetryPolicy(int maxAttempts, TimeSpan initialDelay)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        if (initialDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDelay));
        }

        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay;
    }

    public int MaxAttempts { get; }

    public TimeSpan InitialDelay { get; }

    public IEnumerable<TimeSpan> GetDelays()
    {
        var attempt = 0;
        var delay = InitialDelay;
        while (attempt < MaxAttempts)
        {
            yield return delay;
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, TimeSpan.FromMinutes(5).TotalMilliseconds));
            attempt++;
        }
    }
}
