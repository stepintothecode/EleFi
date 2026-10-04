using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Application.Export;
using EleFi.Application.Labels;
using EleFi.Application.Suggestions;
using EleFi.Application.Transactions;
using EleFi.Infrastructure.Persistence;
using EleFi.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EleFi.Infrastructure;

/// <summary>
/// Registers everything the app needs from this layer.
/// </summary>
/// <remarks>
/// This is the only member of <c>EleFi.Infrastructure</c> that <c>EleFi.App</c> is allowed
/// to name. An architecture test fails the build if a view model reaches past it to a
/// <c>DbContext</c>, because a view model that can see one will eventually use one.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// Wires up the encrypted database, the repositories, and the use cases.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Full path to the database file.</param>
    /// <param name="encryptionKey">
    /// The SQLCipher key, read from the platform keystore. Never a literal, never in
    /// configuration, and never in the repository (NFR-5.3).
    /// </param>
    public static IServiceCollection AddEleFiInfrastructure(
        this IServiceCollection services,
        string databasePath,
        string encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionKey);

        // Microsoft.Data.Sqlite emits PRAGMA key from the Password keyword, which works
        // because the only SQLite engine present is the SQLCipher build. Referencing
        // EF Core's full Sqlite package would also pull the plain engine, and whichever
        // provider registered last would win: the database would open unencrypted and
        // nothing would say so. That is why the csproj uses Sqlite.Core.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = encryptionKey,
            ForeignKeys = true,
        }.ToString();

        services.AddDbContext<EleFiDbContext>(options => options
            .UseSqlite(connectionString)
            .EnableSensitiveDataLogging(false));

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IContainerRepository, ContainerRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IPartyRepository, PartyRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<IAppRepository, AppRepository>();
        services.AddScoped<ISuggestionRepository, SuggestionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddScoped<ContainerService>();
        services.AddScoped<LabelService>();
        services.AddScoped<CaptureService>();
        services.AddScoped<EditTransactionService>();
        services.AddScoped<CsvExporter>();
        services.AddScoped<SuggestionService>();
        services.AddScoped<AlertCaptureSettings>();
        services.AddScoped<IncomingAlertHandler>();
        services.AddScoped<DatabaseInitialiser>();
        services.AddScoped<DataWipe>();
        services.AddScoped<IDataWipe>(sp => sp.GetRequiredService<DataWipe>());
        services.AddScoped<ILocalBackup, LocalBackup>();

        return services;
    }
}
