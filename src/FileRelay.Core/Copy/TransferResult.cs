using System;
using FileRelay.Core.Queue;

namespace FileRelay.Core.Copy;

/// <summary>
/// Represents the outcome of a copy attempt.
/// </summary>
public sealed class TransferResult
{
    private TransferResult(CopyRequest request, bool success, string status, Exception? exception = null)
    {
        Request = request;
        Success = success;
        Status = status;
        Exception = exception;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public CopyRequest Request { get; }

    public bool Success { get; }

    public string Status { get; }

    public Exception? Exception { get; }

    public DateTimeOffset CompletedAt { get; }

    public static TransferResult Ok(CopyRequest request) => new(request, true, "OK");

    public static TransferResult AuthError(CopyRequest request, Exception ex) => new(request, false, "AuthError", ex);

    public static TransferResult Failure(CopyRequest request, Exception ex) => new(request, false, "Failed", ex);

    public static TransferResult Skipped(CopyRequest request, string reason) => new(request, false, reason);
}
