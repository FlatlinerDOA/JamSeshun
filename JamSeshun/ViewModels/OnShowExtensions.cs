using Avalonia;
using Avalonia.VisualTree;
using System.Reactive.Disposables;

namespace JamSeshun.ViewModels;

public static class OnShowExtensions
{
    /// <summary>
    /// Disposes <paramref name="previous"/> and calls <see cref="IOnShow.OnShow"/> on the
    /// element's current DataContext if it implements <see cref="IOnShow"/>.
    /// </summary>
    public static IDisposable UpdateOnShow(this Visual element, IDisposable? previous = null)
    {
        previous?.Dispose();
        return (element.DataContext as IOnShow)?.OnShow() ?? Disposable.Empty;
    }

    /// <summary>
    /// Wires the full <see cref="IOnShow"/> lifecycle to the element's visual-tree and
    /// DataContext events. Call once from the constructor after InitializeComponent().
    /// <para>
    /// On attach or DataContext change: calls <see cref="UpdateOnShow"/> and invokes
    /// <paramref name="onContextUpdated"/> with the new DataContext.
    /// On detach: disposes both subscriptions.
    /// </para>
    /// </summary>
    public static IDisposable WireOnShow(
        this Visual element,
        Func<object?, IDisposable?>? onContextUpdated = null)
    {
        IDisposable? showSubscription = null;
        IDisposable? contextSubscription = null;

        void Update()
        {
            showSubscription = element.UpdateOnShow(showSubscription);
            contextSubscription?.Dispose();
            contextSubscription = onContextUpdated?.Invoke(element.DataContext);
        }

        void Teardown()
        {
            showSubscription?.Dispose();
            showSubscription = null;
            contextSubscription?.Dispose();
            contextSubscription = null;
        }

        return new CompositeDisposable
        {
            Observable.FromEventPattern<VisualTreeAttachmentEventArgs>(
                    h => element.AttachedToVisualTree += h,
                    h => element.AttachedToVisualTree -= h)
                .Subscribe(_ => Update()),

            Observable.FromEventPattern<VisualTreeAttachmentEventArgs>(
                    h => element.DetachedFromVisualTree += h,
                    h => element.DetachedFromVisualTree -= h)
                .Subscribe(_ => Teardown()),

            Observable.FromEventPattern(element, nameof(element.DataContextChanged))
                .Subscribe(_ => Update())
        };
    }
}
