namespace EleFi.Infrastructure.Persistence;

/// <summary>
/// The SQL that writes the audit trail.
/// </summary>
/// <remarks>
/// <para>
/// <b>Triggers, not application code (AU2).</b> An EF Core <c>SaveChanges</c> interceptor
/// was available and is deliberately not used: it sees only writes that pass through the
/// <c>DbContext</c>, and this app has paths that will not. A restore that swaps the
/// database file, a seeded performance dataset, a future bulk import, and anything written
/// from a background service all bypass it. A trail with holes in exactly those places is
/// worse than no trail, because it looks complete.
/// </para>
/// <para>
/// The cost is real and worth stating: these must be maintained alongside every schema
/// change. That is the main argument against this approach, and it was accepted knowingly.
/// The <see cref="TrackedFields"/> map is the part that goes stale, so a new column that
/// matters is a line there as well as a line in the model.
/// </para>
/// <para>
/// <b>Never call this from a migration.</b> <see cref="DatabaseInitialiser"/> owns applying
/// it, after migrating, and is the only caller. A migration that pulls this in is running
/// today's definition against an older schema: when the trigger set grew a pair on
/// <c>TransactionLabels</c>, every migration that referenced it began failing on a fresh
/// database with "no such table", three migrations before that table exists. A migration
/// that needs triggers gone drops them by name.
/// </para>
/// <para>
/// <b>On storing values.</b> FR-8.2 requires field-level before and after, so the trail
/// holds amounts and names. That is not a breach of NFR-5.9, which is about <em>logs</em>:
/// this is user-facing history inside the encrypted database, and it is the whole reason a
/// surprising number can be explained six months later.
/// </para>
/// </remarks>
public static class AuditTriggers
{
    /// <summary>
    /// The columns whose changes are recorded, per table.
    /// </summary>
    /// <remarks>
    /// Deliberately not every column. <c>UpdatedAt</c> changes on every write and recording
    /// it would mean every entry lists a field nobody cares about; <c>DeletedAt</c> is
    /// covered by the delete and restore triggers instead.
    /// </remarks>
    public static IReadOnlyDictionary<string, string[]> TrackedFields { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Transactions"] =
            [
                "SourcePartyId", "DestinationPartyId",
                "SourceAmountMinor", "SourceCurrencyCode",
                "DestinationAmountMinor", "DestinationCurrencyCode",
                "OccurredOn", "OccurredAtTime", "Description",
                "MarketplaceAppId", "PaymentAppId",
                "NeedsReview", "CaptureSource",

                // Labels are not here. They live in a join table, so this trigger cannot see
                // them. TransactionLabels has its own pair of triggers instead: see
                // LabelTriggers below. Without those, changing only a transaction's labels
                // would leave no trace at all, because the WHEN clause here needs a tracked
                // column on this table to differ and UpdatedAt is not one.
            ],
            ["Containers"] =
            [
                "Name", "Kind", "CurrencyCode",
                "OpeningBalanceMinor", "OpeningBalanceAsOf",
                "IsArchived", "InstitutionName", "AccountNumberLast4", "Ifsc",
                "CreditLimitMinor", "StatementDay", "PaymentDueDay", "Colour",
            ],
            ["Labels"] = ["Name", "Colour", "SortOrder"],
            ["Apps"] = ["Name", "DefaultContainerId", "Colour"],
        };

    /// <summary>Tables whose changes are recorded.</summary>
    public static IReadOnlyList<string> AuditedTables { get; } = [.. TrackedFields.Keys];

    /// <summary>The statements that create every trigger.</summary>
    public static IReadOnlyList<string> CreateStatements => Build();

    /// <summary>The statements that drop every trigger, for a migration's down path.</summary>
    public static IReadOnlyList<string> DropStatements =>
    [
        .. AuditedTables.SelectMany(t => new[]
        {
            $"DROP TRIGGER IF EXISTS trg_{t}_Created;",
            $"DROP TRIGGER IF EXISTS trg_{t}_Updated;",
            $"DROP TRIGGER IF EXISTS trg_{t}_Deleted;",
            $"DROP TRIGGER IF EXISTS trg_{t}_Restored;",
        }),
        "DROP TRIGGER IF EXISTS trg_TransactionLabels_Added;",
        "DROP TRIGGER IF EXISTS trg_TransactionLabels_Removed;",
    ];

    /// <summary>
    /// A SQL expression producing a JSON array of the fields that actually differed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AU3: only fields that changed. Each column contributes a fragment or an empty string,
    /// the pieces are concatenated, and the leading comma is trimmed. Ugly, and the
    /// alternative inside a trigger is uglier.
    /// </para>
    /// <para>
    /// <c>IS NOT</c> rather than <c>&lt;&gt;</c>, because it is null-safe. With
    /// <c>&lt;&gt;</c>, a description going from NULL to text compares as NULL, which is not
    /// true, and the single most common edit would silently record nothing.
    /// </para>
    /// </remarks>
    /// <param name="table">The table whose columns to compare.</param>
    public static string ChangesExpression(string table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (!TrackedFields.TryGetValue(table, out var fields))
        {
            return "NULL";
        }

        var fragments = fields.Select(f =>
            $"""
             CASE WHEN OLD.{f} IS NOT NEW.{f}
                  THEN ',' || json_object('field', '{f}', 'from', OLD.{f}, 'to', NEW.{f})
                  ELSE '' END
             """);

        return "'[' || ltrim(" + string.Join(" || ", fragments) + ", ',') || ']'";
    }

    private static List<string> Build()
    {
        var statements = new List<string>();

        foreach (var table in AuditedTables)
        {
            // Action codes match the AuditAction enum: 0 Created, 1 Updated, 2 Deleted,
            // 3 Restored.
            var captureSource = string.Equals(table, "Transactions", StringComparison.Ordinal)
                ? "NEW.CaptureSource"
                : "NULL";

            var changes = ChangesExpression(table);

            statements.Add($"""
                CREATE TRIGGER IF NOT EXISTS trg_{table}_Created
                AFTER INSERT ON {table}
                BEGIN
                    INSERT INTO AuditEvents (Id, EntityType, EntityId, Action, Changes, CaptureSource, OccurredAt)
                    VALUES (lower(hex(randomblob(16))), '{table}', NEW.Id, 0, NULL, {captureSource},
                            CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER));
                END;
                """);

            // An ordinary edit: DeletedAt did not change state.
            //
            // The WHEN also requires at least one tracked field to differ, so a write that
            // touches only UpdatedAt does not produce an entry saying nothing happened.
            statements.Add($"""
                CREATE TRIGGER IF NOT EXISTS trg_{table}_Updated
                AFTER UPDATE ON {table}
                WHEN ((OLD.DeletedAt IS NULL AND NEW.DeletedAt IS NULL)
                   OR (OLD.DeletedAt IS NOT NULL AND NEW.DeletedAt IS NOT NULL))
                  AND {changes} <> '[]'
                BEGIN
                    INSERT INTO AuditEvents (Id, EntityType, EntityId, Action, Changes, CaptureSource, OccurredAt)
                    VALUES (lower(hex(randomblob(16))), '{table}', NEW.Id, 1, {changes}, {captureSource},
                            CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER));
                END;
                """);

            // A soft delete: DeletedAt went from null to a value.
            statements.Add($"""
                CREATE TRIGGER IF NOT EXISTS trg_{table}_Deleted
                AFTER UPDATE ON {table}
                WHEN OLD.DeletedAt IS NULL AND NEW.DeletedAt IS NOT NULL
                BEGIN
                    INSERT INTO AuditEvents (Id, EntityType, EntityId, Action, Changes, CaptureSource, OccurredAt)
                    VALUES (lower(hex(randomblob(16))), '{table}', NEW.Id, 2, NULL, {captureSource},
                            CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER));
                END;
                """);

            // A restore: DeletedAt went back to null.
            statements.Add($"""
                CREATE TRIGGER IF NOT EXISTS trg_{table}_Restored
                AFTER UPDATE ON {table}
                WHEN OLD.DeletedAt IS NOT NULL AND NEW.DeletedAt IS NULL
                BEGIN
                    INSERT INTO AuditEvents (Id, EntityType, EntityId, Action, Changes, CaptureSource, OccurredAt)
                    VALUES (lower(hex(randomblob(16))), '{table}', NEW.Id, 3, NULL, {captureSource},
                            CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER));
                END;
                """);
        }

        statements.AddRange(LabelTriggers());
        return statements;
    }

    /// <summary>
    /// Records labels going on and coming off a transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A join table cannot be handled by the generic template above: it has no
    /// <c>Id</c>, no <c>DeletedAt</c>, and its edits are inserts and deletes rather than
    /// updates. It still needs a trail, because attaching a label is an edit to a
    /// transaction and FR-8.2 does not exempt it.
    /// </para>
    /// <para>
    /// The audit row is written against the <b>transaction</b>, not the join row, so it lands
    /// in the timeline the user is actually looking at. Action is <c>Updated</c>, because
    /// that is what happened from their point of view: the transaction changed.
    /// </para>
    /// <para>
    /// The label's name is resolved at trigger time rather than stored as an id, so the trail
    /// still reads correctly after that label is renamed or deleted. A trail that says
    /// "added 0f3c..." six months later explains nothing.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> LabelTriggers()
    {
        const string occurredAt = "CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER)";

        yield return $"""
            CREATE TRIGGER IF NOT EXISTS trg_TransactionLabels_Added
            AFTER INSERT ON TransactionLabels
            BEGIN
                INSERT INTO AuditEvents (Id, EntityType, EntityId, Action, Changes, CaptureSource, OccurredAt)
                VALUES (lower(hex(randomblob(16))), 'Transactions', NEW.TransactionId, 1,
                        '[' || json_object(
                            'field', 'Labels',
                            'from', 'nothing',
                            'to', (SELECT Name FROM Labels WHERE Id = NEW.LabelId)) || ']',
                        NULL, {occurredAt});
            END;
            """;

        yield return $"""
            CREATE TRIGGER IF NOT EXISTS trg_TransactionLabels_Removed
            AFTER DELETE ON TransactionLabels
            BEGIN
                INSERT INTO AuditEvents (Id, EntityType, EntityId, Action, Changes, CaptureSource, OccurredAt)
                VALUES (lower(hex(randomblob(16))), 'Transactions', OLD.TransactionId, 1,
                        '[' || json_object(
                            'field', 'Labels',
                            'from', (SELECT Name FROM Labels WHERE Id = OLD.LabelId),
                            'to', 'nothing') || ']',
                        NULL, {occurredAt});
            END;
            """;
    }
}
