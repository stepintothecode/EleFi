namespace EleFi.Domain.Alerts;

/// <summary>Where a Transaction Alert arrived from.</summary>
public enum AlertChannel
{
    /// <summary>An SMS from a bank, card issuer, or wallet. The Sender ID is the TRAI header.</summary>
    Sms = 0,

    /// <summary>
    /// A notification posted by a Payment App such as GPay or PhonePe. The sender is the
    /// app's package name, which Android supplies and an app cannot forge (ADR-0014).
    /// </summary>
    PaymentApp = 1,
}

/// <summary>Which channels have reported the same payment. A suggestion can hold both.</summary>
[Flags]
public enum AlertEvidence
{
    /// <summary>Nothing. Never stored; a suggestion always has at least one source.</summary>
    None = 0,

    /// <summary>A bank SMS described it.</summary>
    Sms = 1,

    /// <summary>A Payment App notification described it.</summary>
    PaymentApp = 2,
}

/// <summary>Conversions between a channel and the evidence it contributes.</summary>
public static class AlertChannelExtensions
{
    /// <summary>The evidence flag a channel contributes.</summary>
    /// <param name="channel">The channel.</param>
    public static AlertEvidence AsEvidence(this AlertChannel channel) => channel switch
    {
        AlertChannel.Sms => AlertEvidence.Sms,
        AlertChannel.PaymentApp => AlertEvidence.PaymentApp,
        _ => AlertEvidence.None,
    };
}
