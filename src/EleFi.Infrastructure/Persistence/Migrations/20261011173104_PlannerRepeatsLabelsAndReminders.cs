using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlannerRepeatsLabelsAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "Plans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Occurrence",
                table: "Plans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RemindOn",
                table: "Plans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatCount",
                table: "Plans",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatDays",
                table: "Plans",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RepeatUntil",
                table: "Plans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlanLabels",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanLabels", x => new { x.PlanId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_PlanLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanLabels_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanLabels_LabelId",
                table: "PlanLabels",
                column: "LabelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanLabels");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Occurrence",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "RemindOn",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "RepeatCount",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "RepeatDays",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "RepeatUntil",
                table: "Plans");
        }
    }
}
