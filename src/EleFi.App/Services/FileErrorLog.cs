using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EleFi.App.Services;

/// <summary>
/// Writes errors to a file on the device, so a failure that stops the app opening leaves
/// evidence behind.
/// </summary>
/// <remarks>
/// <para>
/// EleFi has no crash reporting and never will: NFR-5.8 forbids transmitting anything, and
/// there is no server to transmit to. That leaves exactly one place a diagnosis can come
/// from, which is the device itself.
/// </para>
/// <para>
/// Read it with:
/// <code>adb shell run-as com.stepintothecode.elefi cat files/elefi-errors.log</code>
/// </para>
/// <para>
/// <b>NFR-5.9 still applies.</b> Only the exception type, message, and stack are written:
/// no log scopes, no state, no message arguments. EF Core runs with sensitive data logging
/// off, so its exceptions carry SQL shape but never parameter values. If a future exception
/// message could carry an amount or a party name, it does not belong in an exception
/// message in the first place.
/// </para>
/// </remarks>
public sealed class FileErrorLog(string path) : ILoggerProvider
{
    private const long MaxBytes = 128 * 1024;

    private readonly Lock _gate = new();

    /// <summary>Where the log is written.</summary>
    public static string DefaultPath => Path.Combine(FileSystem.AppDataDirectory, "elefi-errors.log");

    /// <summary>The most recent entries, for showing the user what went wrong.</summary>
    /// <param name="path">The log file.</param>
    public static string Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Writer(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing held open: each write opens and closes, because a process that is dying
        // does not reliably flush a stream it was holding.
    }

    private void Append(string category, LogLevel level, Exception? exception, string message)
    {
        var entry = new StringBuilder()
            .Append(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
            .Append("  [").Append(level).Append("]  ")
            .AppendLine(category)
            .AppendLine(message);

        if (exception is not null)
        {
            entry.AppendLine(exception.GetType().FullName)
                 .AppendLine(exception.Message)
                 .AppendLine(exception.StackTrace);
        }

        entry.AppendLine(new string('-', 60));

        lock (_gate)
        {
            try
            {
                // Keep it bounded. A crash loop would otherwise fill the device.
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    File.Delete(path);
                }

                File.AppendAllText(path, entry.ToString());
            }
            catch (IOException)
            {
                // Diagnostics must never be the thing that breaks the app.
            }
            catch (UnauthorizedAccessException)
            {
                // As above.
            }
        }
    }

    private sealed class Writer(FileErrorLog owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        // Errors only. An app that logs everything to disk on a phone is a battery and
        // storage problem, and this exists for the one case where nothing else can speak.
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

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

            ArgumentNullException.ThrowIfNull(formatter);
            owner.Append(category, logLevel, exception, formatter(state, exception));
        }
    }
}
