using System;
using System.Security;
using FileRelay.Core.Configuration;
using FileRelay.Core.Credentials;
using Xunit;

namespace FileRelay.Tests;

public class CredentialStoreTests
{
    [Fact]
    public void UpsertAndRetrieveRoundtrip()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var password = new SecureString();
        foreach (var ch in "P@ssw0rd".ToCharArray())
        {
            password.AppendChar(ch);
        }
        password.MakeReadOnly();

        var store = new CredentialStore(Array.Empty<CredentialReference>());
        var reference = store.Upsert("Test", "DOMAIN", "User", password);
        using var credential = store.TryGetDomainCredential(reference.Id);

        Assert.NotNull(credential);
        Assert.Equal("DOMAIN", credential!.Domain);
        Assert.Equal("User", credential.Username);
    }
}
