using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GarageFlow.Tests.Integration.Support.Helpers;

public sealed class StartupFailureLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<Exception> _exceptions = new();

    public IReadOnlyCollection<Exception> Exceptions => _exceptions.ToArray();

    public ILogger CreateLogger(string categoryName) => new ExceptionLogger(_exceptions);

    public void Dispose()
    {
    }

    private sealed class ExceptionLogger(ConcurrentQueue<Exception> exceptions) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel) && exception is not null)
            {
                exceptions.Enqueue(exception);
            }
        }
    }
}
