using JamSeshun.Services;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// The best chord match for a chroma frame.
/// </summary>
/// <param name="ChordName">Chord name exactly as it appears in the vocabulary (i.e. the tab).</param>
/// <param name="PitchClasses">Pitch-class set of the matched chord, for set-based comparison.</param>
/// <param name="Similarity">Raw cosine similarity of chroma vs template (0–1).</param>
/// <param name="Confidence">
/// Softmax-normalised confidence over the whole vocabulary (0–1): high only when the
/// best template fits clearly better than the alternatives, low on ambiguous or noisy
/// frames — feeds the ≥ 0.6 advance gate and the UI indicator.
/// </param>
public sealed record ChordMatch(
    string ChordName,
    IReadOnlySet<int> PitchClasses,
    float Similarity,
    float Confidence);

/// <summary>
/// Classifies chroma vectors against templates built from a fixed chord vocabulary —
/// in performance mode, the chords of the song being played.  Restricting the
/// vocabulary to the song's own chords is what makes template matching reliable:
/// the matcher only has to separate a handful of alternatives, not all 24+ chords.
/// </summary>
public sealed class ChordTemplateMatcher
{
    private const float RootEmphasis = 1.25f;   // roots ring loudest on guitar
    private const float MinSimilarity = 0.55f;  // below this, the frame is noise — no match
    private const double SoftmaxTemperature = 0.05;

    private readonly List<(string Name, IReadOnlySet<int> PitchClasses, float[] Template)> templates = [];

    /// <param name="vocabulary">Chords named exactly as printed in the tab (the SHAPE fretted).</param>
    /// <param name="capo">
    /// The fret the song's capo sits on (0 = none).  Templates are built in capo-transposed
    /// pitch-class space so they match the actual sounding audio, which the microphone hears
    /// at concert pitch rather than the shape's open-position pitch.
    /// </param>
    public ChordTemplateMatcher(IEnumerable<Chord> vocabulary, int capo = 0)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chord in vocabulary)
        {
            var pitchClasses = ChordNotes.GetPitchClasses(chord, capo);
            if (pitchClasses.Count == 0)
            {
                continue;
            }

            // Enharmonic duplicates (and identical voicings under different names)
            // would split the softmax mass, so key by pitch-class set.
            var key = string.Join(',', pitchClasses.Order());
            if (!seen.Add(key))
            {
                continue;
            }

            this.templates.Add((chord.Name, pitchClasses, ChordTemplateMatcher.BuildTemplate(chord, pitchClasses, capo)));
        }
    }

    /// <summary>Number of distinct templates in the vocabulary.</summary>
    public int TemplateCount => this.templates.Count;

    /// <summary>
    /// Returns the best match for <paramref name="chroma"/> (12-bin, L2-normalised),
    /// or null when nothing in the vocabulary fits well enough.
    /// </summary>
    public ChordMatch? Classify(ReadOnlySpan<float> chroma)
    {
        if (this.templates.Count == 0)
        {
            return null;
        }

        int bestIndex = -1;
        double bestSimilarity = double.MinValue;
        Span<double> similarities = stackalloc double[this.templates.Count];

        for (int t = 0; t < this.templates.Count; t++)
        {
            double dot = 0;
            var template = this.templates[t].Template;
            for (int i = 0; i < 12; i++)
            {
                dot += (double)chroma[i] * template[i];
            }

            similarities[t] = dot;
            if (dot > bestSimilarity)
            {
                bestSimilarity = dot;
                bestIndex = t;
            }
        }

        if (bestSimilarity < MinSimilarity)
        {
            return null;
        }

        // Softmax over similarities: confidence reflects the margin over the
        // runner-up rather than the absolute fit alone.
        double denominator = 0;
        for (int t = 0; t < similarities.Length; t++)
        {
            denominator += Math.Exp((similarities[t] - bestSimilarity) / SoftmaxTemperature);
        }

        var (name, pitchClasses, _) = this.templates[bestIndex];
        return new ChordMatch(name, pitchClasses, (float)bestSimilarity, (float)(1.0 / denominator));
    }

    private static float[] BuildTemplate(Chord chord, IReadOnlySet<int> pitchClasses, int capo = 0)
    {
        var template = new float[12];
        foreach (var pc in pitchClasses)
        {
            template[pc] = 1f;
        }

        // pitchClasses (and thus the template bins) are already capo-shifted, so the
        // root looked up here must be shifted by the same amount or it will land on
        // the wrong (or an empty) bin.
        var root = ChordNotes.ParseRootPitchClass(chord.Name);
        var shiftedRoot = root != null ? ((root.Value + capo) % 12 + 12) % 12 : (int?)null;
        if (shiftedRoot != null && template[shiftedRoot.Value] > 0)
        {
            template[shiftedRoot.Value] = RootEmphasis;
        }

        double norm = 0;
        for (int i = 0; i < 12; i++)
        {
            norm += (double)template[i] * template[i];
        }

        float scale = (float)(1.0 / Math.Sqrt(norm));
        for (int i = 0; i < 12; i++)
        {
            template[i] *= scale;
        }

        return template;
    }
}
