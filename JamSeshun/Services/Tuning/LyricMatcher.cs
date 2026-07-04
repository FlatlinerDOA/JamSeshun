namespace JamSeshun.Services.Tuning;

/// <summary>
/// Pure fuzzy matcher that decides when a transcribed fragment indicates the singer
/// has moved on to an upcoming lyric line.  Implements the algorithm from
/// <c>docs/PerformanceAudioTracking.md</c> — Strategy B: normalised token overlap
/// scored against the next two non-empty lyric lines, threshold 0.4, with two-window
/// hysteresis.  Advance-only; lines with empty lyric text (instrument-only passages)
/// are skipped — the chord tracker owns progression through those.
/// </summary>
public sealed class LyricMatcher
{
    private const float AdvanceThreshold = 0.4f;
    private const int HoldWindows = 2;

    private readonly IReadOnlyList<string[]> tokenizedLines;
    private int currentIndex;
    private int heldTarget = -1;
    private int holdCount;

    public LyricMatcher(IReadOnlyList<string> lines)
    {
        this.tokenizedLines = lines.Select(LyricMatcher.Tokenize).ToList();
    }

    /// <summary>Moves the matcher to the caller's current line (e.g. after manual navigation).</summary>
    public void SetPosition(int index)
    {
        if (index != this.currentIndex)
        {
            this.currentIndex = index;
            this.ResetHold();
        }
    }

    /// <summary>
    /// Scores <paramref name="fragment"/> against upcoming lines.  Returns the score
    /// and, when the advance threshold has held for <see cref="HoldWindows"/>
    /// consecutive calls, the index to advance to; null otherwise.
    /// </summary>
    public (int? AdvanceTo, float Score) Match(string fragment)
    {
        var tokens = LyricMatcher.Tokenize(fragment);
        if (tokens.Length == 0)
        {
            this.ResetHold();
            return (null, 0f);
        }

        // Candidates: the next two non-empty lyric lines after the current position.
        int best = -1;
        float bestScore = 0f;
        int found = 0;
        for (int i = this.currentIndex + 1; i < this.tokenizedLines.Count && found < 2; i++)
        {
            var line = this.tokenizedLines[i];
            if (line.Length == 0)
            {
                continue;
            }

            found++;
            float score = LyricMatcher.Overlap(tokens, line);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        if (best < 0 || bestScore <= AdvanceThreshold)
        {
            this.ResetHold();
            return (null, bestScore);
        }

        if (best != this.heldTarget)
        {
            this.heldTarget = best;
            this.holdCount = 0;
        }

        this.holdCount++;
        if (this.holdCount < HoldWindows)
        {
            return (null, bestScore);
        }

        this.currentIndex = best; // advance locally, ahead of position feedback
        this.ResetHold();
        return (best, bestScore);
    }

    private void ResetHold()
    {
        this.heldTarget = -1;
        this.holdCount = 0;
    }

    /// <summary>|fragment ∩ line| / |line| over normalised tokens.</summary>
    private static float Overlap(string[] fragment, string[] line)
    {
        var fragmentSet = new HashSet<string>(fragment, StringComparer.Ordinal);
        int hits = 0;
        foreach (var token in line.Distinct(StringComparer.Ordinal))
        {
            if (fragmentSet.Contains(token))
            {
                hits++;
            }
        }

        return (float)hits / Math.Max(line.Distinct(StringComparer.Ordinal).Count(), 1);
    }

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => new string(t.Where(char.IsLetterOrDigit).ToArray()))
            .Where(t => t.Length > 0)
            .ToArray();
    }
}
