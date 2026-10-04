namespace EleFi.Domain.Alerts;

/// <summary>A Payment App whose notifications EleFi is willing to read.</summary>
/// <param name="Package">The Android package name. What Android reports as the poster.</param>
/// <param name="Name">The App name used in EleFi, so a confirmed payment says "Paid with GPay".</param>
public sealed record PaymentApp(string Package, string Name);

/// <summary>
/// The allow-list of Payment Apps whose notifications are read.
/// </summary>
/// <remarks>
/// <para>
/// The notification listener sees every notification on the device. This list is the gate
/// in front of all of them, checked against the package name <b>before</b> any title or
/// text is touched (ADR-0014). Anything not on it is dropped unread: chats, emails, OTPs,
/// everything.
/// </para>
/// <para>
/// Data, not configuration the user edits. Adding an app is a line here plus a rule in
/// <see cref="BuiltInParseRules"/> plus a corpus test, the same as adding a bank.
/// </para>
/// </remarks>
public static class PaymentApps
{
    /// <summary>Google Pay.</summary>
    public static readonly PaymentApp GPay = new("com.google.android.apps.nbu.paisa.user", "GPay");

    /// <summary>PhonePe.</summary>
    public static readonly PaymentApp PhonePe = new("com.phonepe.app", "PhonePe");

    /// <summary>Paytm.</summary>
    public static readonly PaymentApp Paytm = new("net.one97.paytm", "Paytm");

    /// <summary>Amazon Pay, which lives inside the Amazon shopping app.</summary>
    public static readonly PaymentApp AmazonPay = new("in.amazon.mShop.android.shopping", "Amazon Pay");

    /// <summary>CRED.</summary>
    public static readonly PaymentApp Cred = new("com.dreamplug.androidapp", "CRED");

    /// <summary>Every app whose notifications may be read.</summary>
    public static IReadOnlyList<PaymentApp> All { get; } = [GPay, PhonePe, Paytm, AmazonPay, Cred];

    /// <summary>The app with this package name, or null when it is not on the list.</summary>
    /// <param name="package">The package that posted a notification.</param>
    public static PaymentApp? Find(string? package) =>
        string.IsNullOrWhiteSpace(package)
            ? null
            : All.FirstOrDefault(a => string.Equals(a.Package, package.Trim(), StringComparison.Ordinal));

    /// <summary>True when notifications from this package may be read at all.</summary>
    /// <param name="package">The package that posted a notification.</param>
    public static bool IsAllowListed(string? package) => Find(package) is not null;
}
