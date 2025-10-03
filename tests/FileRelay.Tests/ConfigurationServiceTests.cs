using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileRelay.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public async Task LoadAsync_PersistsDefaultsWhenConfigurationFileMissing()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var configurationFile = Path.Combine(tempDirectory, "config.json");

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
            var logger = loggerFactory.CreateLogger<ConfigurationService>();
            var service = new ConfigurationService(configurationFile, logger);

            var configuration = await service.LoadAsync().ConfigureAwait(false);

            Assert.NotNull(configuration);
            Assert.True(File.Exists(configurationFile));

            await using var stream = File.OpenRead(configurationFile);
            var persisted = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream).ConfigureAwait(false);
            Assert.NotNull(persisted);
            Assert.Equal(configuration.Options.ManagementEndpoint, persisted!.Options.ManagementEndpoint);
            Assert.Equal(configuration.Sources.Count, persisted.Sources.Count);
            Assert.Equal(configuration.Credentials.Count, persisted.Credentials.Count);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_RecreatesDefaultsWhenPreviousConfigurationLost()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var configurationFile = Path.Combine(tempDirectory, "config.json");

        try
        {
            var service = new ConfigurationService(configurationFile, NullLogger<ConfigurationService>.Instance);

            var customConfiguration = new AppConfiguration();
            customConfiguration.Options.ManagementEndpoint = "net.pipe://localhost/Custom";
            customConfiguration.Sources.Add(new SourceConfiguration
            {
                Name = "Custom",
                Path = tempDirectory
            });

            await service.SaveAsync(customConfiguration).ConfigureAwait(false);
            File.Delete(configurationFile);

            var configuration = await service.LoadAsync().ConfigureAwait(false);

            Assert.NotNull(configuration);
            Assert.True(File.Exists(configurationFile));
            Assert.Equal("net.pipe://localhost/FileRelay", configuration.Options.ManagementEndpoint);
            Assert.Empty(configuration.Sources);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }
}
