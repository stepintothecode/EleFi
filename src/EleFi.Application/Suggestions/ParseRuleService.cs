using EleFi.Application.Abstractions;
using EleFi.Domain.Alerts;

namespace EleFi.Application.Suggestions;

/// <summary>
/// The rules the user taught: creating one from an example, and switching or removing it.
/// </summary>
/// <remarks>
/// Built-in rules are listed by <see cref="IParseRuleRepository"/> too, but are never
/// deleted here: they ship with the app and are re-seeded by name (FR-11.26). A user who
/// wants one gone switches it off.
/// </remarks>
public sealed class ParseRuleService(IParseRuleRepository rules, IClock clock)
{
    /// <summary>The rules the user taught, newest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<IReadOnlyList<ParseRule>> TaughtAsync(CancellationToken cancellationToken = default) =>
        (await rules.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(r => !r.IsBuiltIn)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

    /// <summary>Builds a rule from an annotated example and saves it, if it reads the example back.</summary>
    /// <param name="example">The message and the user's answers.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<TaughtRule> TeachAsync(TeachingExample example, CancellationToken cancellationToken = default)
    {
        var taught = RuleInducer.Induce(example, clock.UtcNow);

        if (taught.Rule is { } rule)
        {
            await rules.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        }

        return taught;
    }

    /// <summary>Switches a rule on or off.</summary>
    /// <param name="id">The rule.</param>
    /// <param name="enabled">Whether it should be used.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var rule = await rules.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return;
        }

        rule.IsEnabled = enabled;
        await rules.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes a taught rule. Built-in rules are refused.</summary>
    /// <param name="id">The rule.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when it was removed.</returns>
    public async Task<bool> ForgetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await rules.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (rule is null || rule.IsBuiltIn)
        {
            return false;
        }

        await rules.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
