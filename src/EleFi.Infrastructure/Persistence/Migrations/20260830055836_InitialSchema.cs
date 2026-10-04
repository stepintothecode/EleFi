using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Apps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DefaultContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    Colour = table.Column<string>(type: "TEXT", nullable: true),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Changes = table.Column<string>(type: "TEXT", nullable: true),
                    CaptureSource = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaptureSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParseRuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CounterpartyText = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OccurredOn = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaptureSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    OpeningBalanceMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    OpeningBalanceAsOf = table.Column<string>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Colour = table.Column<string>(type: "TEXT", nullable: true),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    InstitutionName = table.Column<string>(type: "TEXT", nullable: true),
                    AccountNumberLast4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    Ifsc = table.Column<string>(type: "TEXT", nullable: true),
                    CreditLimitMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    StatementDay = table.Column<int>(type: "INTEGER", nullable: true),
                    PaymentDueDay = table.Column<int>(type: "INTEGER", nullable: true),
                    InterestRateBps = table.Column<int>(type: "INTEGER", nullable: true),
                    MaturityDate = table.Column<string>(type: "TEXT", nullable: true),
                    TenureMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    InstallmentMinor = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AppliesTo = table.Column<int>(type: "INTEGER", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    Colour = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Labels_Labels_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParseRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SenderPattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BodyPattern = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParseRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Colour = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourcePartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceCurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    DestinationPartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DestinationAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    DestinationCurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    OccurredOn = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LabelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MarketplaceAppId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PaymentAppId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NeedsReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    CaptureSource = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transactions_DistinctParties", "SourcePartyId <> DestinationPartyId");
                    table.CheckConstraint("CK_Transactions_PositiveAmounts", "SourceAmountMinor > 0 AND DestinationAmountMinor > 0");
                    table.CheckConstraint("CK_Transactions_SameCurrencySameAmount", "SourceCurrencyCode <> DestinationCurrencyCode OR SourceAmountMinor = DestinationAmountMinor");
                    table.ForeignKey(
                        name: "FK_Transactions_Apps_MarketplaceAppId",
                        column: x => x.MarketplaceAppId,
                        principalTable: "Apps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Apps_PaymentAppId",
                        column: x => x.PaymentAppId,
                        principalTable: "Apps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Parties_DestinationPartyId",
                        column: x => x.DestinationPartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Parties_SourcePartyId",
                        column: x => x.SourcePartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTags",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTags", x => new { x.TransactionId, x.TagId });
                    table.ForeignKey(
                        name: "FK_TransactionTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionTags_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_EntityId",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaptureSuggestions_Fingerprint",
                table: "CaptureSuggestions",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Containers_AccountNumberLast4",
                table: "Containers",
                column: "AccountNumberLast4");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_ParentId",
                table: "Labels",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_ContainerId",
                table: "Parties",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_Name",
                table: "Parties",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_DestinationPartyId",
                table: "Transactions",
                column: "DestinationPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_LabelId",
                table: "Transactions",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_MarketplaceAppId",
                table: "Transactions",
                column: "MarketplaceAppId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OccurredOn",
                table: "Transactions",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentAppId",
                table: "Transactions",
                column: "PaymentAppId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourcePartyId",
                table: "Transactions",
                column: "SourcePartyId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTags_TagId",
                table: "TransactionTags",
                column: "TagId");

            // AU2: the audit trail is written by the database, not by application code, so no
            // path can bypass it. The triggers themselves are NOT created here.
            //
            // They used to be, by calling AuditTriggers.CreateStatements. That is a migration
            // depending on today's code, and it broke the moment a later migration introduced
            // a table the triggers reference: replaying this migration on a fresh database
            // failed with "no such table: TransactionLabels", because the definition it pulled
            // in was three migrations ahead of the schema it was running against.
            //
            // DatabaseInitialiser drops and recreates every trigger after migrating, so there
            // is exactly one place that knows what the triggers are, and it always sees the
            // finished schema.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // By name, not from AuditTriggers: a down path has to describe the world as it was
            // when this migration ran, and a live list would describe today's.
            foreach (var table in new[] { "Transactions", "Containers", "Labels", "Apps", "Tags" })
            {
                foreach (var action in new[] { "Created", "Updated", "Deleted", "Restored" })
                {
                    migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{table}_{action};");
                }
            }

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "CaptureSuggestions");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "ParseRules");

            migrationBuilder.DropTable(
                name: "TransactionTags");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Apps");

            migrationBuilder.DropTable(
                name: "Labels");

            migrationBuilder.DropTable(
                name: "Parties");
        }
    }
}
