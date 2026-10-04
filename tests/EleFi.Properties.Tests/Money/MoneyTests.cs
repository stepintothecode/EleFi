using CsCheck;
using EleFi.Domain.Money;

namespace EleFi.Properties.Tests.Money;

/// <summary>
/// Properties that must hold for every input, not just the ones someone thought to write
/// an example for.
/// </summary>
/// <remarks>
/// These are the tests most likely to catch the bug that matters. An example test proves a
/// case; a property test tries to break the rule.
/// </remarks>
public class MoneyTests
{
    // Up to about 90 lakh crore in paise, comfortably past anything a person holds and
    // well inside long.
    private static readonly Gen<long> Minor = Gen.Long[0, 9_000_000_000_000L];

    [Fact]
    public void Parse_of_format_round_trips_for_every_amount()
    {
        Minor.Sample(minor =>
        {
            var original = EleFi.Domain.Money.Money.FromMinor(minor, Currency.Inr);
            var text = MoneyText.ToBareDecimal(original);

            return MoneyText.TryParse(text, Currency.Inr, out var parsed) && parsed == original;
        });
    }

    [Fact]
    public void Round_tripping_holds_for_currencies_with_other_exponents()
    {
        var currencies = Gen.OneOfConst(Currency.Inr, Currency.Of("USD"), Currency.Of("JPY"), Currency.Of("KWD"));

        Gen.Select(Minor, currencies).Sample(t =>
        {
            var (minor, currency) = t;
            var original = EleFi.Domain.Money.Money.FromMinor(minor, currency);
            var text = MoneyText.ToBareDecimal(original);

            // JPY has no minor units and KWD has three. Assuming two would break both.
            return MoneyText.TryParse(text, currency, out var parsed) && parsed == original;
        });
    }

    [Fact]
    public void Addition_and_subtraction_are_exact_and_reversible()
    {
        Gen.Select(Minor, Minor).Sample(t =>
        {
            var (a, b) = t;
            var left = EleFi.Domain.Money.Money.FromMinor(a, Currency.Inr);
            var right = EleFi.Domain.Money.Money.FromMinor(b, Currency.Inr);

            // Integer minor units, so this is exact. The same property over doubles fails
            // within a few operations, which is the entire reason for M1.
            return left + right - right == left;
        });
    }

    [Fact]
    public void Display_formatting_never_loses_or_invents_a_digit()
    {
        Minor.Sample(minor =>
        {
            var money = EleFi.Domain.Money.Money.FromMinor(minor, Currency.Inr);
            var display = MoneyText.ToDisplayString(money);

            // Strip the symbol and grouping and the digits must be the bare decimal's.
            var digits = new string(display.Where(c => char.IsAsciiDigit(c) || c == '.').ToArray());

            return digits == MoneyText.ToBareDecimal(money);
        });
    }

    [Fact]
    public void M4_a_negative_amount_cannot_be_constructed_through_the_capture_path()
    {
        Gen.Long[-9_000_000_000L, -1].Sample(minor =>
        {
            try
            {
                EleFi.Domain.Money.Money.FromMinor(minor, Currency.Inr);
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Direction comes from which end of a transaction a party sits on, never
                // from a sign, so there is exactly one way to say "money left here".
                return true;
            }
        });
    }
}
