using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One label per transaction becomes many, flat, and optional. Tags go away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// See ADR-0013. The scaffolded version of this migration dropped
    /// <c>Transactions.LabelId</c> before creating the join table, which would have thrown
    /// away every label the user had assigned. The order below moves the data first.
    /// </para>
    /// <para>
    /// Two deliberate data decisions:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Transactions pointing at the old <c>Uncategorised</c> label become unlabelled rather
    /// than carrying it forward. Nobody chose that label; capture assigned it because the
    /// old model demanded one. Carrying it over would give every historical row a
    /// meaningless tag.
    /// </item>
    /// <item>
    /// Names are de-duplicated before the unique index. Flattening a hierarchy can collide:
    /// "Groceries" under Food and "Groceries" under Household were legal and are now the
    /// same name.
    /// </item>
    /// </list>
    /// </remarks>
    public partial class MultipleFlatLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The join table first, so there is somewhere for the data to go.
            migrationBuilder.CreateTable(
                name: "TransactionLabels",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLabels", x => new { x.TransactionId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_TransactionLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionLabels_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Carry the existing assignments across, skipping the system label.
            migrationBuilder.Sql("""
                INSERT INTO TransactionLabels (TransactionId, LabelId)
                SELECT t.Id, t.LabelId
                FROM Transactions AS t
                WHERE t.LabelId IS NOT NULL
                  AND t.LabelId NOT IN (SELECT Id FROM Labels WHERE IsSystem = 1);
                """);

            // 3. The system label has no meaning now that unlabelled is a real state.
            migrationBuilder.Sql("DELETE FROM Labels WHERE IsSystem = 1;");

            // 4. Flattening can collide. Rank by creation and suffix all but the oldest,
            //    computed in a CTE so the ranks do not shift as rows are updated.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY Name COLLATE NOCASE
                               ORDER BY CreatedAt, Id
                           ) AS rn
                    FROM Labels
                    WHERE DeletedAt IS NULL
                )
                UPDATE Labels
                SET Name = Name || ' (' || (SELECT rn FROM ranked WHERE ranked.Id = Labels.Id) || ')'
                WHERE DeletedAt IS NULL
                  AND (SELECT rn FROM ranked WHERE ranked.Id = Labels.Id) > 1;
                """);

            // Safety net, for the case where the suffixed name already existed.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY Name COLLATE NOCASE
                               ORDER BY CreatedAt, Id
                           ) AS rn
                    FROM Labels
                    WHERE DeletedAt IS NULL
                )
                UPDATE Labels
                SET Name = Name || ' ' || Id
                WHERE DeletedAt IS NULL
                  AND (SELECT rn FROM ranked WHERE ranked.Id = Labels.Id) > 1;
                """);

            // 5. Only now is the old shape safe to remove.
            migrationBuilder.DropForeignKey(name: "FK_Labels_Labels_ParentId", table: "Labels");
            migrationBuilder.DropForeignKey(name: "FK_Transactions_Labels_LabelId", table: "Transactions");

            migrationBuilder.DropTable(name: "TransactionTags");
            migrationBuilder.DropTable(name: "Tags");

            migrationBuilder.DropIndex(name: "IX_Transactions_LabelId", table: "Transactions");
            migrationBuilder.DropIndex(name: "IX_Labels_ParentId", table: "Labels");

            migrationBuilder.DropColumn(name: "LabelId", table: "Transactions");
            migrationBuilder.DropColumn(name: "AppliesTo", table: "Labels");
            migrationBuilder.DropColumn(name: "IsSystem", table: "Labels");
            migrationBuilder.DropColumn(name: "ParentId", table: "Labels");

            // 6. NOCASE, matching what LabelService checks, so the database and the code
            //    agree about what is legal.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IX_Labels_Name
                ON Labels (Name COLLATE NOCASE)
                WHERE DeletedAt IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLabels_LabelId",
                table: "TransactionLabels",
                column: "LabelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately lossy and deliberately loud about it. Going back means choosing
            // one label per transaction out of several, and there is no honest way to pick.
            // Restoring the columns without the data is the least misleading option.
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Labels_Name;");
            migrationBuilder.DropTable(name: "TransactionLabels");

            migrationBuilder.AddColumn<Guid>(name: "LabelId", table: "Transactions", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<int>(name: "AppliesTo", table: "Labels", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<bool>(name: "IsSystem", table: "Labels", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<Guid>(name: "ParentId", table: "Labels", type: "TEXT", nullable: true);
        }
    }
}
