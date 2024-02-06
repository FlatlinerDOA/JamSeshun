using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls.Shapes;
using Avalonia.Animation;
using Avalonia.Animation.Easings;

namespace JamSeshun.Views
{
    public partial class TunerNeedle : Control
    {
        public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<TunerNeedle, double>(nameof(Angle));

        static TunerNeedle()
        {
            AffectsRender<TunerNeedle>(AngleProperty);
        }

        public TunerNeedle()
        {
            //var timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromSeconds(1 / 60.0);
            //timer.Tick += (sender, e) => this.Angle += Math.PI / 360;
            //timer.Start();
            this.Transitions = new Transitions()
            {
                new DoubleTransition()
                {
                    Duration = TimeSpan.FromMilliseconds(100),
                    Property = AngleProperty,
                    Easing = new CubicEaseInOut()
                }
            };
        }

        public double Angle
        {
            get => this.GetValue(AngleProperty);
            set => this.SetValue(AngleProperty, value);
        }

        public override void Render(DrawingContext drawingContext)
        {
            var lineLength = Math.Sqrt((this.Width * this.Width) + (this.Height * this.Height));

            var diffX = CalculateAdjSide(Angle - 90f, lineLength);
            var diffY = CalculateOppSide(Angle - 90f, lineLength);


            var p1 = new Point(200, 200);
            var p2 = new Point(p1.X + diffX, p1.Y + diffY);

            var pen = new Pen(Brushes.Green, 2, lineCap: PenLineCap.Round);
            ////var boundPen = new Pen(Brushes.Black);

            drawingContext.DrawLine(pen, p1, p2);

            //var (minX, minY) = (Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y));
            //var bounds = new Rect(minX, minY, Math.Max(p1.X, p2.X) - minX, Math.Max(p1.Y, p2.Y) - minY);
            //drawingContext.DrawRectangle(Brushes.Black, pen, bounds);
        }

        public static double CalculateAdjSide(double angleDegrees, double hypotenuseLength)
        {
            // Convert angle from degrees to radians
            double angleRadians = angleDegrees * (Math.PI / 180);
            // Calculate adjacent side length using cosine
            return Math.Cos(angleRadians) * hypotenuseLength;
        }

        public static double CalculateOppSide(double angleDegrees, double hypotenuseLength)
        {
            // Convert angle from degrees to radians
            double angleRadians = angleDegrees * (Math.PI / 180);
            // Calculate opposite side length using sine
            return Math.Sin(angleRadians) * hypotenuseLength;
        }
    }
}
