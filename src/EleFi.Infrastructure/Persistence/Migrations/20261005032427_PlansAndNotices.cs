using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Planner (S41) and the in-app notification list.
    /// </summary>
    /// <remarks>
    /// Two new tables, nothing changed in existing ones. A plan is never money: it has no
    /// foreign key into a balance, only a nullable link to the transaction it was ticked off
    /// against.
    /// </remarks>
    public partial class PlansAndNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Route = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReadAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CounterpartyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DestinationContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DueOn = table.Column<string>(type: "TEXT", nullable: false),
                    DueTime = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    RepeatFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    AnchorDay = table.Column<int>(type: "INTEGER", nullable: false),
                    SeriesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notices_CreatedAt",
                table: "Notices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notices_Route",
                table: "Notices",
                column: "Route");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_DueOn",
                table: "Plans",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_SeriesId",
                table: "Plans",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_TransactionId",
                table: "Plans",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notices");

            migrationBuilder.DropTable(
                name: "Plans");
        }
    }
}
