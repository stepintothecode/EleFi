using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransactionItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionItems", x => x.Id);
                    table.CheckConstraint("CK_TransactionItems_PositiveAmount", "AmountMinor > 0");
                    table.ForeignKey(
                        name: "FK_TransactionItems_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionItemLabels",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionItemLabels", x => new { x.ItemId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_TransactionItemLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionItemLabels_TransactionItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "TransactionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItemLabels_LabelId",
                table: "TransactionItemLabels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItems_TransactionId_Position",
                table: "TransactionItems",
                columns: new[] { "TransactionId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionItemLabels");

            migrationBuilder.DropTable(
                name: "TransactionItems");
        }
    }
}
