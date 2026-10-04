namespace EleFi.Domain.Alerts;

/// <summary>
/// The text of an incoming SMS, for the moment it takes to match a rule against it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a <c>ref struct</c> so that SM1 is a compiler guarantee rather than a
/// convention.</b> A <c>ref struct</c> cannot be assigned to a field, boxed, captured by a
/// lambda, stored in a collection, serialised, or held across an <c>await</c>. There is
/// therefore no expressible code path that writes an SMS body to disk, to a log, to the
/// audit trail, to a backup, or to an export.
/// </para>
/// <para>
/// SM1 is the strictest rule in the project and it has no exceptions. One exception would
/// make it unauditable, and an SMS body is the most sensitive text on the device: it
/// includes one-time passcodes.
/// </para>
/// <para>
/// If you find yourself wanting to keep one of these, you want a
/// <see cref="CaptureSuggestion"/> instead. That is what the parser produces, and it holds
/// only the named captures a rule extracted.
/// </para>
/// </remarks>
public readonly ref struct SmsBody
{
    private readonly ReadOnlySpan<char> _text;

    /// <summary>Wraps message text for the duration of one parse.</summary>
    /// <param name="text">The message body.</param>
    public SmsBody(ReadOnlySpan<char> text) => _text = text;

    /// <summary>The message text, as a span that cannot outlive this frame.</summary>
    public ReadOnlySpan<char> Text => _text;

    /// <summary>Number of characters in the message.</summary>
    public int Length => _text.Length;

    /// <summary>True when there is nothing to parse.</summary>
    public bool IsEmpty => _text.IsEmpty;

    /// <summary>
    /// Never returns the message. Overridden so an accidental interpolation or log call
    /// cannot leak the body through <c>ToString</c>.
    /// </summary>
    public override string ToString() => $"<sms body, {_text.Length} chars, never rendered>";
}
