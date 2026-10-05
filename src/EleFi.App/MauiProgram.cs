using EleFi.App.Services;
using EleFi.Application.Abstractions;
using EleFi.Infrastructure;
using EleFi.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace EleFi.App;

/// <summary>
/// Composes the app: the one place that knows both the abstractions and the things that
/// implement them.
/// </summary>
public static class MauiProgram
{
    /// <summary>Builds the application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

        // The only diagnosis channel this app has. There is no crash reporting and there
        // will not be: NFR-5.8 forbids transmitting anything and there is no server to
        // transmit to. Without this, a failure that stops the app opening leaves nothing
        // behind but a spinner, which is exactly how one shipped.
        // Registered through the container rather than Logging.AddProvider so ownership is
        // explicit: the container creates it and the container disposes it.
        builder.Services.AddSingleton<ILoggerProvider>(_ => new FileErrorLog(FileErrorLog.DefaultPath));

        // The SQLCipher key comes from the platform keystore, never from configuration and
        // never from a literal. Blocking here is deliberate: the database must be keyed and
        // migrated before the first render, because a half-ready database renders a screen
        // showing wrong money.
        //
        // Task.Run first, though. This runs on the Android main thread, which has a
        // synchronisation context, and blocking it on a continuation that wants that same
        // thread is a deadlock. It happens not to bite today; it would bite on a slower
        // device or a colder keystore, and the failure would be a hang with no message.
        var key = Task.Run(DatabaseKey.GetOrCreateAsync).GetAwaiter().GetResult();
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "elefi.db");

        builder.Services.AddEleFiInfrastructure(databasePath, key);

        // Platform surfaces, each behind the interface declared in EleFi.Application.
        builder.Services.AddSingleton<ILinkOpener, LinkOpener>();
        builder.Services.AddSingleton<IFileShare, ShareSheet>();
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddSingleton<ILaunchIntent, AndroidLaunchIntent>();
        builder.Services.AddSingleton<IFilePick, FilePick>();
        builder.Services.AddSingleton<ISystemBack, MauiSystemBack>();
        builder.Services.AddSingleton<IAlertPromptSurface, AndroidAlertPrompts>();
        builder.Services.AddSingleton<IAlertAccess, AndroidAlertAccess>();

        // Scoped, matching the WebView's lifetime, so a message raised on one page is still
        // on screen after navigating to another.
        builder.Services.AddScoped<EleFi.Ui.Services.ToastService>();

        // Scoped for the same reason: one history per WebView, kept across page changes.
        builder.Services.AddScoped<EleFi.Ui.Services.BackNavigator>();
        builder.Services.AddScoped<EleFi.Ui.Services.TransactionListState>();

        // Scoped too: the layout and the dashboard must see the same switch.
        builder.Services.AddScoped<EleFi.Ui.Services.PrivacyMode>();
        builder.Services.AddScoped<EleFi.Ui.Services.AppSwitcherPrivacy>();

        // Transient, not scoped. MascotViewer owns a canvas and disposes its service when it
        // unmounts, so each viewer needs its own instance. Sharing one meant navigating away
        // from the dashboard disposed the shared service and the mascot never animated again.
        builder.Services.AddTransient<IMascotService, MascotService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        InitialiseDatabase(app.Services);

        return app;
    }

    /// <summary>
    /// Runs migrations and seeds before anything renders.
    /// </summary>
    /// <remarks>
    /// Inside a scope, because the context is scoped. Failures are deliberately not
    /// swallowed: an app that starts with an unmigrated database would show balances
    /// computed from a schema that does not match the code.
    /// </remarks>
    private static void InitialiseDatabase(IServiceProvider services)
    {
        // Off the main thread for the same reason as the key above: migrating an encrypted
        // database is real I/O, and blocking the Android main looper on it invites a
        // deadlock that presents as a frozen splash screen.
        Task.Run(async () =>
        {
            using var scope = services.CreateScope();
            var initialiser = scope.ServiceProvider.GetRequiredService<DatabaseInitialiser>();

            await initialiser.InitialiseAsync().ConfigureAwait(false);
        }).GetAwaiter().GetResult();
    }
}

/// <summary>Stores small settings in MAUI Preferences.</summary>
/// <remarks>
/// Preferences, not the database: these are per-device choices such as theme, not user
/// data that belongs in a backup. Never money and never a balance, because a cached
/// balance is a second source of truth waiting to disagree with the derived one.
/// </remarks>
public sealed class PreferencesSettingsStore : ISettingsStore
{
    /// <inheritdoc />
    public string Read(string key, string fallback) => Preferences.Default.Get(key, fallback);

    /// <inheritdoc />
    public void Write(string key, string value) => Preferences.Default.Set(key, value);
}
