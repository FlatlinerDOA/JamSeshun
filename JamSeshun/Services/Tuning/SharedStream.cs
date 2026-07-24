using System.Reactive.Linq;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// Multicasting helper for the shared-capture requirement in
/// <see cref="IAudioCaptureService"/> (one open device, many analysers).
/// </summary>
public static class SharedStream
{
    /// <summary>
    /// Shares one subscription to <paramref name="source"/> across concurrent
    /// subscribers — the same intent as <c>Publish().RefCount()</c>, but the shared
    /// instance is dropped as soon as it goes cold so a later subscriber always gets a
    /// live stream.
    ///
    /// <para>
    /// <c>Publish()</c> multicasts through a single <c>Subject&lt;T&gt;</c> that lives
    /// for the lifetime of the connectable observable.  A subject remembers its terminal
    /// notification: once the capture source calls <c>OnError</c> — device unplugged,
    /// microphone permission revoked, Android <c>AudioRecord</c> read failure — every
    /// future subscriber to that same subject is handed the error immediately, even
    /// after <c>RefCount</c> has dropped to zero and reconnected.  Caching such a stream
    /// per device (as the capture services do) therefore turns one transient failure
    /// into permanently broken audio until the app restarts.  Recreating the subject
    /// whenever the stream goes cold makes the failure recoverable: toggling tracking
    /// off and on, or leaving and re-entering the view, opens the device again.
    /// </para>
    /// </summary>
    public static IObservable<T> ShareResettable<T>(this IObservable<T> source)
    {
        var gate = new object();
        IObservable<T>? shared = null;

        return Observable.Defer<T>(() =>
        {
            lock (gate)
            {
                var existing = shared;
                if (existing != null)
                {
                    return existing;
                }

                // Assigned below, before anything can subscribe and therefore before
                // Finally can run, so the closure always sees the instance it belongs to.
                IObservable<T>? created = null;
                var instance = source
                    .Finally(() =>
                    {
                        lock (gate)
                        {
                            // Only retire our own instance: a subscriber that raced in
                            // ahead of this teardown may already have installed a newer one.
                            if (ReferenceEquals(shared, created))
                            {
                                shared = null;
                            }
                        }
                    })
                    .Publish()
                    .RefCount();

                created = instance;
                shared = instance;
                return instance;
            }
        });
    }
}
