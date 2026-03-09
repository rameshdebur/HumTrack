using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace HumTrack.Core.Diagnostics;

/// <summary>
/// Centralized logging service for HumTrack.
/// Writes structured logs to both console and rolling log files.
/// All tracking engine errors, calibration events, and performance metrics
/// are routed through this service for debugging and audit trails.
/// </summary>
public static class HumTrackLogger
{
    private static Logger? _logger;
    private static readonly object Lock = new();
    private static bool _initialized;

    /// <summary>
    /// Gets the configured Serilog logger instance.
    /// Auto-initializes with defaults if not explicitly configured.
    /// </summary>
    public static ILogger Log
    {
        get
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _logger!;
        }
    }

    /// <summary>
    /// Initialize the logging system with file + console sinks.
    /// Call once at application startup.
    /// </summary>
    /// <param name="logDirectory">
    /// Directory for log files. Defaults to "logs" in the working directory.
    /// </param>
    /// <param name="minimumLevel">
    /// Minimum severity to log. Default: Debug in debug builds, Information in release.
    /// </param>
    public static void Initialize(
        string? logDirectory = null,
        LogEventLevel? minimumLevel = null)
    {
        lock (Lock)
        {
            if (_initialized)
            {
                return;
            }

            var logDir = logDirectory ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "logs");

            Directory.CreateDirectory(logDir);

            var level = minimumLevel ??
#if DEBUG
                LogEventLevel.Debug;
#else
                LogEventLevel.Information;
#endif

#pragma warning disable CA1305 // Serilog uses its own template formatting
            var config = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .Enrich.WithProperty("Application", "HumTrack")
                .Enrich.WithProperty("Version",
                    typeof(HumTrackLogger).Assembly.GetName().Version?.ToString() ?? "dev")
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logDir, "humtrack-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: 50 * 1024 * 1024, // 50 MB per file
                    rollOnFileSizeLimit: true)
                .WriteTo.File(
                    path: Path.Combine(logDir, "humtrack-errors-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90,
                    restrictedToMinimumLevel: LogEventLevel.Error,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
#pragma warning restore CA1305

            _logger = config.CreateLogger();
            _initialized = true;

            _logger.Information("HumTrack logging initialized. Log directory: {LogDirectory}", logDir);
        }
    }

    /// <summary>
    /// Creates a logger for a specific source context (class/module name).
    /// Use this in each class to tag log entries with their origin.
    /// </summary>
    /// <typeparam name="T">The class requesting the logger.</typeparam>
    /// <returns>A logger tagged with the class name.</returns>
    public static ILogger ForContext<T>()
    {
        return Log.ForContext<T>();
    }

    /// <summary>
    /// Creates a logger for a tracking engine by name.
    /// </summary>
    /// <param name="engineName">The engine's display name.</param>
    /// <returns>A logger tagged with the engine name.</returns>
    public static ILogger ForEngine(string engineName)
    {
        return Log.ForContext("TrackingEngine", engineName);
    }

    /// <summary>
    /// Flushes all buffered log entries and releases resources.
    /// Call at application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        lock (Lock)
        {
            if (_logger is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _logger = null;
            _initialized = false;
        }
    }
}
