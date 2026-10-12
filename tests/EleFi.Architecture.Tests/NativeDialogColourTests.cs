using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EleFi.Architecture.Tests.Support;

namespace EleFi.Architecture.Tests;

/// <summary>
/// The buttons on Android's own dialogs (the date and time pickers' Set, Clear and Cancel)
/// stay readable in both themes.
/// </summary>
/// <remarks>
/// <para>
/// Twice they were not. First the template's indigo vanished on the dark dialog. Then the fix
/// set colorAccent, but Material draws those buttons in colorPrimary, which this app uses for
/// the window's ground: white buttons on a white dialog, near-black on a dark one.
/// </para>
/// <para>
/// No device is needed to catch either again. The colours are data in the resource files, so
/// this reads them and holds the dialog colour to AA contrast on the dialog's surface.
/// </para>
/// </remarks>
public class NativeDialogColourTests
{
    // Material's dialog surfaces: white by day, the elevated grey of a dark dialog by night.
    private const string LightSurface = "#FFFFFF";
    private const string DarkSurface = "#383838";

    [Theory]
    [InlineData("values", LightSurface)]
    [InlineData("values-night", DarkSurface)]
    public void The_dialog_colour_reads_on_the_dialog_at_AA_contrast(string folder, string surface)
    {
        var accent = Colour(folder, "dialogAccent");

        Assert.True(
            Contrast(accent, surface) >= 4.5,
            $"{folder}: dialogAccent {accent} on {surface} is {Contrast(accent, surface):0.00}:1, under 4.5:1.");
    }

    [Theory]
    [InlineData("values")]
    [InlineData("values-night")]
    public void Dialogs_are_never_drawn_in_the_window_ground_colour(string folder)
    {
        Assert.NotEqual(Colour(folder, "colorPrimary"), Colour(folder, "dialogAccent"), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_dialog_style_sets_primary_to_the_dialog_colour_and_the_activity_applies_it()
    {
        var styles = XDocument.Load(Path.Combine(Resources(), "values", "styles.xml"));
        var dialogs = styles.Descendants("style").Single(s => (string?)s.Attribute("name") == "EleFi.NativeDialogs");

        Assert.Equal(
            "@color/dialogAccent",
            dialogs.Elements("item").Single(i => (string?)i.Attribute("name") == "colorPrimary").Value.Trim());

        var activity = File.ReadAllText(Path.Combine(RepositoryRoot.Src().FullName, "EleFi.App", "Platforms", "Android", "MainActivity.cs"));
        Assert.Contains("ApplyStyle(Resource.Style.EleFi_NativeDialogs", activity, StringComparison.Ordinal);
    }

    private static string Resources() =>
        Path.Combine(RepositoryRoot.Src().FullName, "EleFi.App", "Platforms", "Android", "Resources");

    private static string Colour(string folder, string name) =>
        XDocument.Load(Path.Combine(Resources(), folder, "colors.xml"))
            .Descendants("color")
            .Single(c => (string?)c.Attribute("name") == name)
            .Value.Trim();

    // WCAG 2 relative luminance and contrast ratio.
    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        var rgb = Enumerable.Range(0, 3)
            .Select(i => int.Parse(hex.AsSpan(1 + (i * 2), 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0)
            .Select(c => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4))
            .ToArray();

        return (0.2126 * rgb[0]) + (0.7152 * rgb[1]) + (0.0722 * rgb[2]);
    }
}
