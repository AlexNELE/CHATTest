using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FileRelay.Core.Configuration;

/// <summary>
/// Manages persistence of the application configuration.
/// </summary>
public sealed class ConfigurationService
{
    private readonly string _configurationFile;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<ConfigurationService> _logger;
    private AppConfiguration _current;
    private readonly object _sync = new();

    public ConfigurationService(string configurationFile, ILogger<ConfigurationService> logger)
    {
        _configurationFile = configurationFile;
        _logger = logger;
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
        _current = new AppConfiguration();
    }

    public AppConfiguration GetCurrent()
    {
        lock (_sync)
        {
            return _current;
        }
    }

    public async Task<AppConfiguration> LoadAsync()
    {
        try
        {
            if (!File.Exists(_configurationFile))
            {
                _logger.LogWarning("Configuration file {File} does not exist. Using defaults.", _configurationFile);

                var defaults = new AppConfiguration();
                lock (_sync)
                {
                    _current = defaults;
                }

                await SaveAsync(defaults).ConfigureAwait(false);
                return defaults;
            }

            await using var stream = File.OpenRead(_configurationFile);
            var configuration = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, _serializerOptions).ConfigureAwait(false) ?? new AppConfiguration();
            lock (_sync)
            {
                _current = configuration;
            }

            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
            throw;
        }
    }

    public async Task SaveAsync(AppConfiguration configuration)
    {
        var directory = Path.GetDirectoryName(_configurationFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await using var stream = File.Create(_configurationFile);
        await JsonSerializer.SerializeAsync(stream, configuration, _serializerOptions).ConfigureAwait(false);
        lock (_sync)
        {
            _current = configuration;
        }
    }
}
