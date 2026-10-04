using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransactionTimeInvestmentKindsAndUniqueContainerNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OccurredAtTime",
                table: "Transactions",
                type: "TEXT",
                maxLength: 5,
                nullable: true);

            // Existing databases already contain duplicate names, because nothing stopped
            // them. Creating the unique index first would fail, and a migration that throws
            // on startup leaves the app unable to open with the user's data still inside.
            // So: rename the duplicates, keeping the oldest of each set untouched.
            //
            // " (2)", " (3)" and so on, by creation order. Not deleted and not merged: two
            // containers with the same name may well be two real accounts, and only the
            // person who made them knows.
            // Ranks come from a CTE rather than a correlated count over the same table. A
            // correlated count is evaluated per row against rows already updated, so the
            // second and third "HDFC" both saw one earlier match and both became "(2)",
            // which fails the very index this is clearing the way for.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY Name COLLATE NOCASE
                               ORDER BY CreatedAt, Id
                           ) AS rn
                    FROM Containers
                    WHERE DeletedAt IS NULL
                )
                UPDATE Containers
                SET Name = Name || ' (' || (SELECT rn FROM ranked WHERE ranked.Id = Containers.Id) || ')'
                WHERE DeletedAt IS NULL
                  AND (SELECT rn FROM ranked WHERE ranked.Id = Containers.Id) > 1;
                """);

            // Safety net for the pathological case: a container genuinely called "HDFC (2)"
            // already existed, so the rename above collided with it. Falls back to the id,
            // which is unique by construction. An ugly name the user can fix in one tap
            // beats a migration that throws and an app that will not open.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY Name COLLATE NOCASE
                               ORDER BY CreatedAt, Id
                           ) AS rn
                    FROM Containers
                    WHERE DeletedAt IS NULL
                )
                UPDATE Containers
                SET Name = Name || ' ' || Id
                WHERE DeletedAt IS NULL
                  AND (SELECT rn FROM ranked WHERE ranked.Id = Containers.Id) > 1;
                """);

            // NOCASE, so the index enforces exactly what ContainerService checks. A
            // case-sensitive index would let "HDFC" and "hdfc" both exist while the
            // application refused to create the second, which is worse than either rule
            // alone: the database and the code would disagree about what is legal.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IX_Containers_Name
                ON Containers (Name COLLATE NOCASE)
                WHERE DeletedAt IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Raw SQL on the way up, so raw SQL on the way down. Renamed duplicates are not
            // restored: the rename carried information the old name did not, and guessing
            // which "HDFC (2)" used to be plain "HDFC" would be inventing history.
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Containers_Name;");

            migrationBuilder.DropColumn(
                name: "OccurredAtTime",
                table: "Transactions");
        }
    }
}
