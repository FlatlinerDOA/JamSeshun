using Android.Media;
using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace JamSeshun.Android;

/// <summary>
/// Code taken from https://github.com/drasticactions/Drastic.AudioRecorder/blob/main/src/Drastic.AudioRecorder/Android/AudioStream.Android.cs
/// (MIT License)
/// </summary>
internal class AudioStream
{
    private readonly int bufferSize;
    private readonly ChannelIn channels = ChannelIn.Mono;
    private readonly Encoding audioFormat = Encoding.Pcm16bit;
    private readonly Subject<ReadOnlyMemory<byte>> audioBuffers = new();
    private readonly Subject<bool> isActiveChanges = new();

    /// <summary>
    /// The audio source.
    /// </summary>
    AudioRecord? audioSource;

    /// <summary>
    /// Occurs when new audio has been streamed.
    /// </summary>
    public IObservable<ReadOnlyMemory<byte>> AudioBuffers => this.audioBuffers;

    /// <summary>
    /// Occurs when the audio stream active status changes.
    /// </summary>
    public IObservable<bool> IsActiveChanges => this.isActiveChanges;

    /// <summary>
    /// The default device.
    /// </summary>
    public static readonly AudioSource DefaultDevice = AudioSource.Mic;

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    /// <value>
    /// The sample rate.
    /// </value>
    public int SampleRate { get; private set; } = 44100;

    /// <summary>
    /// Gets bits per sample.
    /// </summary>
    public int BitsPerSample => this.audioSource is not null ? this.audioSource.AudioFormat == Encoding.Pcm16bit ? 16 : 8 : 0;

    /// <summary>
    /// Gets the channel count.
    /// </summary>
    /// <value>
    /// The channel count.
    /// </value>        
    public int ChannelCount => this.audioSource?.ChannelCount ?? 0;

    /// <summary>
    /// Gets the average data transfer rate
    /// </summary>
    /// <value>The average data transfer rate in bytes per second.</value>
    public int AverageBytesPerSecond => this.SampleRate * this.BitsPerSample / 8 * this.ChannelCount;

    /// <summary>
    /// Gets a value indicating if the audio stream is active.
    /// </summary>
    public bool IsActive => this.audioSource?.RecordingState == RecordState.Recording;

    void Init()
    {
        this.Stop(); // just in case

        this.audioSource = new AudioRecord(
            DefaultDevice,
            this.SampleRate,
            this.channels,
            this.audioFormat,
            this.bufferSize);

        if (this.audioSource.State == State.Uninitialized)
        {
            throw new Exception(
                "Unable to successfully initialize AudioStream; reporting State.Uninitialized.  If using an emulator, make sure it has access to the system microphone.");
        }
    }

    /// <summary>
    /// Starts the audio stream.
    /// </summary>
    public async Task Start()
    {
        if (!await CheckPermission())
        {
            return;
        }

        try
        {
            if (!this.IsActive)
            {
                // not sure this does anything or if should be here... inherited via copied code ¯\_(ツ)_/¯
                global::Android.OS.Process.SetThreadPriority(global::Android.OS.ThreadPriority.UrgentAudio);

                this.Init();

                if (this.audioSource == null)
                {
                    throw new InvalidOperationException("Could not initialize audio source.");
                }

                this.audioSource.StartRecording();

                this.isActiveChanges.OnNext(true);
                Task.Run(() => this.Record());
            }

            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Error in AudioStream.Start(): {0}", ex.Message);

            await this.Stop();
            throw;
        }
    }

    /// <summary>
    /// Stops the audio stream.
    /// </summary>
    public Task Stop()
    {
        if (this.IsActive)
        {
            this.audioSource!.Stop();
            this.audioSource.Release();
            this.isActiveChanges.OnNext(false);
        }
        else // just in case
        {
            this.audioSource?.Release();
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioStream"/> class.
    /// </summary>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">The <see cref="ChannelIn"/> value representing the number of channels to record.</param>
    /// <param name="audioFormat">The format of the recorded audio.</param>
    public AudioStream(
        int sampleRate = 44100,
        ChannelIn channels = ChannelIn.Mono,
        Encoding audioFormat = Encoding.Pcm16bit)
    {
        this.bufferSize = AudioRecord.GetMinBufferSize(sampleRate, channels, audioFormat);

        if (this.bufferSize < 0)
        {
            throw new Exception(
                "Invalid buffer size calculated; audio settings used may not be supported on this device");
        }

        this.SampleRate = sampleRate;
        this.channels = channels;
        this.audioFormat = audioFormat;
    }

    /// <summary>
    /// Record from the microphone and broadcast the buffer.
    /// </summary>
    async Task Record()
    {
        byte[] data = new byte[this.bufferSize];
        int readFailureCount = 0;
        int readResult = 0;

        Debug.WriteLine("AudioStream.Record(): Starting background loop to read audio stream");

        while (this.IsActive && this.audioBuffers.HasObservers)
        {
            try
            {
                // not sure if this is even a good idea, but we'll try to allow a single bad read, and past that shut it down
                if (readFailureCount > 1)
                {
                    Debug.WriteLine("AudioStream.Record(): Multiple read failures detected, stopping stream");
                    await this.Stop();
                    break;
                }

                readResult = this.audioSource!.Read(data, 0, this.bufferSize); // this can block if there are no bytes to read

                // readResult should == the # bytes read, except a few special cases
                if (readResult > 0)
                {
                    readFailureCount = 0;
                    this.audioBuffers.OnNext(data.AsMemory().Slice(0, readResult));
                }
                else
                {
                    switch (readResult)
                    {
                        case (int)TrackStatus.ErrorInvalidOperation:
                        case (int)TrackStatus.ErrorBadValue:
                        case (int)TrackStatus.ErrorDeadObject:
                            Debug.WriteLine("AudioStream.Record(): readResult returned error code: {0}", readResult);
                            await this.Stop();
                            break;
                        //case (int)TrackStatus.Error:
                        default:
                            readFailureCount++;
                            Debug.WriteLine("AudioStream.Record(): readResult returned error code: {0}", readResult);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                readFailureCount++;
                Debug.WriteLine("Error in Android AudioStream.Record(): {0}", ex.Message);
                await this.Stop();
                this.audioBuffers.OnError(ex);
                return;
            }
        }

        await this.Stop();
    }

    public static async Task<bool> CheckPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
        {
            // Prompt the user to turn on in settings
            // On iOS once a permission has been denied it may not be requested again from the application
            return false;
        }

        status = await Permissions.RequestAsync<Permissions.Microphone>();
        return status == PermissionStatus.Granted;
    }

    /// <summary>
    /// Flushes any audio bytes in memory but not yet broadcast out to any listeners.
    /// </summary>
    public void Flush()
    {
        // not needed for this implementation
    }
}
