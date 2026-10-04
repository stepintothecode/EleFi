using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Payment App notifications as a second alert channel (ADR-0014).
    /// </summary>
    /// <remarks>
    /// Additive only. Rules learn which channel they read and which App they belong to;
    /// suggestions learn which channels described them, the App, note and time an App
    /// supplied, and the fingerprint of a second alert merged into them, so a reposted
    /// notification is still recognised as a duplicate (SM6). No message text is stored,
    /// here or anywhere (SM1).
    /// </remarks>
    public partial class PaymentAppAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppName",
                table: "ParseRules",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "ParseRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CorroboratingFingerprint",
                table: "CaptureSuggestions",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Evidence",
                table: "CaptureSuggestions",
                type: "INTEGER",
                nullable: false,

                // Every suggestion before this migration came from an SMS (AlertEvidence.Sms).
                // 0 would mean "no source at all", which no real suggestion has.
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "CaptureSuggestions",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OccurredAtTime",
                table: "CaptureSuggestions",
                type: "TEXT",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentAppName",
                table: "CaptureSuggestions",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaptureSuggestions_CorroboratingFingerprint",
                table: "CaptureSuggestions",
                column: "CorroboratingFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_CaptureSuggestions_CreatedAt",
                table: "CaptureSuggestions",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaptureSuggestions_CorroboratingFingerprint",
                table: "CaptureSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_CaptureSuggestions_CreatedAt",
                table: "CaptureSuggestions");

            migrationBuilder.DropColumn(
                name: "AppName",
                table: "ParseRules");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "ParseRules");

            migrationBuilder.DropColumn(
                name: "CorroboratingFingerprint",
                table: "CaptureSuggestions");

            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "CaptureSuggestions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "CaptureSuggestions");

            migrationBuilder.DropColumn(
                name: "OccurredAtTime",
                table: "CaptureSuggestions");

            migrationBuilder.DropColumn(
                name: "PaymentAppName",
                table: "CaptureSuggestions");
        }
    }
}
