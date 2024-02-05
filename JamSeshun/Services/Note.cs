using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamSeshun.Services
{
    public record struct Note(string Name, float Frequency, int Octave = 0)
    {
        public Note ShiftOctave(int octave) => this.Octave switch
        {
            0 => octave == 0 ? this : this with
            {
                Frequency = (float)(this.Frequency * Math.Pow(2, octave)),
                Octave = octave
            },
            _ => throw new NotSupportedException("Can only shift from the base note")
        };
    };
}
