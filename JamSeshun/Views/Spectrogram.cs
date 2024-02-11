namespace JamSeshun.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using JamSeshun.Services.Tuning;
using MathNet.Numerics.IntegralTransforms;
using System;
using System.Linq;
using System.Numerics;



public class Spectrogram : Canvas
{
    public static readonly StyledProperty<double[]> LatestPcmSampleProperty =
    AvaloniaProperty.Register<Spectrogram, double[]>(nameof(LatestPcmSample));

    private ReadOnlyMemory<double> pcmData = new double[2048]; // Your PCM data

    public Spectrogram()
    {
    }

    private int currentOffset = 0;
    public void AddBuffer(ReadOnlyMemory<double> memory)
    {
        ////memory.Slice(this.cu).CopyTo(memory.sl)
        this.pcmData = memory;
        this.InvalidateVisual();
    }

    public double[] LatestPcmSample { get; set; }

    public void RenderSpectrogram(Canvas canvas, double[,] spectrogramData)
    {
        var writableBitmap = FftAlgorithm.RenderSpectrogramToBitmap(spectrogramData);

        // Display the spectrogram
        var image = new Image { Source = writableBitmap };
        canvas.Children.Clear();
        canvas.Children.Add(image);
    }

    public void DrawSpectrum(Canvas canvas)
    {
        // Convert PCM data to complex for FFT
        var complexData = this.pcmData.Span.ToComplex();

        // Perform FFT
        Fourier.Forward(complexData, FourierOptions.Matlab);

        // Clear previous drawings
        canvas.Children.Clear();

        double canvasWidth = canvas.Bounds.Width;
        double canvasHeight = canvas.Bounds.Height;
        double maxFrequencyMagnitude = complexData.Max(c => c.Magnitude);

        // Calculate scaling factors
        double xScale = canvasWidth / complexData.Length;
        double yScale = canvasHeight / maxFrequencyMagnitude;

        for (int i = 0; i < complexData.Length; i++)
        {
            double magnitude = complexData[i].Magnitude;

            // Create a line for each frequency component
            var line = new LineGeometry
            {
                StartPoint = new Point(i * xScale, canvasHeight),
                EndPoint = new Point(i * xScale, canvasHeight - magnitude * yScale)
            };

            var path = new Path
            {
                Data = line,
                Stroke = Brushes.Blue,
                StrokeThickness = 1
            };

            canvas.Children.Add(path);
        }

    }
}
