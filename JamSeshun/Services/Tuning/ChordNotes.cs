using JamSeshun.Services;

namespace JamSeshun.Services.Tuning;

/// <summary>
/// Converts chords to pitch-class sets and normalises enharmonic spellings, shared by
/// the chord tracker's template matching and its advance logic (see
/// <c>docs/PerformanceAudioTracking.md</c> — "Enharmonic normalisation").  Canonical
/// spelling prefers sharps (Db → C#).
/// </summary>
public static class ChordNotes
{
    /// <summary>Standard-tuning open-string MIDI numbers, low E to high e.</summary>
    private static readonly int[] openStringMidi = [40, 45, 50, 55, 59, 64];

    private static readonly string[] sharpNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>
    /// Returns the set of pitch classes (0 = C … 11 = B) sounded by <paramref name="chord"/>,
    /// shifted up by <paramref name="capo"/> semitones (mod 12).  Uses the fingering when
    /// available (exact voicing), otherwise derives the classes from the chord name (root +
    /// quality intervals).  Returns an empty set when the name cannot be parsed.
    /// </summary>
    /// <param name="chord">The chord shape/name as printed in the tab.</param>
    /// <param name="capo">
    /// The fret the song's capo sits on (0 = none).  A capo raises every open string's
    /// sounding pitch by <paramref name="capo"/> semitones, so the chord SHAPE fretted
    /// (what <paramref name="chord"/> describes) sounds <paramref name="capo"/> semitones
    /// higher than the same shape would with no capo.
    /// </param>
    public static IReadOnlySet<int> GetPitchClasses(Chord chord, int capo = 0)
    {
        if (chord.Frets is { Length: 6 } frets)
        {
            var classes = new HashSet<int>();
            for (int s = 0; s < 6; s++)
            {
                if (frets[s] >= 0)
                {
                    classes.Add(ChordNotes.ShiftByCapo(ChordNotes.openStringMidi[s] + frets[s], capo));
                }
            }

            return classes;
        }

        return ChordNotes.GetPitchClassesFromName(chord.Name, capo);
    }

    /// <summary>
    /// Canonicalises the root (and slash bass) spelling of a chord name to sharps:
    /// "Db" → "C#", "Ebm7" → "D#m7", "F#/Gb" variants collapse.  Quality text is preserved.
    /// </summary>
    public static string Normalize(string chordName)
    {
        if (string.IsNullOrEmpty(chordName))
        {
            return string.Empty;
        }

        var slash = chordName.IndexOf('/');
        if (slash > 0 && slash < chordName.Length - 1)
        {
            return ChordNotes.NormalizePart(chordName[..slash]) + "/" +
                   ChordNotes.NormalizePart(chordName[(slash + 1)..]);
        }

        return ChordNotes.NormalizePart(chordName);
    }

    /// <summary>
    /// True when two chord names refer to the same chord after enharmonic
    /// normalisation (e.g. "Db" ≡ "C#", "Ebm" ≡ "D#m").
    /// </summary>
    public static bool AreEquivalent(string a, string b) =>
        string.Equals(ChordNotes.Normalize(a), ChordNotes.Normalize(b), StringComparison.Ordinal);

    /// <summary>Parses the root pitch class of a chord name, or null when unparseable.</summary>
    public static int? ParseRootPitchClass(string chordName)
    {
        if (string.IsNullOrEmpty(chordName))
        {
            return null;
        }

        int pc = chordName[0] switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1,
        };

        if (pc < 0)
        {
            return null;
        }

        if (chordName.Length > 1)
        {
            if (chordName[1] == '#')
            {
                pc = (pc + 1) % 12;
            }
            else if (chordName[1] == 'b')
            {
                pc = (pc + 11) % 12;
            }
        }

        return pc;
    }

    private static IReadOnlySet<int> GetPitchClassesFromName(string chordName, int capo = 0)
    {
        // Reject ordinary words that merely start with A–G ("Chorus", "Bridge")
        // using the same strict token validation as the tab parser.
        var root = ChordNotes.ParseRootPitchClass(chordName);
        if (root == null || !WikiTabParser.ChordRegex.IsMatch(chordName))
        {
            return new HashSet<int>();
        }

        string bassName = string.Empty;
        var slash = chordName.IndexOf('/');
        if (slash > 0 && slash < chordName.Length - 1)
        {
            bassName = chordName[(slash + 1)..];
            chordName = chordName[..slash];
        }

        int qualityStart = chordName.Length > 1 && (chordName[1] == '#' || chordName[1] == 'b') ? 2 : 1;
        var quality = chordName[qualityStart..];

        var classes = new HashSet<int> { ChordNotes.ShiftByCapo(root.Value, capo) };
        foreach (var interval in ChordNotes.QualityIntervals(quality))
        {
            classes.Add(ChordNotes.ShiftByCapo(root.Value + interval, capo));
        }

        var bass = ChordNotes.ParseRootPitchClass(bassName);
        if (bass != null)
        {
            classes.Add(ChordNotes.ShiftByCapo(bass.Value, capo));
        }

        return classes;
    }

    /// <summary>Shifts a pitch class up by <paramref name="capo"/> semitones, wrapping mod 12.</summary>
    private static int ShiftByCapo(int pitchClass, int capo) =>
        ((pitchClass + capo) % 12 + 12) % 12;

    /// <summary>Intervals above the root (excluding the root itself) for a quality suffix.</summary>
    private static IEnumerable<int> QualityIntervals(string quality)
    {
        // Triad skeleton
        bool minor = quality.StartsWith('m') && !quality.StartsWith("maj", StringComparison.OrdinalIgnoreCase);
        bool dim = quality.StartsWith("dim", StringComparison.OrdinalIgnoreCase) || quality.StartsWith('°');
        bool aug = quality.StartsWith("aug", StringComparison.OrdinalIgnoreCase) || quality.StartsWith('+');
        bool halfDim = quality.Contains("m7b5", StringComparison.OrdinalIgnoreCase);

        if (quality is "5")
        {
            yield return 7;
            yield break;
        }

        if (quality.StartsWith("sus2", StringComparison.OrdinalIgnoreCase))
        {
            yield return 2;
            yield return 7;
        }
        else if (quality.StartsWith("sus", StringComparison.OrdinalIgnoreCase))
        {
            yield return 5;
            yield return 7;
        }
        else if (dim || halfDim)
        {
            yield return 3;
            yield return 6;
        }
        else if (aug)
        {
            yield return 4;
            yield return 8;
        }
        else if (minor)
        {
            yield return 3;
            yield return 7;
        }
        else
        {
            yield return 4;
            yield return 7;
        }

        // Sevenths / extensions
        if (halfDim || quality.Contains('7') && dim)
        {
            yield return dim && !halfDim ? 9 : 10; // dim7 → bb7; m7b5 → b7
        }
        else if (quality.Contains("maj7", StringComparison.OrdinalIgnoreCase) ||
                 quality.Contains("M7", StringComparison.Ordinal))
        {
            yield return 11;
        }
        else if (quality.Contains('7') ||
                 (quality.Contains('9') && !quality.Contains("add", StringComparison.OrdinalIgnoreCase)))
        {
            yield return 10;
        }
        else if (quality.Contains('6'))
        {
            yield return 9;
        }

        if (quality.Contains('9'))
        {
            yield return 2;
        }
    }

    private static string NormalizePart(string part)
    {
        var pc = ChordNotes.ParseRootPitchClass(part);
        if (pc == null)
        {
            return part;
        }

        int qualityStart = part.Length > 1 && (part[1] == '#' || part[1] == 'b') ? 2 : 1;
        return ChordNotes.sharpNames[pc.Value] + part[qualityStart..];
    }
}
