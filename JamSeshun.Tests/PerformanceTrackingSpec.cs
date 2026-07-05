using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Headless.XUnit;
using JamSeshun.Services;
using JamSeshun.Services.Tuning;
using JamSeshun.ViewModels;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for the audio-tracking wiring in <see cref="PerformanceViewModel"/>: the
/// enable toggle starts/stops tracker subscriptions while the view is shown, tracker
/// updates advance the current line (never backwards), and manual navigation is fed
/// back to the trackers through the position observable.
/// </summary>
public class PerformanceTrackingSpec
{
    private const string TabContent =
        "G  Em\n" +
        "First line of words\n" +
        "C  D\n" +
        "Second line of words\n" +
        "Am  F\n" +
        "Third line of words\n";

    private readonly FakeNavigationService navigation = new();
    private readonly FakeChordTracker chordTracker = new();
    private readonly FakeAudioCaptureService capture = new();

    private PerformanceViewModel CreateShownViewModel(out IDisposable shown)
    {
        var vm = new PerformanceViewModel(this.navigation, this.chordTracker, null, this.capture);
        vm.LoadForTab(new SavedTab("Artist", "Song", PerformanceTrackingSpec.TabContent));
        shown = vm.OnShow();
        return vm;
    }

    [AvaloniaFact]
    public void WithoutTrackers_TogglingIsHiddenAndInert()
    {
        var vm = new PerformanceViewModel(this.navigation, null, null, this.capture);
        vm.LoadForTab(new SavedTab("Artist", "Song", PerformanceTrackingSpec.TabContent));
        using var shown = vm.OnShow();

        Assert.False(vm.CanAudioTrack);
        vm.AudioTrackingEnabled = true;
        Assert.False(vm.IsTracking);
    }

    [AvaloniaFact]
    public void EnablingTheToggleWhileShown_StartsTracking()
    {
        var vm = this.CreateShownViewModel(out var shown);
        using (shown)
        {
            Assert.Equal(0, this.chordTracker.ActiveSubscriptions);

            vm.AudioTrackingEnabled = true;

            Assert.Equal(1, this.chordTracker.ActiveSubscriptions);
            Assert.True(vm.IsTracking);

            vm.AudioTrackingEnabled = false;

            Assert.Equal(0, this.chordTracker.ActiveSubscriptions);
            Assert.False(vm.IsTracking);
        }
    }

    [AvaloniaFact]
    public void HidingTheView_StopsTracking()
    {
        var vm = this.CreateShownViewModel(out var shown);
        vm.AudioTrackingEnabled = true;
        Assert.Equal(1, this.chordTracker.ActiveSubscriptions);

        shown.Dispose();

        Assert.Equal(0, this.chordTracker.ActiveSubscriptions);
        Assert.False(vm.IsTracking);
    }

    [AvaloniaFact]
    public void TrackerUpdate_AdvancesTheCurrentLine()
    {
        var vm = this.CreateShownViewModel(out var shown);
        using (shown)
        {
            vm.AudioTrackingEnabled = true;

            this.chordTracker.Updates.OnNext(new ChordTrackingUpdate(1, "C", 0.85f));

            Assert.Equal(2, vm.Position); // 1-based display
            Assert.Equal("C", vm.LastDetectedChord);
            Assert.Equal(0.85f, vm.TrackingConfidence);
        }
    }

    [AvaloniaFact]
    public void StaleBackwardUpdate_IsIgnored()
    {
        var vm = this.CreateShownViewModel(out var shown);
        using (shown)
        {
            vm.AudioTrackingEnabled = true;
            vm.NextCommand.Execute(null);
            vm.NextCommand.Execute(null);
            Assert.Equal(3, vm.Position);

            this.chordTracker.Updates.OnNext(new ChordTrackingUpdate(1, "C", 0.9f));

            Assert.Equal(3, vm.Position);
        }
    }

    [AvaloniaFact]
    public void ManualNavigation_IsFedBackToTheTracker()
    {
        var vm = this.CreateShownViewModel(out var shown);
        using (shown)
        {
            vm.AudioTrackingEnabled = true;

            vm.NextCommand.Execute(null);
            vm.NextCommand.Execute(null);
            vm.PreviousCommand.Execute(null);

            Assert.Equal([0, 1, 2, 1], this.chordTracker.ObservedPositions);
        }
    }

    [AvaloniaFact]
    public void EnablingBeforeShowing_StartsTrackingOnShow()
    {
        var vm = new PerformanceViewModel(this.navigation, this.chordTracker, null, this.capture);
        vm.LoadForTab(new SavedTab("Artist", "Song", PerformanceTrackingSpec.TabContent));
        vm.AudioTrackingEnabled = true;
        Assert.Equal(0, this.chordTracker.ActiveSubscriptions);

        using var shown = vm.OnShow();

        Assert.Equal(1, this.chordTracker.ActiveSubscriptions);
    }

    [AvaloniaFact]
    public void DeviceDiscovery_DefaultsBothGuitarAndVoiceToTheDefaultDevice()
    {
        var vm = this.CreateShownViewModel(out var shown);
        using (shown)
        {
            Assert.Equal(FakeAudioCaptureService.Device, vm.GuitarDevice);
            Assert.Equal(FakeAudioCaptureService.Device, vm.VoiceDevice);
        }
    }

    [AvaloniaFact]
    public void SettingDifferentVoiceDevice_RoutesEachTrackerItsOwnDevice()
    {
        var multiDeviceCapture = new MultiDeviceFakeAudioCaptureService();
        var lyricTracker = new FakeLyricTracker();
        var vm = new PerformanceViewModel(this.navigation, this.chordTracker, lyricTracker, multiDeviceCapture);
        vm.LoadForTab(new SavedTab("Artist", "Song", PerformanceTrackingSpec.TabContent));
        using var shown = vm.OnShow();

        vm.VoiceDevice = MultiDeviceFakeAudioCaptureService.SecondaryDevice;
        vm.AudioTrackingEnabled = true;

        Assert.Equal(MultiDeviceFakeAudioCaptureService.PrimaryDevice, this.chordTracker.LastDevice);
        Assert.Equal(MultiDeviceFakeAudioCaptureService.SecondaryDevice, lyricTracker.LastDevice);
    }

    [AvaloniaFact]
    public void ChangingGuitarDeviceWhileTracking_RestartsTheChordTracker()
    {
        var multiDeviceCapture = new MultiDeviceFakeAudioCaptureService();
        var vm = new PerformanceViewModel(this.navigation, this.chordTracker, null, multiDeviceCapture);
        vm.LoadForTab(new SavedTab("Artist", "Song", PerformanceTrackingSpec.TabContent));
        using var shown = vm.OnShow();

        vm.AudioTrackingEnabled = true;
        Assert.Equal(1, this.chordTracker.ActiveSubscriptions);
        Assert.Equal(MultiDeviceFakeAudioCaptureService.PrimaryDevice, this.chordTracker.LastDevice);

        vm.GuitarDevice = MultiDeviceFakeAudioCaptureService.SecondaryDevice;

        // Old subscription torn down and a new one started against the new device.
        Assert.Equal(1, this.chordTracker.ActiveSubscriptions);
        Assert.Equal(MultiDeviceFakeAudioCaptureService.SecondaryDevice, this.chordTracker.LastDevice);
    }

    /// <summary>Exposes two devices so guitar/voice routing tests can tell them apart.</summary>
    private sealed class MultiDeviceFakeAudioCaptureService : IAudioCaptureService
    {
        public static readonly AudioCaptureDevice PrimaryDevice = new(1, "Primary Microphone", IsDefault: true);
        public static readonly AudioCaptureDevice SecondaryDevice = new(2, "Secondary Microphone");

        public IObservable<IReadOnlyList<AudioCaptureDevice>> GetAudioCaptureDevices() =>
            Observable.Return<IReadOnlyList<AudioCaptureDevice>>(
                [MultiDeviceFakeAudioCaptureService.PrimaryDevice, MultiDeviceFakeAudioCaptureService.SecondaryDevice]);

        public IObservable<AudioFrame> Capture(AudioCaptureDevice device) => Observable.Never<AudioFrame>();
    }

    private sealed class FakeLyricTracker : ILyricTrackerService
    {
        public AudioCaptureDevice? LastDevice { get; private set; }

        public IObservable<LyricTrackingUpdate> TrackLyrics(
            AudioCaptureDevice device,
            IReadOnlyList<string> lines,
            IObservable<int> position)
        {
            this.LastDevice = device;
            return Observable.Never<LyricTrackingUpdate>();
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeChordTracker : IChordTrackerService
    {
        public Subject<ChordTrackingUpdate> Updates { get; } = new();

        public int ActiveSubscriptions { get; private set; }

        public List<int> ObservedPositions { get; } = [];

        /// <summary>Device the most recent subscription started with.</summary>
        public AudioCaptureDevice? LastDevice { get; private set; }

        public IObservable<ChordTrackingUpdate> TrackChords(
            AudioCaptureDevice device,
            IReadOnlyList<PerformanceLine> lines,
            IObservable<int> position,
            int capo = 0)
        {
            return Observable.Create<ChordTrackingUpdate>(observer =>
            {
                this.ActiveSubscriptions++;
                this.LastDevice = device;
                return new CompositeDisposable(
                    position.Subscribe(this.ObservedPositions.Add),
                    this.Updates.Subscribe(observer),
                    Disposable.Create(() => this.ActiveSubscriptions--));
            });
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public Task PushAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase =>
            Task.CompletedTask;

        public Task PopAsync() => Task.CompletedTask;

        public Task PopToRootAsync() => Task.CompletedTask;

        public Task ActivateAsync<TViewModel>(Action<TViewModel>? configure = null) where TViewModel : ViewModelBase =>
            Task.CompletedTask;
    }
}
