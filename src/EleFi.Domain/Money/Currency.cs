using System.Globalization;

namespace EleFi.Domain.Money;

/// <summary>
/// An ISO-4217 currency code and the number of decimal places it uses.
/// </summary>
/// <remarks>
/// The exponent is looked up per currency and never assumed to be 2 (M3). JPY has none,
/// and treating it as 2 would multiply every yen amount by a hundred.
/// </remarks>
public readonly record struct Currency
{
    private static readonly Dictionary<string, int> Exponents = new(StringComparer.Ordinal)
    {
        ["INR"] = 2,
        ["USD"] = 2,
        ["EUR"] = 2,
        ["GBP"] = 2,
        ["AED"] = 2,
        ["SGD"] = 2,
        ["AUD"] = 2,
        ["CAD"] = 2,
        ["CHF"] = 2,
        ["JPY"] = 0,
        ["KRW"] = 0,
        ["KWD"] = 3,
        ["BHD"] = 3,
        ["OMR"] = 3,
    };

    private readonly string? _code;

    private Currency(string code) => _code = code;

    /// <summary>
    /// The uppercase three-letter ISO-4217 code (M2), or empty when unspecified.
    /// </summary>
    /// <remarks>
    /// Never null. A struct can always be default-constructed, so every member here has to
    /// answer sensibly for <c>default(Currency)</c>. There is no constructor to defend, and
    /// a member that throws is a crash waiting for the first screen that renders before its
    /// data has loaded.
    /// </remarks>
    public string Code => _code ?? string.Empty;

    /// <summary>
    /// True for <c>default(Currency)</c>: no currency has been chosen yet.
    /// </summary>
    /// <remarks>
    /// Zero is zero in every currency, so an unspecified one is the additive identity
    /// rather than an error. See the operators on <see cref="Money"/>.
    /// </remarks>
    public bool IsUnspecified => _code is null;

    /// <summary>Indian rupee, the default home currency.</summary>
    public static Currency Inr { get; } = new("INR");

    /// <summary>
    /// Decimal places this currency uses. 2 for INR, 0 for JPY, 3 for KWD.
    /// </summary>
    /// <remarks>Two for anything unrecognised or unspecified, which is the common case.</remarks>
    public int Exponent =>
        _code is not null && Exponents.TryGetValue(_code, out var e) ? e : 2;

    /// <summary>Minor units in one major unit: 100 for INR, 1 for JPY.</summary>
    public long MinorUnitsPerMajor
    {
        get
        {
            long scale = 1;
            for (var i = 0; i < Exponent; i++)
            {
                scale *= 10;
            }

            return scale;
        }
    }

    /// <summary>
    /// Creates a currency from a code, normalising case and whitespace.
    /// </summary>
    /// <param name="code">A three-letter ISO-4217 code.</param>
    /// <exception cref="ArgumentException">The code is not three letters.</exception>
    public static Currency Of(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var normalised = code.Trim().ToUpperInvariant();

        if (normalised.Length != 3 || !normalised.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException($"'{code}' is not a three-letter ISO-4217 code.", nameof(code));
        }

        return new Currency(normalised);
    }

    /// <summary>Returns true when <paramref name="code"/> is a usable currency code.</summary>
    /// <param name="code">The candidate code.</param>
    /// <param name="currency">The parsed currency, when this returns true.</param>
    public static bool TryOf(string? code, out Currency currency)
    {
        currency = default;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalised = code.Trim().ToUpperInvariant();
        if (normalised.Length != 3 || !normalised.All(char.IsAsciiLetterUpper))
        {
            return false;
        }

        currency = new Currency(normalised);
        return true;
    }

    /// <summary>The currency code, or empty when unspecified.</summary>
    public override string ToString() => Code;
}
