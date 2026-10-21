using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Receipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    AppName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Payee = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    OccurredOn = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredAtTime = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AccountLast4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    SettledAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.CheckConstraint("CK_Receipts_AccountLast4", "AccountLast4 IS NULL OR length(AccountLast4) = 4");
                    table.CheckConstraint("CK_Receipts_PositiveAmount", "AmountMinor IS NULL OR AmountMinor > 0");
                    table.ForeignKey(
                        name: "FK_Receipts_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptImages",
                columns: table => new
                {
                    ReceiptId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Photo = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Thumbnail = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptImages", x => x.ReceiptId);
                    table.ForeignKey(
                        name: "FK_ReceiptImages_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TransactionId",
                table: "Receipts",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptImages");

            migrationBuilder.DropTable(
                name: "Receipts");
        }
    }
}
