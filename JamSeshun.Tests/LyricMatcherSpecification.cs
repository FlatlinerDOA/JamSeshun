using JamSeshun.Services.Tuning;

namespace JamSeshun.Tests;

/// <summary>
/// Tests for the token-overlap lyric matcher: threshold, hysteresis, empty-line
/// skipping, and advance-only behaviour (docs/PerformanceAudioTracking.md — Strategy B).
/// </summary>
public class LyricMatcherSpecification
{
    private static readonly string[] lines =
    [
        "today is gonna be the day",
        "somehow you're gonna be the one that saves me",
        "and after all",
        "you're my wonderwall",
    ];

    [Fact]
    public void MatchingFragment_HeldForTwoWindows_Advances()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        var (first, score) = matcher.Match("somehow gonna be the one that saves");
        Assert.Null(first);
        Assert.True(score > 0.4f);

        var (second, _) = matcher.Match("somehow gonna be the one that saves me");
        Assert.Equal(1, second);
    }

    [Fact]
    public void SingleMatchingWindow_DoesNotAdvance()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        var (advance, _) = matcher.Match("somehow you're gonna be the one");

        Assert.Null(advance);
    }

    [Fact]
    public void NonMatchingWindowBetweenMatches_ResetsTheHold()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        matcher.Match("somehow you're gonna be the one that saves me");
        matcher.Match("mumble mumble");
        var (advance, _) = matcher.Match("somehow you're gonna be the one that saves me");

        Assert.Null(advance);
    }

    [Fact]
    public void WeakOverlap_ScoresBelowThreshold_NeverAdvances()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        for (int i = 0; i < 5; i++)
        {
            var (advance, score) = matcher.Match("gonna day"); // 2 of 8 tokens of line 1
            Assert.Null(advance);
            Assert.True(score <= 0.4f, $"score {score}");
        }
    }

    [Fact]
    public void CurrentLineText_IsNotACandidate_OnlyUpcomingLinesAre()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        // Singing the line already on screen must not move anything.
        matcher.Match("today is gonna be the day");
        var (advance, _) = matcher.Match("today is gonna be the day");

        Assert.Null(advance);
    }

    [Fact]
    public void SkippingAheadTwoLines_IsAllowed()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        matcher.Match("and after all");
        var (advance, _) = matcher.Match("and after all");

        Assert.Equal(2, advance);
    }

    [Fact]
    public void EmptyLyricLines_AreSkippedAsCandidates()
    {
        // Line 1 is an instrumental passage — candidates from line 0 are lines 2 and 3.
        var matcher = new LyricMatcher(["intro words here", "", "hello world again", "goodbye now"]);

        matcher.Match("hello world again");
        var (advance, _) = matcher.Match("hello world again");

        Assert.Equal(2, advance);
    }

    [Fact]
    public void PunctuationAndCase_AreIgnored()
    {
        var matcher = new LyricMatcher(["first line", "Some-how, You're GONNA be; the one!"]);

        matcher.Match("somehow youre gonna be the one");
        var (advance, _) = matcher.Match("somehow youre gonna be the one");

        Assert.Equal(1, advance);
    }

    [Fact]
    public void SetPosition_MovesTheWindowAndResetsTheHold()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        matcher.Match("somehow you're gonna be the one that saves me");
        matcher.SetPosition(2);

        // Old hold must not carry over; and from line 2 the candidate is line 3.
        matcher.Match("you're my wonderwall");
        var (advance, _) = matcher.Match("you're my wonderwall");

        Assert.Equal(3, advance);
    }

    [Fact]
    public void AfterAdvancing_MatchingContinuesFromTheNewPosition()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        matcher.Match("somehow you're gonna be the one that saves me");
        matcher.Match("somehow you're gonna be the one that saves me");

        matcher.Match("and after all");
        var (advance, _) = matcher.Match("and after all");

        Assert.Equal(2, advance);
    }

    [Fact]
    public void EmptyFragment_ScoresZeroAndResetsHold()
    {
        var matcher = new LyricMatcher(LyricMatcherSpecification.lines);

        matcher.Match("somehow you're gonna be the one that saves me");
        var (_, score) = matcher.Match("   ");
        var (advance, _) = matcher.Match("somehow you're gonna be the one that saves me");

        Assert.Equal(0f, score);
        Assert.Null(advance);
    }
}
