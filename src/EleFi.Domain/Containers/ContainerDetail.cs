namespace EleFi.Domain.Containers;

/// <summary>
/// The small line under a container's name: its bank and the last four digits.
/// </summary>
/// <remarks>
/// Names are whatever the user typed, and two cards called "HDFC" and "HDFC Regalia" are
/// easy to confuse. The issuer and the last four digits are what is printed on the card
/// itself, so they settle it at a glance. Only the last four are ever stored (C1), so only
/// the last four are ever shown.
/// </remarks>
public static class ContainerDetail
{
    /// <summary>"HDFC Bank ••4417", "HDFC Bank", "••4417", or null when there is neither.</summary>
    /// <param name="institutionName">The bank or issuer.</param>
    /// <param name="last4">The last four digits.</param>
    public static string? Describe(string? institutionName, string? last4)
    {
        var bank = string.IsNullOrWhiteSpace(institutionName) ? null : institutionName.Trim();
        var digits = string.IsNullOrWhiteSpace(last4) ? null : $"••{last4.Trim()}";

        return (bank, digits) switch
        {
            (null, null) => null,
            (not null, null) => bank,
            (null, not null) => digits,
            _ => $"{bank} {digits}",
        };
    }

    /// <summary>The detail line for a container.</summary>
    /// <param name="container">The container.</param>
    public static string? Describe(this Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
        return Describe(container.InstitutionName, container.AccountNumberLast4);
    }
}
