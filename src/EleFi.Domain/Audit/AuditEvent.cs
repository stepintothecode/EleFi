namespace EleFi.Domain.Audit;

/// <summary>What happened to a record.</summary>
public enum AuditAction
{
    /// <summary>The record was created.</summary>
    Created = 0,

    /// <summary>One or more fields changed.</summary>
    Updated = 1,

    /// <summary>The record was soft-deleted.</summary>
    Deleted = 2,

    /// <summary>A soft-deleted record was brought back.</summary>
    Restored = 3,
}

/// <summary>
/// An immutable record of something that happened to a record.
/// </summary>
/// <remarks>
/// <para>
/// Written by database triggers, not application code (AU2), so no code path can bypass
/// the trail: not a bulk import, not a restore, not a background service writing outside
/// the normal scope. An application-layer audit is only as complete as the discipline of
/// whoever writes the next repository method, and a bypassed audit is worse than none
/// because it looks complete.
/// </para>
/// <para>
/// Append-only (AU1). Audit events are never updated or deleted, including when their
/// subject is deleted.
/// </para>
/// </remarks>
public class AuditEvent
{
    /// <summary>The primary key.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Which table the subject lives in.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>The subject's identifier.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What happened.</summary>
    public AuditAction Action { get; set; }

    /// <summary>A JSON array of the fields that actually differed (AU3).</summary>
    public string? Changes { get; set; }

    /// <summary>How the change was made, when the row carried a capture source.</summary>
    public string? CaptureSource { get; set; }

    /// <summary>When it happened, UTC.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// The changes, parsed out of <see cref="Changes"/>.
    /// </summary>
    /// <remarks>
    /// Returns nothing rather than throwing when the JSON is unreadable. An audit entry
    /// that cannot be rendered should degrade to "something changed" rather than breaking
    /// the timeline, because the timeline is what a user opens when they already distrust a
    /// number.
    /// </remarks>
    public IReadOnlyList<FieldChange> ParsedChanges()
    {
        if (string.IsNullOrWhiteSpace(Changes))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(Changes, AuditJson.Default.ListFieldChange) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}

/// <summary>One field that differed, and what it went from and to.</summary>
/// <param name="Field">The column name.</param>
/// <param name="From">The value before, as JSON text. Null when it was null.</param>
/// <param name="To">The value after, as JSON text. Null when it is null.</param>
public sealed record FieldChange(
    [property: System.Text.Json.Serialization.JsonPropertyName("field")] string Field,
    [property: System.Text.Json.Serialization.JsonPropertyName("from")] System.Text.Json.JsonElement? From,
    [property: System.Text.Json.Serialization.JsonPropertyName("to")] System.Text.Json.JsonElement? To)
{
    /// <summary>The value before, as display text.</summary>
    public string FromText => Render(From);

    /// <summary>The value after, as display text.</summary>
    public string ToText => Render(To);

    private static string Render(System.Text.Json.JsonElement? value)
    {
        if (value is not { } element || element.ValueKind is System.Text.Json.JsonValueKind.Null)
        {
            return "nothing";
        }

        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString() ?? "nothing",
            _ => element.ToString(),
        };
    }
}

/// <summary>
/// Compile-time JSON for the audit trail's change lists.
/// </summary>
/// <remarks>
/// Generated rather than reflected: a Release build trims unused code and switches off
/// reflection-based JSON, and the timeline on every transaction reads these.
/// </remarks>
[System.Text.Json.Serialization.JsonSerializable(typeof(List<FieldChange>))]
internal sealed partial class AuditJson : System.Text.Json.Serialization.JsonSerializerContext;
