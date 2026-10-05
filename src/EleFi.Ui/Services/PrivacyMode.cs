using EleFi.Application.Abstractions;

namespace EleFi.Ui.Services;

/// <summary>
/// Hides every amount in the app at once, for using it with someone looking over a shoulder.
/// </summary>
/// <remarks>
/// <para>
/// One switch, read by the layout, which puts a class on the whole shell. Every amount is
/// already marked as an amount for styling, so the stylesheet blurs them all and no page
/// has to know the switch exists. A page that forgot to check a flag would leak a balance;
/// a page cannot forget a style it never applied.
/// </para>
/// <para>
/// Remembered between launches in the settings store: someone who hides amounts on a bus
/// does not expect them back on screen the next time the app opens.
/// </para>
/// </remarks>
public sealed class PrivacyMode
{
    /// <summary>The settings key.</summary>
    public const string Key = "privacy.hide-amounts";

    private readonly ISettingsStore _store;

    /// <summary>Reads the remembered state.</summary>
    /// <param name="store">Where small per-device settings live.</param>
    public PrivacyMode(ISettingsStore store)
    {
        _store = store;
        AmountsHidden = string.Equals(store.Read(Key, "false"), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Raised whenever amounts are hidden or shown.</summary>
    public event Action? Changed;

    /// <summary>True while amounts are hidden.</summary>
    public bool AmountsHidden { get; private set; }

    /// <summary>The class the layout puts on the shell.</summary>
    public string CssClass => AmountsHidden ? "amounts-hidden" : string.Empty;

    /// <summary>Hides amounts if shown, shows them if hidden.</summary>
    public void Toggle() => Set(!AmountsHidden);

    /// <summary>Hides or shows amounts.</summary>
    /// <param name="hidden">True to hide.</param>
    public void Set(bool hidden)
    {
        if (hidden == AmountsHidden)
        {
            return;
        }

        AmountsHidden = hidden;
        _store.Write(Key, hidden ? "true" : "false");
        Changed?.Invoke();
    }
}
