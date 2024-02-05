using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamSeshun.Services.Tuning
{
    public readonly record struct DetectedPitch(float EstimatedFrequency, Note ClosestNote, float ErrorInCents);
}
