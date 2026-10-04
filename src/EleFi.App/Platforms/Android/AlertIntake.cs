using EleFi.Application.Suggestions;
using Microsoft.Extensions.Logging;

namespace EleFi.App.Platforms.Android;

/// <summary>
/// Runs alert handling in its own DI scope, off the thread the system called in on.
/// </summary>
/// <remarks>
/// <para>
/// Shared by the SMS receiver, the notification listener, and the prompt's Dismiss action,
/// so each of them is a few lines of Android plumbing and nothing more. Everything that
/// decides anything lives in <see cref="IncomingAlertHandler"/>, which is tested without a
/// device.
/// </para>
/// <para>
/// <b>Failures are logged by exception type only.</b> An exception message here could
/// echo the alert it was handling, and SM1 forbids that text ever reaching a log.
/// </para>
/// </remarks>
internal static class AlertIntake
{
    /// <summary>Resolves the handler in a fresh scope and runs the work.</summary>
    /// <param name="work">What to do with the handler.</param>
    public static async Task RunAsync(Func<IncomingAlertHandler, Task> work)
    {
        // Set by MainApplication. A receiver that wakes a cold process still gets here
        // after the app object, and with it the database, has been built.
        var services = IPlatformApplication.Current?.Services;
        if (services is null)
        {
            return;
        }

        try
        {
            await using var scope = services.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IncomingAlertHandler>();
            await work(handler).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A receiver must never crash the process over one alert.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            services.GetService<ILoggerFactory>()
                ?.CreateLogger("EleFi.Alerts")
                .LogError("Alert intake failed with {ExceptionType}.", ex.GetType().Name);
        }
    }
}
