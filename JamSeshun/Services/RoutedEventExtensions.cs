using Avalonia.Interactivity;
using System.Reactive.Disposables;

namespace JamSeshun.Services;

public static class RoutedEventExtensions
{
    public static IObservable<TEventArgs> AsObservable<TEventArgs>(
        this Interactive target,
        RoutedEvent<TEventArgs> routedEvent,
        RoutingStrategies routingStrategies = RoutingStrategies.Bubble)
        where TEventArgs : RoutedEventArgs
    {
        return Observable.Create<TEventArgs>(observer =>
        {
            EventHandler<TEventArgs> handler = (_, e) => observer.OnNext(e);
            target.AddHandler(routedEvent, handler, routingStrategies);
            return Disposable.Create<(Interactive Target, RoutedEvent<TEventArgs> RoutedEvent, EventHandler<TEventArgs> Handler)>(
                (target, routedEvent, handler),
                static state => state.Target.RemoveHandler(state.RoutedEvent, state.Handler));
        });
    }
}
