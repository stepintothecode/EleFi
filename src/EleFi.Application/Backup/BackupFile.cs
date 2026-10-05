using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EleFi.Application.Backup;

/// <summary>
/// Everything EleFi holds, as one JSON document.
/// </summary>
/// <remarks>
/// <para>
/// A local, unencrypted, human-readable file that the user moves themselves. It is the
/// stopgap until encrypted Drive backup exists, and it is deliberately the simplest thing
/// that makes the data recoverable: plain JSON any tool can read, no format of our own.
/// </para>
/// <para>
/// <b>This file is not encrypted.</b> It is a complete financial history in readable text,
/// so where it is put matters. The UI says so at the point of export rather than burying it.
/// </para>
/// <para>
/// <see cref="SchemaVersion"/> exists so a future format change can refuse an incompatible
/// file rather than importing half of it.
/// </para>
/// <para>
/// <b>Format 2 holds everything</b>: every table, every row (deleted ones too, so Restore
/// in Settings still has them), every column, as stored, under <see cref="Tables"/>. It is
/// written from the database's own model rather than from a list of fields kept by hand, so
/// a table or column added later is in the backup without anyone remembering to add it. Enum
/// values are written by name and instants as ISO-8601 text, so it stays readable. Format 1
/// files, which listed a few fields per entity, still restore through the lists below.
/// </para>
/// </remarks>
public sealed class BackupFile
{
    /// <summary>The format this file was written in.</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>Format version, checked on import.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>When the file was written, UTC.</summary>
    [JsonPropertyName("exportedAt")]
    public DateTimeOffset ExportedAt { get; set; }

    /// <summary>The app version that wrote it, for diagnosing an odd file later.</summary>
    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>The last database migration the writing app had, so a newer file is refused.</summary>
    [JsonPropertyName("databaseVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DatabaseVersion { get; set; }

    /// <summary>Format 2: every table by name, each row a column-to-value object.</summary>
    [JsonPropertyName("tables")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<JsonObject>>? Tables { get; set; }

    /// <summary>Format 1 only: Money Containers.</summary>
    [JsonPropertyName("containers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BackupContainer>? Containers { get; set; }

    /// <summary>Format 1 only: Labels.</summary>
    [JsonPropertyName("labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BackupLabel>? Labels { get; set; }

    /// <summary>Format 1 only: Apps, in both roles.</summary>
    [JsonPropertyName("apps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BackupApp>? Apps { get; set; }

    /// <summary>Format 1 only: external parties. Container parties are rebuilt from the containers.</summary>
    [JsonPropertyName("parties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BackupParty>? Parties { get; set; }

    /// <summary>Format 1 only: Transactions.</summary>
    [JsonPropertyName("transactions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BackupTransaction>? Transactions { get; set; }

    /// <summary>
    /// How many live (not deleted) rows a table holds, in either format, for telling the user
    /// what a file contains before they restore it.
    /// </summary>
    /// <param name="table">The table: "Transactions", "Containers", "Labels", "Plans", "Goals", "ParseRules".</param>
    public int LiveCount(string table)
    {
        if (Tables is not null)
        {
            return Tables.TryGetValue(table, out var rows)
                ? rows.Count(r => r["DeletedAt"] is null && !IsBuiltIn(r))
                : 0;
        }

        return table switch
        {
            "Transactions" => Transactions?.Count ?? 0,
            "Containers" => Containers?.Count ?? 0,
            "Labels" => Labels?.Count ?? 0,
            _ => 0,
        };
    }

    // Built-in Parse Rules ship with the app, so the count of rules is the ones taught.
    private static bool IsBuiltIn(JsonObject row) =>
        row["IsBuiltIn"] is JsonValue value && value.TryGetValue<bool>(out var builtIn) && builtIn;
}

/// <summary>A container, as stored in a backup.</summary>
public sealed class BackupContainer
{
    /// <summary>Identifier, preserved so transactions still point at it.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The identifier of the Party row representing this container.
    /// </summary>
    /// <remarks>
    /// Stored explicitly. Transactions reference parties, not containers, so a restore that
    /// generated a fresh party id would leave every transaction pointing at nothing.
    /// </remarks>
    public Guid PartyId { get; set; }

    /// <summary>The name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The kind, by name rather than number, so a reordered enum cannot silently reinterpret it.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>ISO-4217 code.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Opening balance in minor units, signed.</summary>
    public long OpeningBalanceMinor { get; set; }

    /// <summary>The date tracking started.</summary>
    public string OpeningBalanceAsOf { get; set; } = string.Empty;

    /// <summary>Whether it is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>The bank or issuer.</summary>
    public string? InstitutionName { get; set; }

    /// <summary>Last four digits only.</summary>
    public string? AccountNumberLast4 { get; set; }

    /// <summary>A hex colour.</summary>
    public string? Colour { get; set; }
}

/// <summary>A label, as stored in a backup.</summary>
public sealed class BackupLabel
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A hex colour.</summary>
    public string? Colour { get; set; }

    /// <summary>Ordering in the picker.</summary>
    public int SortOrder { get; set; }
}

/// <summary>An app, as stored in a backup.</summary>
public sealed class BackupApp
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>An external party, as stored in a backup.</summary>
public sealed class BackupParty
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>A transaction, as stored in a backup.</summary>
public sealed class BackupTransaction
{
    /// <summary>Identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The party money left.</summary>
    public Guid SourcePartyId { get; set; }

    /// <summary>The party money reached.</summary>
    public Guid DestinationPartyId { get; set; }

    /// <summary>What left, in minor units.</summary>
    public long SourceAmountMinor { get; set; }

    /// <summary>The source currency.</summary>
    public string SourceCurrencyCode { get; set; } = string.Empty;

    /// <summary>What arrived, in minor units.</summary>
    public long DestinationAmountMinor { get; set; }

    /// <summary>The destination currency.</summary>
    public string DestinationCurrencyCode { get; set; } = string.Empty;

    /// <summary>The calendar day, ISO-8601.</summary>
    public string OccurredOn { get; set; } = string.Empty;

    /// <summary>The time of day, or null.</summary>
    public string? OccurredAtTime { get; set; }

    /// <summary>Free text.</summary>
    public string? Description { get; set; }

    /// <summary>The labels on it. Any number.</summary>
    public List<Guid> LabelIds { get; set; } = [];

    /// <summary>The platform bought through.</summary>
    public Guid? MarketplaceAppId { get; set; }

    /// <summary>The rail money moved along.</summary>
    public Guid? PaymentAppId { get; set; }

    /// <summary>Whether it still needs checking.</summary>
    public bool NeedsReview { get; set; }

    /// <summary>How it was recorded, by name.</summary>
    public string CaptureSource { get; set; } = string.Empty;
}
