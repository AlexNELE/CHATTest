using System;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using FileRelay.Core.Configuration;

namespace FileRelay.UI.Windows;

public partial class CredentialEditorViewModel : ObservableObject
{
    private CredentialReference? _original;

    private CredentialEditorViewModel()
    {
    }

    public static CredentialEditorViewModel CreateNew()
        => new()
        {
            Id = Guid.Empty,
            DisplayName = string.Empty,
            Domain = string.Empty,
            Username = string.Empty,
            LastRotated = DateTimeOffset.UtcNow
        };

    public static CredentialEditorViewModel FromExisting(CredentialReference reference)
        => new()
        {
            _original = Clone(reference),
            Id = reference.Id,
            DisplayName = reference.DisplayName,
            Domain = reference.Domain,
            Username = reference.Username,
            LastRotated = reference.LastRotated,
            RotationIntervalDays = reference.RotationInterval.HasValue
                ? (int?)Math.Round(reference.RotationInterval.Value.TotalDays)
                : null
        };

    [ObservableProperty]
    private Guid id;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string domain = string.Empty;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private int? rotationIntervalDays;

    [ObservableProperty]
    private DateTimeOffset lastRotated;

    public SecureString? Password { get; private set; }

    public CredentialReference? Original => _original;

    public bool RequirePassword => Id == Guid.Empty;

    public bool HasPassword => Password != null && Password.Length > 0;

    public TimeSpan? RotationInterval => RotationIntervalDays.HasValue
        ? TimeSpan.FromDays(RotationIntervalDays.Value)
        : null;

    public void SetPassword(SecureString? password)
    {
        Password?.Dispose();
        Password = password?.Copy();
    }

    public void ClearPassword()
    {
        Password?.Dispose();
        Password = null;
    }

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            error = "Anzeigename ist erforderlich.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            error = "Benutzername ist erforderlich.";
            return false;
        }

        if (RequirePassword && !HasPassword)
        {
            error = "Ein Passwort wird benötigt.";
            return false;
        }

        if (RotationIntervalDays.HasValue && RotationIntervalDays.Value < 0)
        {
            error = "Die Rotationsdauer muss positiv sein.";
            return false;
        }

        error = null;
        return true;
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
