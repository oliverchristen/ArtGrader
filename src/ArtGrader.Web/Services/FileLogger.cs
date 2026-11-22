using System.Text;

namespace ArtGrader.Web.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int KeepDays = 14;
    private readonly string folder;
    private readonly Lock gate = new();

    public FileLoggerProvider(string folder)
    {
        this.folder = folder;
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.GetFiles(folder, "artgrader-*.log").Where(f => File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-KeepDays)))
        {
            try
            {
                File.Delete(old);
            }
            catch (IOException)
            {
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append(" [").Append(level).Append("] ")
            .Append(category).Append(": ")
            .AppendLine(message);
        if (exception is not null)
        {
            line.AppendLine(exception.ToString());
        }
        lock (gate)
        {
            try
            {
                File.AppendAllText(Path.Combine(folder, $"artgrader-{DateTime.Now:yyyy-MM-dd}.log"), line.ToString());
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(category, logLevel, formatter(state, exception), exception);
            }
        }
    }
}
