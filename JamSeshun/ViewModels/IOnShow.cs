namespace JamSeshun.ViewModels;

/// <summary>
/// Implement on view models that need to load data and subscribe to live updates
/// for the duration that their view is visible. The view calls OnShow() each time
/// it attaches to the visual tree (first show and every navigate-back), and disposes
/// the returned IDisposable when it detaches (hidden or destroyed).
/// </summary>
public interface IOnShow
{
    /// <summary>
    /// Perform the initial load and set up any change subscriptions.
    /// The returned IDisposable tears down all subscriptions when disposed.
    /// </summary>
    IDisposable OnShow();
}
