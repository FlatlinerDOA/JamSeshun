using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamSeshun.Services
{
    public record struct Note(string Name, float Frequency, int Octave = 0)
    {
        public static readonly Note[] BaseNotes = new Note[]
        {
                new("C", 16.35f),
                new("C#", 17.32f),
                new("D", 18.35f),
                new("Eb", 19.45f),
                new("E", 20.60f),
                new("F", 21.83f),
                new("F#", 23.12f),
                new("G", 24.50f),
                new("G#", 25.96f),
                new("A", 27.50f),
                new("Bb", 29.14f),
                new("B", 30.87f),
        };

        private static readonly Note[] AllNotes;
        static Note()
        {
            AllNotes = (from note in Note.BaseNotes
                             from octave in Enumerable.Range(0, 9)
                             let shiftedNote = note.ShiftOctave(octave)
                             select shiftedNote).ToArray();
        }

        const double AFrequency = 440;
        static double ToneStep = Math.Pow(2, 1.0 / 12);

        public static Note GetClosestNote(float estimatedFrequency, float minimumFrequency = 50, float maximumFrequency = 500) =>
            estimatedFrequency <= 0.0f ? default : AllNotes.Where(n => n.Frequency >= minimumFrequency && n.Frequency <= maximumFrequency).MinBy(note => Math.Abs(note.Frequency - estimatedFrequency));


        private double GetToneStep(double frequency)
        {
            return Math.Log(frequency / AFrequency, ToneStep);
        }

        public Note ShiftOctave(int octave) => this.Octave switch
        {
            0 => octave == 0 ? this : this with
            {
                Frequency = (float)(this.Frequency * Math.Pow(2, octave)),
                Octave = octave
            },
            _ => throw new NotSupportedException("Can only shift from the base note")
        };

        public override string ToString()
        {
            return $"{this.Name}{this.Octave}";
        }

        public float GetCentsError(float estimatedFrequency) => this.Frequency > 0.0f ? (float)(1200.0d * Math.Log2(estimatedFrequency / this.Frequency)) : 0;
    };
}
