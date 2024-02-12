using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JamSeshun.Services.Tuning;

public interface IPitchDetector
{
    int SampleBufferSize { get; }
    
    DetectedPitch DetectPitch(ReadOnlySpan<float> signal);
}
