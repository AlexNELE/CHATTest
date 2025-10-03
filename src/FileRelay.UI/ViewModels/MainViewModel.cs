using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileRelay.Core.Configuration;
using FileRelay.Core.Messaging;
using FileRelay.UI.Services;

namespace FileRelay.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IManagementClient _managementClient;
    private AppConfiguration _configuration = new();

    public MainViewModel()
        : this(new ManagementClient("net.pipe://localhost/FileRelay"))
    {
    }

    public MainViewModel(IManagementClient managementClient)
    {
        _managementClient = managementClient;
        Sources = new ObservableCollection<SourceItemViewModel>();
        LogEntries = new ObservableCollection<LogEntryViewModel>();
        LogLevelFilters = new ObservableCollection<string>(new[] { "Alle", "Info", "Warn", "Error" });
        SelectedLogLevelFilter = LogLevelFilters.First();
        _ = RefreshAsync();
    }

    public ObservableCollection<SourceItemViewModel> Sources { get; }

    [ObservableProperty]
    private SourceItemViewModel? selectedSource;

    public ObservableCollection<LogEntryViewModel> LogEntries { get; }

    public ObservableCollection<string> LogLevelFilters { get; }

    [ObservableProperty]
    private string selectedLogLevelFilter = "Alle";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var status = await _managementClient.GetStatusAsync(CancellationToken.None).ConfigureAwait(false);
        var configuration = await _managementClient.GetConfigurationAsync(CancellationToken.None).ConfigureAwait(false);
        if (configuration != null)
        {
            _configuration = configuration;
        }

        Application.Current?.Dispatcher.Invoke(() => UpdateSources(status));
        LoadLogs();
    }

    [RelayCommand]
    private async Task AddSourceAsync()
    {
        var editor = new SourceItemViewModel();
        if (!ShowSourceDialog(editor))
        {
            return;
        }

        var configuration = editor.ToConfiguration();
        _configuration.Sources.Add(configuration);
        await PersistConfigurationAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task EditSourceAsync()
    {
        if (SelectedSource == null)
        {
            return;
        }

        var editor = SelectedSource;
        if (!ShowSourceDialog(editor))
        {
            return;
        }

        var updated = editor.ToConfiguration();
        var existing = _configuration.Sources.FirstOrDefault(s => s.Id == updated.Id);
        if (existing != null)
        {
            var index = _configuration.Sources.IndexOf(existing);
            _configuration.Sources[index] = updated;
        }

        await PersistConfigurationAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveSourceAsync()
    {
        if (SelectedSource == null)
        {
            return;
        }

        if (MessageBox.Show($"Quelle {SelectedSource.Name} wirklich entfernen?", "Bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _configuration.Sources = _configuration.Sources.Where(s => s.Id != SelectedSource.Id).ToList();
        await PersistConfigurationAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task PauseSourceAsync()
    {
        if (SelectedSource == null)
        {
            return;
        }

        SelectedSource.Enabled = false;
        UpdateConfigurationFromViewModel(SelectedSource);
        await PersistConfigurationAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ResumeSourceAsync()
    {
        if (SelectedSource == null)
        {
            return;
        }

        SelectedSource.Enabled = true;
        UpdateConfigurationFromViewModel(SelectedSource);
        await PersistConfigurationAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private void ExportLogs()
    {
        try
        {
            var exportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FileRelay-Logs");
            Directory.CreateDirectory(exportDirectory);
            var sourceFile = GetLogFile();
            if (sourceFile != null && File.Exists(sourceFile))
            {
                var targetFile = Path.Combine(exportDirectory, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, targetFile, true);
                MessageBox.Show($"Logs exportiert nach {targetFile}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateSources(RuntimeStatus? status)
    {
        Sources.Clear();
        var credentialNames = _configuration.Credentials.ToDictionary(c => c.Id, c => $"{c.Domain}\\{c.Username}");
        var statusSources = status?.Sources ?? Enumerable.Empty<SourceStatus>();
        foreach (var source in _configuration.Sources)
        {
            var statusItem = statusSources.FirstOrDefault(s => s.Id == source.Id);
            var vm = SourceItemViewModel.FromConfiguration(source, statusItem?.LastActivityUtc, statusItem?.TargetCount ?? 0);
            foreach (var target in vm.Targets)
            {
                if (credentialNames.TryGetValue(target.CredentialId, out var name))
                {
                    target.CredentialName = name;
                }
            }

            if (statusItem != null)
            {
                vm.PendingTransfers = status?.PendingQueueItems is long queue ? (int)queue : vm.PendingTransfers;
                vm.Enabled = statusItem.Enabled;
            }

            Sources.Add(vm);
        }
    }

    private void LoadLogs()
    {
        LogEntries.Clear();
        var file = GetLogFile();
        if (file == null || !File.Exists(file))
        {
            return;
        }

        foreach (var line in File.ReadLines(file).TakeLast(200))
        {
            if (!TryParseLog(line, out var entry))
            {
                continue;
            }

            if (SelectedLogLevelFilter != "Alle" && !entry.Level.Equals(SelectedLogLevelFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LogEntries.Add(entry);
        }
    }

    partial void OnSelectedLogLevelFilterChanged(string value)
    {
        LoadLogs();
    }

    private static bool TryParseLog(string line, out LogEntryViewModel entry)
    {
        entry = new LogEntryViewModel();
        try
        {
            var parts = line.Split(' ', 3);
            if (parts.Length < 3)
            {
                return false;
            }

            entry.Timestamp = DateTimeOffset.TryParse(parts[0], out var timestamp) ? timestamp : DateTimeOffset.UtcNow;
            entry.Level = parts[1].Trim('[', ']');
            entry.Message = parts[2];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string? GetLogFile()
    {
        var logDirectory = _configuration.Options?.LogDirectory;
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FileRelay", "logs");
        }

        var file = Path.Combine(logDirectory, "filrelay-service.log");
        return file;
    }

    private bool ShowSourceDialog(SourceItemViewModel viewModel)
    {
        var clone = viewModel.Clone();
        var dialogVm = new Windows.SourceEditorViewModel(clone, _configuration.Credentials);
        var dialog = new Windows.SourceEditorWindow { DataContext = dialogVm };
        var result = dialog.ShowDialog();
        if (result == true)
        {
            viewModel.UpdateFrom(clone);
            if (viewModel.Id == Guid.Empty && clone.Id != Guid.Empty)
            {
                viewModel.Id = clone.Id;
            }
            return true;
        }

        return false;
    }

    private void UpdateConfigurationFromViewModel(SourceItemViewModel viewModel)
    {
        var updated = viewModel.ToConfiguration();
        var existing = _configuration.Sources.FirstOrDefault(s => s.Id == updated.Id);
        if (existing != null)
        {
            var index = _configuration.Sources.IndexOf(existing);
            _configuration.Sources[index] = updated;
        }
    }

    private async Task PersistConfigurationAsync()
    {
        await _managementClient.ApplyConfigurationAsync(_configuration, CancellationToken.None).ConfigureAwait(false);
    }
}
