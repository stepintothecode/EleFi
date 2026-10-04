namespace EleFi.Ui.Services;

/// <summary>How a toast reads.</summary>
public enum ToastKind
{
    /// <summary>Something worked.</summary>
    Ok = 0,

    /// <summary>Something did not work, and the message says what to do about it.</summary>
    Error = 1,

    /// <summary>Neither: a plain statement of fact.</summary>
    Info = 2,
}

/// <summary>One message on screen.</summary>
/// <param name="Id">Identity, so a dismissal removes the right one.</param>
/// <param name="Kind">How it reads.</param>
/// <param name="Message">What it says.</param>
public sealed record Toast(Guid Id, ToastKind Kind, string Message);

/// <summary>
/// Short messages that appear near the user's thumb rather than at the top of the page.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of a real failure: confirmations and errors rendered at the top of
/// the page, so pressing Save at the bottom of a long form appeared to do nothing. The user
/// pressed it again. A message nobody sees is worse than no message, because it removes the
/// feedback the second press was looking for.
/// </para>
/// <para>
/// Errors do not auto-dismiss. A confirmation can slide away unread and cost nothing; a
/// message telling you what went wrong is the only thing standing between the user and
/// repeating the mistake.
/// </para>
/// </remarks>
public sealed class ToastService
{
    private readonly List<Toast> _toasts = [];

    /// <summary>Raised when the visible set changes.</summary>
    public event Action? Changed;

    /// <summary>What is currently on screen, oldest first.</summary>
    public IReadOnlyList<Toast> Current => _toasts;

    /// <summary>How long a success message stays before dismissing itself.</summary>
    public static TimeSpan SuccessLifetime { get; } = TimeSpan.FromSeconds(3);

    /// <summary>Shows a success message, which dismisses itself.</summary>
    /// <param name="message">What to say.</param>
    public void Ok(string message) => Show(ToastKind.Ok, message);

    /// <summary>Shows an error, which stays until dismissed.</summary>
    /// <param name="message">What went wrong, and what to do about it.</param>
    public void Error(string message) => Show(ToastKind.Error, message);

    /// <summary>Shows a neutral message, which dismisses itself.</summary>
    /// <param name="message">What to say.</param>
    public void Info(string message) => Show(ToastKind.Info, message);

    /// <summary>Removes a message.</summary>
    /// <param name="id">Which one.</param>
    public void Dismiss(Guid id)
    {
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Clears everything, for a screen change that makes the messages stale.</summary>
    public void Clear()
    {
        if (_toasts.Count == 0)
        {
            return;
        }

        _toasts.Clear();
        Changed?.Invoke();
    }

    private void Show(ToastKind kind, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var toast = new Toast(Guid.CreateVersion7(), kind, message.Trim());

        // Three at once is already more than anyone reads. Dropping the oldest keeps the
        // most recent thing that happened visible, which is the one being reacted to.
        _toasts.Add(toast);
        while (_toasts.Count > 3)
        {
            _toasts.RemoveAt(0);
        }

        Changed?.Invoke();

        if (kind != ToastKind.Error)
        {
            _ = DismissLaterAsync(toast.Id);
        }
    }

    private async Task DismissLaterAsync(Guid id)
    {
        await Task.Delay(SuccessLifetime).ConfigureAwait(false);
        Dismiss(id);
    }
}
