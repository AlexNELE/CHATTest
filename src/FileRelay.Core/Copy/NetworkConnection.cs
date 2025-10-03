using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using FileRelay.Core.Credentials;

namespace FileRelay.Core.Copy;

/// <summary>
/// Establishes an authenticated network connection for UNC paths using explicit credentials.
/// </summary>
internal sealed class NetworkConnection : IDisposable
{
    private readonly string _networkPath;
    private readonly DomainCredential _credential;
    private bool _disposed;

    public NetworkConnection(string networkPath, DomainCredential credential)
    {
        _networkPath = networkPath;
        _credential = credential;
    }

    public void Connect()
    {
        var resource = new NetResource
        {
            Scope = ResourceScope.GlobalNetwork,
            ResourceType = ResourceType.Disk,
            DisplayType = ResourceDisplayType.Share,
            RemoteName = _networkPath
        };

        var userName = string.IsNullOrWhiteSpace(_credential.Domain)
            ? _credential.Username
            : $"{_credential.Domain}\\{_credential.Username}";

        var passwordHandle = IntPtr.Zero;
        try
        {
            passwordHandle = Marshal.SecureStringToGlobalAllocUnicode(_credential.Password);
            var result = WNetAddConnection2(ref resource, passwordHandle, userName, 0);
            if (result != 0 && result != ErrorAlreadyAssigned)
            {
                throw new Win32Exception(result, $"Failed to connect to {_networkPath}");
            }
        }
        finally
        {
            if (passwordHandle != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(passwordHandle);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_networkPath.StartsWith(@"\\"))
        {
            WNetCancelConnection2(_networkPath, 0, true);
        }
    }

    private const int ErrorAlreadyAssigned = 85;

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(ref NetResource lpNetResource, IntPtr lpPassword, string? lpUserName, int dwFlags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string lpName, int dwFlags, bool fForce);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NetResource
    {
        public ResourceScope Scope;
        public ResourceType ResourceType;
        public ResourceDisplayType DisplayType;
        public int Usage;
        public string? LocalName;
        public string? RemoteName;
        public string? Comment;
        public string? Provider;
    }

    private enum ResourceScope : int
    {
        Connected = 1,
        GlobalNetwork,
        Remembered,
        Recent,
        Context
    }

    private enum ResourceType : int
    {
        Any = 0,
        Disk = 1,
        Print = 2
    }

    private enum ResourceDisplayType : int
    {
        Generic = 0,
        Domain = 1,
        Server = 2,
        Share = 3,
        File = 4,
        Group = 5
    }
}
