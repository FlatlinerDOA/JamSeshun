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
    public void ConfidenceDoesNotShrinkAsTheVocabularyGrows()
    {
        // Confidence used to be a softmax over every template, so each extra chord in
        // the song added another positive term to the denominator and pushed confidence
        // down — under a fixed 0.6 advance gate, busier songs simply stopped
        // auto-advancing however cleanly the chord was detected.  Am/F/A share no pitch
        // class with G major, so they cannot be a closer rival than the runner-up in the
        // small vocabulary: a correct confidence measure is unmoved by them.
        var small = ChordDetectionSpecification.Detect("G", "G", "C", "D", "Em");
        var large = ChordDetectionSpecification.Detect("G", "G", "C", "D", "Em", "Am", "F", "A");

        Assert.NotNull(small);
        Assert.NotNull(large);
        Assert.Equal("G", large.ChordName);
        Assert.True(large.Confidence >= 0.6f, $"confidence {large.Confidence} below advance gate");
        Assert.True(
            Math.Abs(large.Confidence - small.Confidence) < 0.001f,
            $"confidence moved from {small.Confidence} to {large.Confidence} on unrelated chords");
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

    // ── Capo transposition ────────────────────────────────────────────────────

    [Theory]
    [InlineData("G", 1)]
    [InlineData("C", 1)]
    [InlineData("D", 2)]
    [InlineData("Em", 2)]
    public void CapoAwareVocabulary_ClassifiesCapoTransposedAudioAsTheWrittenChord(string played, int capo)
    {
        // The vocabulary is named exactly as printed in the tab (the shape fretted);
        // the audio is synthesised as it actually SOUNDS with a capo at the given fret.
        var matcher = new ChordTemplateMatcher(
            new[] { "G", "C", "D", "Em" }.Select(ChordDetectionSpecification.Chord), capo);
        var chroma = new ChromaExtractor(SampleRate);
        var audio = AudioSynthesizer.Chord(played, 1.0, SampleRate, capoSemitones: capo);

        var match = matcher.Classify(chroma.Extract(audio.AsSpan(0, WindowSize)));

        Assert.NotNull(match);
        Assert.Equal(played, match.ChordName);
        Assert.True(match.Confidence >= 0.6f, $"confidence {match.Confidence} below advance gate");
    }

    [Fact]
    public void UntransposedAudio_AgainstACapoAwareMatcher_DoesNotConfidentlyMatch()
    {
        // Negative/regression guard: if the capo threading did nothing, this would
        // still match "G" confidently.  Audio synthesised WITHOUT the capo shift, fed
        // into a capo:1 matcher, must not be classified as "G" with high confidence.
        var matcher = new ChordTemplateMatcher(
            new[] { "G", "C", "D", "Em" }.Select(ChordDetectionSpecification.Chord), capo: 1);
        var chroma = new ChromaExtractor(SampleRate);
        var audio = AudioSynthesizer.Chord("G", 1.0, SampleRate, capoSemitones: 0);

        var match = matcher.Classify(chroma.Extract(audio.AsSpan(0, WindowSize)));

        Assert.True(match == null || match.ChordName != "G" || match.Confidence < 0.6f,
            $"untransposed G matched 'G' confidently ({match?.Confidence}) against a capo-1 matcher");
    }
}
