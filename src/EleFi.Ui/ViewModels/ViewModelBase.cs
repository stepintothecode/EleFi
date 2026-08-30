using CommunityToolkit.Mvvm.ComponentModel;

namespace EleFi.Ui.ViewModels;

/// <summary>
/// Base for every view model. Inherits change notification from
/// <see cref="ObservableObject"/> so no view model hand-writes
/// <c>INotifyPropertyChanged</c>.
/// </summary>
/// <remarks>
/// A view model orchestrates; it does not decide. Money arithmetic belongs in
/// <c>EleFi.Domain</c> and persistence behind a repository, so a view model that
/// calculates a balance or builds a query is in the wrong layer.
/// </remarks>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;

    /// <summary>
    /// True while a long-running operation is in flight, so a view can show a skeleton
    /// rather than a spinner (NFR-2.10).
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }
}
