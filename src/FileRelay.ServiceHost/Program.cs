using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using FileRelay.Core.Configuration;
using FileRelay.Core.Queue;
using FileRelay.Core.Watchers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FileRelay.ServiceHost;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var defaultOptions = new GlobalOptions();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Async(a => a.File(Path.Combine(defaultOptions.LogDirectory, "filrelay-service.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30))
            .CreateLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    var configurationDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FileRelay");
                    var configurationFile = Path.Combine(configurationDirectory, "config.json");

                    services.AddSingleton<IFileSystem, FileSystem>();
                    services.AddSingleton<CopyQueue>();
                    services.AddSingleton(sp => new FileLockDetector(sp.GetRequiredService<IFileSystem>()));
                    services.AddSingleton(sp => new ConfigurationService(configurationFile, sp.GetRequiredService<ILogger<ConfigurationService>>()));
                    services.AddSingleton(sp => new WatcherCoordinator(sp.GetRequiredService<CopyQueue>(), sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<IFileSystem>()));
                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
