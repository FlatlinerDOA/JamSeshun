using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace JamSeshun.Views;

/// <summary>
/// A scrolling "tuning worm": a vertical history strip where each new reading
/// appears at the bottom and older readings scroll upward. Horizontal position
/// encodes cents error (centre = in tune, right = sharp, left = flat), colored
/// green→red by how far off it is. An internal timer samples the bound
/// <see cref="Cents"/> at a steady cadence so the trace scrolls smoothly even
/// though pitch readings only update a few times per second.
/// </summary>
public sealed class TuningWorm : Control
{
    private const double MaxCents = 50.0;   // full-width deflection
    private const double RowStep = 2.0;     // pixels advanced per sample
    private const int TickMs = 33;          // ~30 fps

    public static readonly StyledProperty<double> CentsProperty =
        AvaloniaProperty.Register<TuningWorm, double>(nameof(TuningWorm.Cents));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<TuningWorm, bool>(nameof(TuningWorm.IsActive));

    public static readonly StyledProperty<bool> HasReadingProperty =
        AvaloniaProperty.Register<TuningWorm, bool>(nameof(TuningWorm.HasReading));

    // NaN marks a gap (no confident reading) so silence scrolls as blank space.
    private readonly List<float> samples = new();
    private readonly DispatcherTimer timer;

    static TuningWorm()
    {
        AffectsRender<TuningWorm>(CentsProperty, IsActiveProperty, HasReadingProperty);
    }

    public TuningWorm()
    {
        this.timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        this.timer.Tick += this.OnTick;
    }

    public double Cents { get => this.GetValue(CentsProperty); set => this.SetValue(CentsProperty, value); }
    public bool IsActive { get => this.GetValue(IsActiveProperty); set => this.SetValue(IsActiveProperty, value); }
    public bool HasReading { get => this.GetValue(HasReadingProperty); set => this.SetValue(HasReadingProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        this.timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        this.timer.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Clear history when a fresh tuning session starts.
        if (change.Property == IsActiveProperty && change.GetNewValue<bool>())
        {
            this.samples.Clear();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!this.IsActive)
        {
            return;
        }

        this.samples.Add(this.IsActive && this.HasReading ? (float)this.Cents : float.NaN);

        int capacity = Math.Max(1, (int)(this.Bounds.Height / RowStep) + 1);
        if (this.samples.Count > capacity)
        {
            this.samples.RemoveRange(0, this.samples.Count - capacity);
        }

        this.InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        double w = this.Bounds.Width, h = this.Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        double cx = w / 2;
        double usableHalf = w / 2 - 8;

        // Guide lines: centre (in tune) + ±25 / ±50 cents.
        var centreLinePen = new Pen(new SolidColorBrush(Color.Parse("#334155")), 1);
        var guidePen = new Pen(new SolidColorBrush(Color.Parse("#1e293b")), 1);
        ctx.DrawLine(guidePen, new Point(cx - usableHalf, 0), new Point(cx - usableHalf, h));
        ctx.DrawLine(guidePen, new Point(cx - usableHalf / 2, 0), new Point(cx - usableHalf / 2, h));
        ctx.DrawLine(guidePen, new Point(cx + usableHalf / 2, 0), new Point(cx + usableHalf / 2, h));
        ctx.DrawLine(guidePen, new Point(cx + usableHalf, 0), new Point(cx + usableHalf, h));
        ctx.DrawLine(centreLinePen, new Point(cx, 0), new Point(cx, h));

        // Newest sample sits at the bottom; older ones scroll up.
        for (int i = 0; i < this.samples.Count; i++)
        {
            float cents = this.samples[this.samples.Count - 1 - i];
            if (float.IsNaN(cents))
            {
                continue;
            }

            double y = h - i * RowStep;
            if (y < 0)
            {
                break;
            }

            double clamped = Math.Clamp(cents / MaxCents, -1.0, 1.0);
            double x = cx + clamped * usableHalf;

            var color = ColorFor(Math.Abs(cents));
            ctx.DrawEllipse(new SolidColorBrush(color), null, new Point(x, y), 2.4, 2.4);
        }
    }

    private static Color ColorFor(double absCents) => absCents switch
    {
        < 5 => Color.Parse("#22c55e"),   // green — in tune
        < 15 => Color.Parse("#a3e635"),  // lime
        < 30 => Color.Parse("#f97316"),  // orange
        _ => Color.Parse("#dc2626"),     // red — well off
    };
}
