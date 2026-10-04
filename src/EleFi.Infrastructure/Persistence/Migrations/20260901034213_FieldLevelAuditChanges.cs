using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleFi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Clears the audit triggers so startup rebuilds them with field-level changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The originals wrote <c>Changes = NULL</c>, so the trail could say a transaction was
    /// edited but never what the amount went from and to. FR-8.2 asks for exactly that, and
    /// it is the whole reason the timeline is worth opening.
    /// </para>
    /// <para>
    /// This migration only drops them. Creating them belongs to <c>DatabaseInitialiser</c>,
    /// which runs straight after migrating and rebuilds every trigger from the current
    /// definition. Two reasons it cannot happen here:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// A trigger has to match the columns it reads, so pinning its SQL in a migration means a
    /// column added later is silently untracked until somebody remembers to write another
    /// migration. Nothing fails; the trail just quietly stops mentioning that field.
    /// </item>
    /// <item>
    /// A migration that calls into live code is running tomorrow's definition against
    /// yesterday's schema. That is not hypothetical: the trigger set later grew a pair on
    /// <c>TransactionLabels</c>, and every migration that pulled the definition in started
    /// failing on a fresh database with "no such table".
    /// </item>
    /// </list>
    /// <para>
    /// Dropping by name is safe to pin, because a name cannot arrive late the way a column
    /// can, and <c>IF EXISTS</c> covers the ones that were never there.
    /// </para>
    /// </remarks>
    public partial class FieldLevelAuditChanges : Migration
    {
        private static readonly string[] Tables = ["Transactions", "Containers", "Labels", "Apps", "Tags"];

        private static readonly string[] Actions = ["Created", "Updated", "Deleted", "Restored"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => DropTriggers(migrationBuilder);

        /// <inheritdoc />
        /// <remarks>
        /// Down leaves them dropped rather than restoring the older shape. Startup recreates
        /// them from the current definition anyway, so putting a stale version back here would
        /// only be undone moments later.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder) => DropTriggers(migrationBuilder);

        private static void DropTriggers(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                foreach (var action in Actions)
                {
                    migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{table}_{action};");
                }
            }
        }
    }
}
