using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Contacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoPartyId",
                table: "Parties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Parties",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneContactKey",
                table: "Parties",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContactDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactDetails_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OtherPartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Relation = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactLinks", x => x.Id);
                    table.CheckConstraint("CK_ContactLinks_DistinctEnds", "PartyId <> OtherPartyId");
                    table.ForeignKey(
                        name: "FK_ContactLinks_Parties_OtherPartyId",
                        column: x => x.OtherPartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactLinks_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_MergedIntoPartyId",
                table: "Parties",
                column: "MergedIntoPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactDetails_PartyId",
                table: "ContactDetails",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLinks_OtherPartyId",
                table: "ContactLinks",
                column: "OtherPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactLinks_PartyId_OtherPartyId",
                table: "ContactLinks",
                columns: new[] { "PartyId", "OtherPartyId" },
                unique: true,
                filter: "DeletedAt IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactDetails");

            migrationBuilder.DropTable(
                name: "ContactLinks");

            migrationBuilder.DropIndex(
                name: "IX_Parties_MergedIntoPartyId",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "MergedIntoPartyId",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "PhoneContactKey",
                table: "Parties");
        }
    }
}
