using System.Reactive.Linq;
using System.Reactive.Subjects;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for <see cref="SharedStream.ShareResettable{T}"/>, the multicast used by every
/// <see cref="IAudioCaptureService"/> so simultaneous analysers open one device.
/// </summary>
public class SharedStreamSpecification
{
    [Fact]
    public void ConcurrentSubscribers_ShareOneSubscriptionToTheSource()
    {
        int opened = 0;
        var shared = Observable.Defer(() =>
        {
            opened++;
            return Observable.Never<int>();
        }).ShareResettable();

        using var first = shared.Subscribe();
        using var second = shared.Subscribe();

        Assert.Equal(1, opened);
    }

    [Fact]
    public void AfterTheLastSubscriberLeaves_TheNextSubscriberReopensTheSource()
    {
        int opened = 0;
        var shared = Observable.Defer(() =>
        {
            opened++;
            return Observable.Never<int>();
        }).ShareResettable();

        shared.Subscribe().Dispose();
        shared.Subscribe().Dispose();

        Assert.Equal(2, opened);
    }

    [Fact]
    public void SubscribersReceiveTheSourcesValues()
    {
        var source = new Subject<int>();
        var shared = source.ShareResettable();
        var received = new List<int>();

        using var subscription = shared.Subscribe(received.Add);
        source.OnNext(7);

        Assert.Equal([7], received);
    }

    [Fact]
    public void AfterTheSourceFails_ALaterSubscriberStillGetsALiveStream()
    {
        // The regression this exists for: Publish().RefCount() multicasts through one
        // Subject for the lifetime of the connectable observable, and a Subject replays
        // its terminal notification forever.  Because the capture services cache the
        // shared stream per device, a single microphone failure used to leave every
        // later subscriber — the tuner included — receiving that error immediately,
        // with no way back short of restarting the app.
        int opened = 0;
        var shared = Observable.Defer(() =>
            opened++ == 0
                ? Observable.Throw<int>(new InvalidOperationException("device lost"))
                : Observable.Return(42)).ShareResettable();

        var beforeFailure = new List<int>();
        Exception? firstError = null;
        shared.Subscribe(beforeFailure.Add, error => firstError = error);

        var received = new List<int>();
        Exception? secondError = null;
        shared.Subscribe(received.Add, error => secondError = error);

        Assert.IsType<InvalidOperationException>(firstError);
        Assert.Null(secondError);
        Assert.Equal([42], received);
    }
}
