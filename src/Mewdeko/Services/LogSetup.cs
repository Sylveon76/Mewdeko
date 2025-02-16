using System.Text;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using ILogger = Serilog.ILogger;

namespace Mewdeko.Services;

/// <summary>
/// Class responsible for setting up the logger configuration with detailed database logging.
/// </summary>
public static class LogSetup
{
    /// <summary>
    /// Sets up the logger configuration.
    /// </summary>
    /// <param name="source">The source object associated with the logger.</param>
    /// <returns>The configured ILogger instance.</returns>
    public static ILogger SetupLogger(object source)
    {
        var logger = Log.Logger = new LoggerConfiguration()
            // Default Microsoft logging
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .MinimumLevel.Override("System", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Error)

            // Database specific logging
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Transaction", LogEventLevel.Error)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Connection", LogEventLevel.Error)
            .MinimumLevel.Override("Npgsql", LogEventLevel.Error)
            .MinimumLevel.Override("Npgsql.Command", LogEventLevel.Error)
            .MinimumLevel.Override("Npgsql.Connection", LogEventLevel.Error)

            // Enrichers
            .Enrich.FromLogContext()
            .Enrich.WithProperty("LogSource", source)

            // Output configuration
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                theme: AnsiConsoleTheme.Code,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] | #{LogSource} | " +
                              "{Message:lj}{NewLine}")
            .CreateBootstrapLogger();

        Console.OutputEncoding = Encoding.UTF8;

        return logger;
    }
}