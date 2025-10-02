using FileRelay.Core.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace FileRelay.Core.Logging;

/// <summary>
/// Provides Serilog configuration for both service and UI layers.
/// </summary>
public static class LogConfigurator
{
    public static ILoggerFactory CreateLoggerFactory(GlobalOptions options)
    {
        var logDirectory = options.LogDirectory;
        Directory.CreateDirectory(logDirectory);

        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Async(a => a.File(Path.Combine(logDirectory, "filrelay.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30))
            .CreateLogger();

        return new SerilogLoggerFactory(logger, dispose: true);
    }
}
