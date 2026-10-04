namespace EleFi.Domain.Primitives;

/// <summary>
/// The fields every stored record carries, from section 1 of the domain model.
/// </summary>
/// <remarks>
/// <para>
/// The identifier is a client-generated UUIDv7, so creating a record offline needs no
/// server and retrying a create is idempotent. Being time-ordered, it indexes like an
/// integer instead of scattering B-tree writes the way UUIDv4 does.
/// </para>
/// <para>
/// <see cref="DeletedAt"/> is the soft-delete marker. Every query excludes non-null values
/// in one place, never per query, because a single forgotten filter leaks deleted rows
/// into a balance.
/// </para>
/// </remarks>
public abstract class Entity
{
    /// <summary>The client-generated UUIDv7 primary key.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>When the record was created, UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the record last changed, UTC. The merge key for a future sync.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the record was soft-deleted, or null while it is live.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>True when the record has been soft-deleted.</summary>
    public bool IsDeleted => DeletedAt is not null;
}
