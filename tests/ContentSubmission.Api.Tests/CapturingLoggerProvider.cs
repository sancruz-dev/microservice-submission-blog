using Microsoft.Extensions.Logging;

namespace ContentSubmission.Api.Tests;

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public List<object?> CapturedScopes { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose() { }

    private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            provider.CapturedScopes.Add(state);
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        { }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}