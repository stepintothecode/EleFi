namespace EleFi.App;

/// <summary>
/// The MAUI application root.
/// </summary>
/// <remarks>
/// The base type is written out in full deliberately. This project references
/// <c>EleFi.Application</c>, so the bare name <c>Application</c> binds to that namespace
/// rather than to the MAUI type and the file stops compiling. Qualifying it here is the
/// smallest fix; renaming the layer would be the larger one.
/// </remarks>
public partial class App : Microsoft.Maui.Controls.Application
{
    /// <summary>Creates the application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "EleFi" };
    }
}
