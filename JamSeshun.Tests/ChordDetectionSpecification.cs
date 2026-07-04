using JamSeshun.Services;
using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Tests the DSP chord-detection engine (chroma extraction + template matching) on
/// synthesised guitar chords.  The vocabulary is restricted to a song's chord set,
/// exactly as <see cref="ChordTrackerService"/> uses it in performance mode.
/// </summary>
public class ChordDetectionSpecification
{
    private const int SampleRate = 16000;
    private const int WindowSize = 8192;

    private static readonly string[] wonderwallChords = ["Em7", "G", "Dsus4", "A7sus4", "C"];

    private static Chord Chord(string name) => new(name, ChordLibrary.Lookup(name));

    private static ChordMatch? Detect(string played, params string[] vocabulary)
    {
        var matcher = new ChordTemplateMatcher(vocabulary.Select(ChordDetectionSpecification.Chord));
        var chroma = new ChromaExtractor(SampleRate);
        var audio = AudioSynthesizer.Chord(played, 1.0, SampleRate);

        return matcher.Classify(chroma.Extract(audio.AsSpan(0, WindowSize)));
    }

    [Theory]
    [InlineData("G")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("Em")]
    public void SimpleVocabulary_ClassifiesEachChordCorrectly(string played)
    {
        var match = ChordDetectionSpecification.Detect(played, "G", "C", "D", "Em");

        Assert.NotNull(match);
        Assert.Equal(played, match.ChordName);
        Assert.True(match.Confidence >= 0.6f, $"confidence {match.Confidence} below advance gate");
    }

    [Theory]
    [InlineData("Em7")]
    [InlineData("C")]
    public void RealSongVocabulary_SeparatesCloselyVoicedChords(string played)
    {
        var match = ChordDetectionSpecification.Detect(played, ChordDetectionSpecification.wonderwallChords);

        Assert.NotNull(match);
        Assert.Equal(played, match.ChordName);
    }

    [Fact]
    public void WhiteNoise_DoesNotProduceAConfidentMatch()
    {
        var matcher = new ChordTemplateMatcher(
            new[] { "G", "C", "D", "Em" }.Select(ChordDetectionSpecification.Chord));
        var chroma = new ChromaExtractor(SampleRate);

        var random = new Random(42);
        var noise = new float[WindowSize];
        for (int i = 0; i < noise.Length; i++)
        {
            noise[i] = (float)(random.NextDouble() * 0.4 - 0.2);
        }

        var match = matcher.Classify(chroma.Extract(noise));

        Assert.True(match == null || match.Confidence < 0.6f,
            $"noise matched {match?.ChordName} at {match?.Confidence}");
    }

    [Fact]
    public void Silence_ProducesNoMatch()
    {
        var matcher = new ChordTemplateMatcher(
            new[] { "G", "C" }.Select(ChordDetectionSpecification.Chord));
        var chroma = new ChromaExtractor(SampleRate);

        var match = matcher.Classify(chroma.Extract(new float[WindowSize]));

        Assert.Null(match);
    }

    [Fact]
    public void EnharmonicDuplicatesInVocabulary_DoNotSplitConfidence()
    {
        // Db and C# are the same chord; if both templates survived, softmax
        // confidence could never exceed 0.5 and the advance gate would never open.
        var matcher = new ChordTemplateMatcher(
            new[] { ChordDetectionSpecification.Chord("C#"), ChordDetectionSpecification.Chord("Db"), ChordDetectionSpecification.Chord("G") });

        Assert.Equal(2, matcher.TemplateCount);
    }
}
