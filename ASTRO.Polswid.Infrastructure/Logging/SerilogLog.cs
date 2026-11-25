using ASTRO.Polswid.Core.Diagnostics;
using Serilog;
using Serilog.Formatting.Json;

namespace ASTRO.Polswid.Infrastructure.Logging;

/// <summary>Structured JSON-lines file logging behind the Core ILog abstraction.</summary>
public sealed class SerilogLog : ILog, IDisposable
{
    readonly Serilog.Core.Logger _logger;
    public string Directory { get; }

    public SerilogLog(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(new JsonFormatter(), Path.Combine(directory, "astro-polswid-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
    }

    public void Debug(string template, params object?[] args) => _logger.Debug(template, args);
    public void Information(string template, params object?[] args) => _logger.Information(template, args);
    public void Warning(string template, params object?[] args) => _logger.Warning(template, args);
    public void Error(Exception? ex, string template, params object?[] args) => _logger.Error(ex, template, args);
    public void Dispose() => _logger.Dispose();
}
