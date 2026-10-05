using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EleFi.Infrastructure.Persistence;

/// <summary>A table as the backup sees it: its name and its columns.</summary>
/// <param name="Name">The table name.</param>
/// <param name="Columns">Every column, in model order.</param>
public sealed record BackupTable(string Name, IReadOnlyList<BackupColumn> Columns)
{
    /// <summary>The column with this name, or null when the table has none.</summary>
    /// <param name="name">The column name.</param>
    public BackupColumn? Column(string name) => Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
}

/// <summary>
/// One column, and how its stored value is written to and read from JSON.
/// </summary>
/// <remarks>
/// Most values travel exactly as stored. Three are translated so the file stays readable and
/// safe to keep: an enum by name (a number would be silently reinterpreted if the enum were
/// ever reordered), an instant as ISO-8601 text rather than epoch milliseconds, and a flag as
/// true or false rather than 1 or 0.
/// </remarks>
/// <param name="Name">The column name.</param>
/// <param name="ModelType">The property's type in the model, without nullability.</param>
public sealed record BackupColumn(string Name, Type ModelType)
{
    /// <summary>A stored value as JSON.</summary>
    /// <param name="stored">What the database returned, or null.</param>
    public JsonNode? ToJson(object? stored)
    {
        if (stored is null)
        {
            return null;
        }

        if (ModelType.IsEnum && stored is long number)
        {
            return Enum.GetName(ModelType, Enum.ToObject(ModelType, number)) is { } name
                ? JsonValue.Create(name)
                : JsonValue.Create(number);
        }

        if (ModelType == typeof(DateTimeOffset) && stored is long milliseconds)
        {
            return JsonValue.Create(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("O", CultureInfo.InvariantCulture));
        }

        if (ModelType == typeof(bool) && stored is long flag)
        {
            return JsonValue.Create(flag != 0);
        }

        return stored switch
        {
            long l => JsonValue.Create(l),
            double d => JsonValue.Create(d),
            string s => JsonValue.Create(s),
            byte[] bytes => JsonValue.Create(Convert.ToBase64String(bytes)),
            _ => JsonValue.Create(Convert.ToString(stored, CultureInfo.InvariantCulture)),
        };
    }

    /// <summary>A JSON value as the database stores it.</summary>
    /// <param name="node">The value from the file, or null.</param>
    public object? FromJson(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (ModelType.IsEnum && value.TryGetValue<string>(out var name))
        {
            return Convert.ToInt64(Enum.Parse(ModelType, name), CultureInfo.InvariantCulture);
        }

        if (ModelType == typeof(DateTimeOffset) && value.TryGetValue<string>(out var iso))
        {
            return DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUnixTimeMilliseconds();
        }

        if (ModelType == typeof(byte[]) && value.TryGetValue<string>(out var base64))
        {
            return Convert.FromBase64String(base64);
        }

        if (value.TryGetValue<string>(out var text))
        {
            return text;
        }

        if (value.TryGetValue<bool>(out var flag))
        {
            return flag ? 1L : 0L;
        }

        if (value.TryGetValue<long>(out var number))
        {
            return number;
        }

        return value.TryGetValue<double>(out var real) ? real : null;
    }
}

/// <summary>Every table the backup covers, read from the database's own model.</summary>
/// <remarks>
/// From the model, not a hand-kept list, so a table or column added later is backed up and
/// restored without anyone having to remember to add it here.
/// </remarks>
public static class BackupTables
{
    /// <summary>The tables of a model, by name.</summary>
    /// <param name="model">The EF Core model.</param>
    public static IReadOnlyList<BackupTable> Of(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.GetEntityTypes()
            .Where(e => e.GetTableName() is not null && !e.IsOwned())
            .GroupBy(e => e.GetTableName()!, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new BackupTable(
                g.Key,
                g.SelectMany(e => e.GetProperties())
                    .Select(p => new BackupColumn(p.GetColumnName(), TranslatedType(p)))
                    .DistinctBy(c => c.Name)
                    .ToList()))
            .ToList();
    }

    // A column already stored as text (a day, a time, an enum kept by name) travels as it is;
    // only values stored as numbers are translated.
    private static Type TranslatedType(IProperty property)
    {
        var provider = property.GetValueConverter()?.ProviderClrType ?? property.GetProviderClrType();
        if (provider is not null && (Nullable.GetUnderlyingType(provider) ?? provider) == typeof(string))
        {
            return typeof(string);
        }

        return Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
    }
}
