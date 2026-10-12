using System.Text.RegularExpressions;
using EleFi.Domain.Primitives;

namespace EleFi.Domain.Parties;

/// <summary>What kind of detail a contact detail is.</summary>
public enum ContactDetailKind
{
    /// <summary>A phone number.</summary>
    Phone = 0,

    /// <summary>An email address.</summary>
    Email = 1,

    /// <summary>A postal address, possibly over several lines.</summary>
    Address = 2,

    /// <summary>Anything else worth keeping: a UPI id, an account to pay into.</summary>
    Other = 3,
}

/// <summary>
/// One way of reaching a contact, or one thing worth remembering about them: a phone
/// number, an email address, an address.
/// </summary>
/// <remarks>
/// <para>
/// A contact can carry any number, of any kind, each with an optional label ("Work",
/// "Mobile"). Checked before it is kept (CT2), so a list of phone numbers contains phone
/// numbers.
/// </para>
/// <para>
/// Never money, and never part of a balance: a contact's details describe who they are, not
/// what moved.
/// </para>
/// </remarks>
public class ContactDetail : Entity
{
    /// <summary>The longest value kept: enough for an address, short of a document.</summary>
    public const int MaxValueLength = 500;

    /// <summary>The longest label kept.</summary>
    public const int MaxLabelLength = 60;

    private static readonly Regex Spaces = new(@"[ \t]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    private static readonly Regex PhoneShape = new(
        @"^\+?[\d\s().-]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    private static readonly Regex EmailShape = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    /// <summary>The contact it belongs to: an external party.</summary>
    public Guid PartyId { get; set; }

    /// <summary>What kind of detail it is.</summary>
    public ContactDetailKind Kind { get; set; }

    /// <summary>A word saying which one it is, such as "Work". Optional.</summary>
    public string? Label { get; set; }

    /// <summary>The detail itself, tidied.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// What is wrong with a value for its kind, or null when it can be kept (CT2).
    /// </summary>
    /// <param name="kind">The kind of detail.</param>
    /// <param name="value">The value as typed.</param>
    public static string? Problem(ContactDetailKind kind, string? value)
    {
        var tidy = Tidy(kind, value);
        if (tidy.Length == 0)
        {
            return "Type something to keep.";
        }

        if (tidy.Length > MaxValueLength)
        {
            return $"That is longer than {MaxValueLength} characters.";
        }

        switch (kind)
        {
            case ContactDetailKind.Phone:
                var digits = tidy.Count(char.IsAsciiDigit);
                return PhoneShape.IsMatch(tidy) && digits is >= 6 and <= 15
                    ? null
                    : "That does not look like a phone number: digits, with spaces, + or - if you like.";

            case ContactDetailKind.Email:
                return EmailShape.IsMatch(tidy) ? null : "That does not look like an email address.";

            default:
                return null;
        }
    }

    /// <summary>A checked detail for a contact.</summary>
    /// <param name="partyId">The contact.</param>
    /// <param name="kind">The kind of detail.</param>
    /// <param name="value">The value as typed.</param>
    /// <param name="label">An optional label.</param>
    /// <param name="now">The creation instant.</param>
    /// <exception cref="ArgumentException">The value fails <see cref="Problem"/>.</exception>
    public static ContactDetail Create(Guid partyId, ContactDetailKind kind, string value, string? label, DateTimeOffset now)
    {
        if (Problem(kind, value) is { } problem)
        {
            throw new ArgumentException(problem, nameof(value));
        }

        var trimmedLabel = label?.Trim();

        return new ContactDetail
        {
            PartyId = partyId,
            Kind = kind,
            Value = Tidy(kind, value),
            Label = string.IsNullOrEmpty(trimmedLabel)
                ? null
                : trimmedLabel[..Math.Min(trimmedLabel.Length, MaxLabelLength)],
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    // Runs of spaces inside a line become one; an address keeps its line breaks.
    private static string Tidy(ContactDetailKind kind, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => Spaces.Replace(line, " ").Trim())
            .Where(line => line.Length > 0);

        return kind == ContactDetailKind.Address ? string.Join('\n', lines) : string.Join(' ', lines);
    }
}
