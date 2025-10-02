namespace FileRelay.Core.Credentials;

/// <summary>
/// Represents a domain credential including decrypted password.
/// </summary>
public sealed class DomainCredential : IDisposable
{
    public DomainCredential(string domain, string username, SecureString password)
    {
        Domain = domain;
        Username = username;
        Password = password;
    }

    public string Domain { get; }

    public string Username { get; }

    public SecureString Password { get; }

    public void Dispose()
    {
        Password.Dispose();
    }
}
