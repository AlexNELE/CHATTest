using System;
using System.Text;
using System.Security.Cryptography; // Hinzugefügt für ProtectedData und DataProtectionScope
using System.Security; // Für SecureString
// using static System.Security.Cryptography.ProtectedData; // Entfernt, da nicht benötigt

namespace FileRelay.Core.Credentials;

/// <summary>
/// Provides DPAPI based encryption helpers for credential persistence.
/// </summary>
public static class CredentialProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FileRelay::CredentialProtector::2023");

    /// <summary>
    /// Protects the provided secret using the current user's DPAPI scope.
    /// </summary>
    public static string Protect(ReadOnlySpan<char> secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret.ToArray());
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// Decrypts the provided DPAPI blob.
    /// </summary>
    public static SecureString UnprotectToSecureString(string protectedBlob)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedBlob), Entropy, DataProtectionScope.CurrentUser);
        var secure = new SecureString();
        var chars = Encoding.UTF8.GetChars(bytes);
        foreach (var c in chars)
        {
            secure.AppendChar(c);
        }
        secure.MakeReadOnly();
        Array.Clear(chars, 0, chars.Length);
        Array.Clear(bytes, 0, bytes.Length);
        return secure;
    }
}
