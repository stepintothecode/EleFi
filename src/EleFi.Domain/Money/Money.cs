namespace EleFi.Domain.Money;

/// <summary>
/// An exact amount of money: an integer count of minor units plus its currency.
/// </summary>
/// <remarks>
/// <para>
/// <b>M1: no floating-point value ever holds, transports, or computes money.</b> There is
/// no conversion to <see cref="double"/> or <see cref="decimal"/> anywhere on this type,
/// and adding one would defeat the reason it exists.
/// </para>
/// <para>
/// <b>M4: amounts are non-negative.</b> Direction is expressed by which end of a
/// transaction a party sits on, never by a negative number, so there is exactly one way to
/// say "money left this container". Balances are the one place a signed value appears,
/// which is why they use <see cref="SignedMinor"/> rather than this invariant.
/// </para>
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    private Money(long minor, Currency currency)
    {
        Minor = minor;
        Currency = currency;
    }

    /// <summary>The amount, counted in minor units. 45050 means 450.50 in a 2-exponent currency.</summary>
    public long Minor { get; }

    /// <summary>The currency this amount is denominated in.</summary>
    public Currency Currency { get; }

    /// <summary>True when the amount is exactly zero.</summary>
    public bool IsZero => Minor == 0;

    /// <summary>Zero in the given currency.</summary>
    /// <param name="currency">The currency.</param>
    public static Money Zero(Currency currency) => new(0, currency);

    /// <summary>
    /// A non-negative amount, as required by M4.
    /// </summary>
    /// <param name="minor">Minor units, zero or more.</param>
    /// <param name="currency">The currency.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public static Money FromMinor(long minor, Currency currency)
    {
        if (minor < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minor),
                minor,
                "Money is never negative (M4). Direction comes from the transaction's parties.");
        }

        return new Money(minor, currency);
    }

    /// <summary>
    /// A signed amount, for balances only.
    /// </summary>
    /// <remarks>
    /// A Liability Container's balance and an overdrawn account both need a sign. Nothing
    /// a user types ever reaches this: capture goes through <see cref="FromMinor"/>.
    /// </remarks>
    /// <param name="minor">Minor units, positive or negative.</param>
    /// <param name="currency">The currency.</param>
    public static Money SignedMinor(long minor, Currency currency) => new(minor, currency);

    /// <summary>Adds two amounts of the same currency.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static Money operator +(Money left, Money right)
    {
        var currency = Combined(left, right);
        return new Money(checked(left.Minor + right.Minor), currency);
    }

    /// <summary>Subtracts two amounts of the same currency. The result may be negative.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static Money operator -(Money left, Money right)
    {
        var currency = Combined(left, right);
        return new Money(checked(left.Minor - right.Minor), currency);
    }

    /// <summary>Negates an amount.</summary>
    /// <param name="value">The amount.</param>
    public static Money operator -(Money value) => new(checked(-value.Minor), value.Currency);

    /// <summary>Multiplies by a whole number, for counts rather than rates.</summary>
    /// <param name="left">The amount.</param>
    /// <param name="right">A whole multiplier.</param>
    public static Money operator *(Money left, long right) => new(checked(left.Minor * right), left.Currency);

    /// <summary>Adds two amounts. Same as <c>operator +</c>, for languages without operators.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static Money Add(Money left, Money right) => left + right;

    /// <summary>Subtracts two amounts. Same as <c>operator -</c>.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static Money Subtract(Money left, Money right) => left - right;

    /// <summary>Negates an amount. Same as unary <c>operator -</c>.</summary>
    /// <param name="value">The amount.</param>
    public static Money Negate(Money value) => -value;

    /// <summary>Multiplies by a whole number. Same as <c>operator *</c>.</summary>
    /// <param name="left">The amount.</param>
    /// <param name="right">A whole multiplier.</param>
    public static Money Multiply(Money left, long right) => left * right;

    /// <summary>True when the left amount is smaller.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <summary>True when the left amount is larger.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <summary>True when the left amount is smaller or equal.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>True when the left amount is larger or equal.</summary>
    /// <param name="left">First amount.</param>
    /// <param name="right">Second amount.</param>
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>Orders two amounts of the same currency.</summary>
    /// <param name="other">The amount to compare with.</param>
    public int CompareTo(Money other)
    {
        Combined(this, other);
        return Minor.CompareTo(other.Minor);
    }

    /// <summary>The absolute value.</summary>
    public Money Abs() => new(System.Math.Abs(Minor), Currency);

    /// <summary>The invariant round-trip form, such as <c>450.50 INR</c>.</summary>
    public override string ToString() => MoneyText.ToInvariantString(this);

    /// <summary>
    /// The currency two amounts share, refusing the pair when they genuinely differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An <em>unspecified</em> currency adopts the other side rather than throwing. That is
    /// not leniency: <c>default(Money)</c> is zero, zero is zero in every currency, and it
    /// is the additive identity. Making it throw meant every accumulator had to be seeded
    /// with the right currency before use, and any screen that formatted a not-yet-loaded
    /// total crashed.
    /// </para>
    /// <para>
    /// Two <em>different</em> real currencies still throw. Rupees and dollars are not the
    /// same kind of thing, and silently adding them is the bug this whole type exists to
    /// prevent.
    /// </para>
    /// </remarks>
    private static Currency Combined(Money left, Money right)
    {
        if (left.Currency.IsUnspecified)
        {
            return right.Currency;
        }

        if (right.Currency.IsUnspecified)
        {
            return left.Currency;
        }

        if (!string.Equals(left.Currency.Code, right.Currency.Code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine {left.Currency.Code} with {right.Currency.Code}. "
                + "Convert through an FX rate first; the two are not the same kind of thing.");
        }

        return left.Currency;
    }
}
