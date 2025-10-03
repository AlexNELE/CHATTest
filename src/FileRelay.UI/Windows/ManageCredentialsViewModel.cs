using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileRelay.Core.Configuration;
using FileRelay.Core.Credentials;

namespace FileRelay.UI.Windows;

public partial class ManageCredentialsViewModel : ObservableObject
{
    public ManageCredentialsViewModel(IEnumerable<CredentialReference> credentials)
    {
        Credentials = new ObservableCollection<CredentialReference>(credentials.Select(Clone));
        SelectedCredential = Credentials.FirstOrDefault();
    }

    public ObservableCollection<CredentialReference> Credentials { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCredentialCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCredentialCommand))]
    private CredentialReference? selectedCredential;

    public IReadOnlyList<CredentialReference> GetCredentials()
        => Credentials.Select(Clone).ToList();

    [RelayCommand]
    private void AddCredential()
    {
        var editor = CredentialEditorViewModel.CreateNew();
        if (!ShowEditor(editor))
        {
            return;
        }

        try
        {
            var reference = CreateReference(editor);
            Credentials.Add(reference);
            SelectedCredential = reference;
        }
        finally
        {
            editor.ClearPassword();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrRemove))]
    private void EditCredential()
    {
        if (SelectedCredential == null)
        {
            return;
        }

        var editor = CredentialEditorViewModel.FromExisting(SelectedCredential);
        if (!ShowEditor(editor))
        {
            return;
        }

        try
        {
            var updated = CreateReference(editor);
            var index = Credentials.IndexOf(SelectedCredential);
            Credentials[index] = updated;
            SelectedCredential = updated;
        }
        finally
        {
            editor.ClearPassword();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrRemove))]
    private void RemoveCredential()
    {
        if (SelectedCredential == null)
        {
            return;
        }

        if (MessageBox.Show($"Credential {SelectedCredential.DisplayName} entfernen?", "Bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var index = Credentials.IndexOf(SelectedCredential);
        Credentials.RemoveAt(index);
        if (Credentials.Count > 0)
        {
            var newIndex = Math.Min(index, Credentials.Count - 1);
            SelectedCredential = Credentials[newIndex];
        }
        else
        {
            SelectedCredential = null;
        }
    }

    private bool CanEditOrRemove()
        => SelectedCredential != null;

    private bool ShowEditor(CredentialEditorViewModel editor)
    {
        var window = new CredentialEditorWindow { DataContext = editor };
        window.Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var result = window.ShowDialog();
        return result == true;
    }

    private CredentialReference CreateReference(CredentialEditorViewModel editor)
    {
        var rotationInterval = editor.RotationInterval;
        CredentialReference reference;
        if (editor.HasPassword)
        {
            var store = new CredentialStore(Array.Empty<CredentialReference>());
            reference = store.Upsert(editor.DisplayName, editor.Domain, editor.Username, editor.Password!);
        }
        else if (editor.Original != null)
        {
            reference = Clone(editor.Original);
            reference.DisplayName = editor.DisplayName;
            reference.Domain = editor.Domain;
            reference.Username = editor.Username;
        }
        else
        {
            throw new InvalidOperationException("Password required");
        }

        if (editor.Id != Guid.Empty)
        {
            reference.Id = editor.Id;
            if (!editor.HasPassword && editor.Original != null)
            {
                reference.ProtectedSecret = editor.Original.ProtectedSecret;
                reference.LastRotated = editor.Original.LastRotated;
            }
        }

        reference.RotationInterval = rotationInterval;
        return reference;
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
