using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptReferenceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Receipts_Reference",
                table: "Receipts",
                column: "Reference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_Reference",
                table: "Receipts");
        }
    }
}
