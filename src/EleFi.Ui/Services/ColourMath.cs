using System.Globalization;

namespace EleFi.Ui.Services;

/// <summary>A colour as hue, saturation and lightness.</summary>
/// <param name="Hue">Degrees, 0 to 359.</param>
/// <param name="Saturation">Percent, 0 to 100.</param>
/// <param name="Lightness">Percent, 0 to 100.</param>
public readonly record struct Hsl(int Hue, int Saturation, int Lightness);

/// <summary>
/// Converts between the hex a label stores and the hue, saturation and lightness the colour
/// picker's sliders move.
/// </summary>
public static class ColourMath
{
    /// <summary>A colour as #rrggbb.</summary>
    /// <param name="colour">The colour.</param>
    public static string ToHex(Hsl colour)
    {
        var h = ((colour.Hue % 360) + 360) % 360 / 360.0;
        var s = Math.Clamp(colour.Saturation, 0, 100) / 100.0;
        var l = Math.Clamp(colour.Lightness, 0, 100) / 100.0;

        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
            var p = (2 * l) - q;
            r = Channel(p, q, h + (1 / 3.0));
            g = Channel(p, q, h);
            b = Channel(p, q, h - (1 / 3.0));
        }

        return string.Create(CultureInfo.InvariantCulture, $"#{Byte(r):x2}{Byte(g):x2}{Byte(b):x2}");
    }

    /// <summary>Reads #rgb or #rrggbb, with or without the #, into hue, saturation and lightness.</summary>
    /// <param name="hex">The text.</param>
    /// <param name="colour">The colour, when it could be read.</param>
    public static bool TryParse(string? hex, out Hsl colour)
    {
        colour = default;
        if (Normalise(hex) is not { } normal)
        {
            return false;
        }

        var r = int.Parse(normal.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(normal.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(normal.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        double h = 0, s = 0;

        if (max != min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? ((g - b) / d) + (g < b ? 6 : 0)
                : max == g ? ((b - r) / d) + 2
                : ((r - g) / d) + 4;
            h /= 6;
        }

        colour = new Hsl((int)Math.Round(h * 360) % 360, (int)Math.Round(s * 100), (int)Math.Round(l * 100));
        return true;
    }

    /// <summary>The colour as lowercase #rrggbb, or null when the text is not a colour.</summary>
    /// <param name="hex">#rgb or #rrggbb, with or without the #.</param>
    public static string? Normalise(string? hex)
    {
        var text = (hex ?? string.Empty).Trim().TrimStart('#');
        if (text.Length == 3)
        {
            text = string.Concat(text.Select(c => new string(c, 2)));
        }

        return text.Length == 6 && text.All(Uri.IsHexDigit)
            ? "#" + text.ToLowerInvariant()
            : null;
    }

    private static double Channel(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        return t < 1 / 6.0 ? p + ((q - p) * 6 * t)
            : t < 1 / 2.0 ? q
            : t < 2 / 3.0 ? p + ((q - p) * ((2 / 3.0) - t) * 6)
            : p;
    }

    private static int Byte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);
}
