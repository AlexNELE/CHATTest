using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using FileRelay.Core.Configuration;

namespace FileRelay.Core.Credentials;

/// <summary>
/// Thread-safe in-memory representation of credential references backed by the configuration file.
/// </summary>
public sealed class CredentialStore
{
    private readonly Dictionary<Guid, CredentialReference> _credentials = new();
    private readonly object _syncRoot = new();

    public CredentialStore(IEnumerable<CredentialReference> credentials)
    {
        foreach (var credential in credentials)
        {
            _credentials[credential.Id] = credential;
        }
    }

    /// <summary>Returns all known credential references.</summary>
    public IReadOnlyCollection<CredentialReference> List()
    {
        lock (_syncRoot)
        {
            return _credentials.Values.Select(Clone).ToList();
        }
    }

    /// <summary>Retrieves a specific reference.</summary>
    public CredentialReference? Get(Guid id)
    {
        lock (_syncRoot)
        {
            return _credentials.TryGetValue(id, out var reference) ? Clone(reference) : null;
        }
    }

    /// <summary>Creates or updates a credential reference.</summary>
    public CredentialReference Upsert(string displayName, string domain, string username, SecureString password)
    {
        var protectedSecret = ProtectSecureString(password);
        var reference = new CredentialReference
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            Domain = domain,
            Username = username,
            ProtectedSecret = protectedSecret,
            LastRotated = DateTimeOffset.UtcNow
        };

        lock (_syncRoot)
        {
            _credentials[reference.Id] = reference;
            return Clone(reference);
        }
    }

    /// <summary>Updates the stored secret for an existing credential.</summary>
    public CredentialReference Rotate(Guid id, SecureString password)
    {
        lock (_syncRoot)
        {
            if (!_credentials.TryGetValue(id, out var reference))
            {
                throw new KeyNotFoundException($"Credential {id} not found");
            }

            reference.ProtectedSecret = ProtectSecureString(password);
            reference.LastRotated = DateTimeOffset.UtcNow;
            return Clone(reference);
        }
    }

    /// <summary>Removes credentials from the store.</summary>
    public void Remove(Guid id)
    {
        lock (_syncRoot)
        {
            _credentials.Remove(id);
        }
    }

    /// <summary>Replaces the internal cache with the provided credentials.</summary>
    public void Reset(IEnumerable<CredentialReference> credentials)
    {
        lock (_syncRoot)
        {
            _credentials.Clear();
            foreach (var credential in credentials)
            {
                _credentials[credential.Id] = Clone(credential);
            }
        }
    }

    /// <summary>Builds a <see cref="DomainCredential"/> for impersonation.</summary>
    public DomainCredential? TryGetDomainCredential(Guid id)
    {
        lock (_syncRoot)
        {
            if (!_credentials.TryGetValue(id, out var reference))
            {
                return null;
            }

            var secure = CredentialProtector.UnprotectToSecureString(reference.ProtectedSecret);
            return new DomainCredential(reference.Domain, reference.Username, secure);
        }
    }

    /// <summary>Determines whether the credential is due for rotation.</summary>
    public bool NeedsRotation(Guid id, DateTimeOffset referenceTime)
    {
        lock (_syncRoot)
        {
            if (!_credentials.TryGetValue(id, out var credential))
            {
                return false;
            }

            if (!credential.RotationInterval.HasValue)
            {
                return false;
            }

            return credential.LastRotated + credential.RotationInterval < referenceTime;
        }
    }

    private static string ProtectSecureString(SecureString password)
    {
        var chars = password.ToCharArray();
        try
        {
            return CredentialProtector.Protect(chars);
        }
        finally
        {
            Array.Clear(chars, 0, chars.Length);
        }
    }

    private static CredentialReference Clone(CredentialReference reference)
        => new()
        {
            Id = reference.Id,
            DisplayName = reference.DisplayName,
            Domain = reference.Domain,
            Username = reference.Username,
            ProtectedSecret = reference.ProtectedSecret,
            LastRotated = reference.LastRotated,
            RotationInterval = reference.RotationInterval
        };
}

internal static class SecureStringExtensions
{
    public static char[] ToCharArray(this SecureString secureString)
    {
        if (secureString == null)
        {
            return Array.Empty<char>();
        }

        var unmanaged = IntPtr.Zero;
        try
        {
            unmanaged = Marshal.SecureStringToGlobalAllocUnicode(secureString);
            var length = secureString.Length;
            var buffer = new char[length];
            Marshal.Copy(unmanaged, buffer, 0, length);
            return buffer;
        }
        finally
        {
            if (unmanaged != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanaged);
            }
        }
    }
}
