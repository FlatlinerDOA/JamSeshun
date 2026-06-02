namespace JamSeshun.Services.Tuning;

public record GuitarTuning(string Name, Note[] Strings)
{
    // Strings are ordered low-to-high: string 6 → string 1
    public static readonly GuitarTuning Standard = Make("Standard",
        ("E", 82.41f, 2), ("A", 110.00f, 2), ("D", 146.83f, 3),
        ("G", 196.00f, 3), ("B", 246.94f, 3), ("E", 329.63f, 4));

    public static readonly GuitarTuning DropD = Make("Drop D",
        ("D", 73.42f, 2), ("A", 110.00f, 2), ("D", 146.83f, 3),
        ("G", 196.00f, 3), ("B", 246.94f, 3), ("E", 329.63f, 4));

    public static readonly GuitarTuning Dadgad = Make("DADGAD",
        ("D", 73.42f, 2), ("A", 110.00f, 2), ("D", 146.83f, 3),
        ("G", 196.00f, 3), ("A", 220.00f, 3), ("D", 293.66f, 4));

    public static readonly GuitarTuning OpenG = Make("Open G",
        ("D", 73.42f, 2), ("G", 98.00f, 2), ("D", 146.83f, 3),
        ("G", 196.00f, 3), ("B", 246.94f, 3), ("D", 293.66f, 4));

    public static readonly GuitarTuning OpenD = Make("Open D",
        ("D", 73.42f, 2), ("A", 110.00f, 2), ("D", 146.83f, 3),
        ("F#", 185.00f, 3), ("A", 220.00f, 3), ("D", 293.66f, 4));

    public static readonly GuitarTuning EbStandard = Make("Eb Standard",
        ("Eb", 77.78f, 2), ("Ab", 103.83f, 2), ("Db", 138.59f, 3),
        ("Gb", 185.00f, 3), ("Bb", 233.08f, 3), ("Eb", 311.13f, 4));

    public static GuitarTuning? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // Named tunings (tab files store them in header comments)
        var named = raw.Trim().ToUpperInvariant() switch
        {
            "STANDARD" or "EADGBE" => Standard,
            "DROP D" or "DROP-D" or "DROPD" => DropD,
            "DADGAD" => Dadgad,
            "OPEN G" => OpenG,
            "OPEN D" => OpenD,
            "EB STANDARD" or "HALF STEP DOWN" or "HALF-STEP DOWN" or "E FLAT STANDARD" => EbStandard,
            _ => (GuitarTuning?)null
        };
        if (named != null)
        {
            return named;
        }

        // Note-sequence format: "D A D G A D" or "Eb Ab Db Gb Bb Eb"
        // (WikiTabParser's TuningRegex captures [A-Ga-g#b ] so sequences like these are the common stored format)
        return TryParseNoteSequence(raw.Trim());
    }

    // Expected frequency ranges per string position (low E string → high E string)
    private static readonly (float Min, float Max)[] StringFreqRanges =
    [
        (55f,  115f),  // string 6 — low: B1..A2
        (80f,  160f),  // string 5
        (110f, 215f),  // string 4
        (150f, 285f),  // string 3
        (200f, 375f),  // string 2
        (260f, 450f),  // string 1 — high
    ];

    private static GuitarTuning? TryParseNoteSequence(string raw)
    {
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6)
        {
            return null;
        }

        var strings = new Note[6];
        for (int i = 0; i < 6; i++)
        {
            var (min, max) = StringFreqRanges[i];
            var note = FindNoteInRange(parts[i], min, max);
            if (note == null)
            {
                return null;
            }
            strings[i] = note.Value;
        }
        return new GuitarTuning(raw, strings);
    }

    private static Note? FindNoteInRange(string name, float minFreq, float maxFreq)
    {
        // Map enharmonic names to the canonical names used in Note.BaseNotes
        var canonical = name.ToUpperInvariant() switch
        {
            "AB" => "G#",
            "DB" => "C#",
            "GB" => "F#",
            "A#" => "Bb",
            "D#" => "Eb",
            _ => name
        };

        var baseNote = Note.BaseNotes.FirstOrDefault(n =>
            n.Name.Equals(canonical, StringComparison.OrdinalIgnoreCase));
        if (baseNote.Frequency == 0)
        {
            return null;
        }

        for (int octave = 0; octave <= 8; octave++)
        {
            var note = baseNote.ShiftOctave(octave);
            if (note.Frequency >= minFreq && note.Frequency <= maxFreq)
            {
                return note;
            }
        }
        return null;
    }

    private static GuitarTuning Make(string name, params (string Name, float Freq, int Octave)[] strings) =>
        new(name, strings.Select(s => new Note(s.Name, s.Freq, s.Octave)).ToArray());
}
