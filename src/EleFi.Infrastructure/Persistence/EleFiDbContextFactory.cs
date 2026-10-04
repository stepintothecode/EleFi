using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EleFi.Infrastructure.Persistence;

/// <summary>
/// Builds a context for the <c>dotnet ef</c> tooling only.
/// </summary>
/// <remarks>
/// <para>
/// Design-time tooling needs a context it can construct without the app's dependency
/// injection, a device, or the SQLCipher key. It never opens the user's database: the
/// connection string points at a throwaway file that only exists while a migration is
/// being scaffolded.
/// </para>
/// <para>
/// This is why no key appears here. A key in a design-time factory would end up in the
/// repository, and the whole point of NFR-5.3 is that it lives in the platform keystore.
/// </para>
/// </remarks>
public sealed class EleFiDbContextFactory : IDesignTimeDbContextFactory<EleFiDbContext>
{
    /// <summary>Creates a context for migration scaffolding.</summary>
    /// <param name="args">Arguments from the tooling. Unused.</param>
    public EleFiDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EleFiDbContext>()
            .UseSqlite("Data Source=elefi-design-time.db")
            .Options;

        return new EleFiDbContext(options);
    }
}
