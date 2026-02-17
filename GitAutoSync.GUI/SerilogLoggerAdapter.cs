using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace GitAutoSync.GUI;

public class NoOpDisposable : IDisposable
{
  public void Dispose()
  {
    // No operation needed
  }
}

public class SerilogLoggerAdapter<T> : ILogger<T>
{
  private readonly Serilog.ILogger _logger;

  public SerilogLoggerAdapter(Serilog.ILogger logger)
  {
    _logger = logger;
  }

  public IDisposable BeginScope<TState>(TState state) where TState : notnull
  {
    // Serilog doesn't have traditional scopes, but we can simulate with ForContext
    return new NoOpDisposable();
  }

  public bool IsEnabled(LogLevel logLevel)
  {
    return _logger.IsEnabled(ConvertLogLevel(logLevel));
  }

  public void Log<TState>(
    LogLevel logLevel,
    EventId eventId,
    TState state,
    Exception? exception,
    Func<TState, Exception?, string> formatter)
  {
    if (!IsEnabled(logLevel))
    {
      return;
    }

    string message = formatter(state, exception);
    LogEventLevel serilogLevel = ConvertLogLevel(logLevel);

    _logger.Write(serilogLevel, exception, message);
  }

  private static LogEventLevel ConvertLogLevel(LogLevel logLevel)
  {
    return logLevel switch
    {
      LogLevel.Trace => LogEventLevel.Verbose,
      LogLevel.Debug => LogEventLevel.Debug,
      LogLevel.Information => LogEventLevel.Information,
      LogLevel.Warning => LogEventLevel.Warning,
      LogLevel.Error => LogEventLevel.Error,
      LogLevel.Critical => LogEventLevel.Fatal,
      LogLevel.None => LogEventLevel.Fatal,
      _ => LogEventLevel.Information,
    };
  }
}